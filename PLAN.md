# VR Mod Tagger — Playnite Plugin Plan

A Playnite **Generic plugin** that pulls VR-mod game lists from four community sources, matches them against the user's existing library, and adds a `VR Mod` tag plus a link to each matched game's mod page.

---

## 1. Source findings (checked 2026-10-28)

| Source | How the data is exposed | Fetch method | Match keys available | Link to add |
|---|---|---|---|---|
| **pcvrcentral.com** | Next.js site. `/mods` and `/native-vr` embed their full catalogs (slug, title, Steam AppID, native VR status, mods) in the page data. The **native VR games** are kept and flagged `NativeVr`. | 2 page requests → decode Next.js page data | Title, **Steam AppID** | `https://pcvrcentral.com/games/{slug}` |
| **pcvrgamer.net** | WordPress with an open REST API. `/wp-json/wp/v2/posts` (~145 posts). Categories: `26 = PCVR MODS` (106), `31 = MrSurviv0r Compatible`, `32 = Hall of Fame`, `33 = Left Handed Support`, `25 = PCVR NEWS` (exclude). | `GET /wp-json/wp/v2/posts?categories=26&per_page=100&page=N&_fields=title,link,categories,content` | Title (HTML-encoded, sometimes "X by Modder"), Steam links inside `content` if present | Post `link` |
| **mrsurvivor-installers.com** | Single static page. The game list is an HTML `<table>`: `<tr><td><b>Title</b></td><td>Platforms</td></tr>`. No per-game URLs. | Fetch HTML → parse table rows | Title, platforms (Steam / GOG / Epic / Meta Quest / Manual) | Site root (or `#vr-hub` anchor) |
| **GitHub: Mr-Nlce/PCVR-Mods-Installer-Hub** | `Core/{Name}VR/` folder per game (348 titles in the README). Each `{Name}VR-core.ps1` calls the installer with `-AppId "214490"` and `-SteamFolderNames @(...)`. The README also has a bullet list of titles. | GitHub Trees API (1 call) → raw `*-core.ps1` files → regex `AppId "(\d+)"` | **Steam AppID**, Steam folder names, README title | `https://github.com/Mr-Nlce/PCVR-Mods-Installer-Hub` (or the game's `Core/{Name}VR` tree URL) |

Things to watch for:
- **Include** Meta Quest-only entries (MrSurvivor "Meta Quest", such as Half-Life 2: Episode One and Aliens Versus Predator Quest). These are Quest ports or mods of PC games and still count as VR mods. Record the platform so the link and an optional tag can say it's for Quest.
- **Include** PCVR Central's `/native-vr` games (games with built-in VR, no mod needed). They get an extra `Native VR` tag so you know the game runs in VR on its own.
- Titles carry noise: `VR`, `(motion controls)`, `[WIP]`, `[FREE]`, `— KHARVOX`, `by CircuitLord`, `(+ HD texture mod)`.
- Unauthenticated GitHub API allows 60 requests per hour. Use the single Trees call plus `raw.githubusercontent.com` (no API quota), and support an optional PAT in settings.

---

## 2. Architecture

```
VrModTagger/                        (C# .NET Framework 4.6.2, Playnite SDK 6.x)
├─ VrModTagger.cs                   GenericPlugin: menu items, OnLibraryUpdated hook
├─ Settings/                        ISettings + WPF settings view
├─ Sources/
│  ├─ IVrModSource.cs               Task<IReadOnlyList<VrModEntry>> FetchAsync(ct)
│  ├─ PcvrCentralSource.cs
│  ├─ PcvrGamerSource.cs
│  ├─ MrSurvivorSource.cs
│  └─ InstallerHubGitHubSource.cs
├─ Models/VrModEntry.cs             Title, NormalizedTitle, SteamAppId?, Source, Url, ModNames[], Flags
├─ Matching/
│  ├─ TitleNormalizer.cs
│  └─ GameMatcher.cs
├─ Cache/VrModCache.cs              JSON in GetPluginUserDataPath(), with timestamp per source
├─ Applying/LibraryTagger.cs        Adds tag + links in BufferedUpdate
└─ extension.yaml
```

**Dependencies:** `PlayniteSDK` only. HTML is parsed with regexes, and JSON with .NET Framework's built-in `DataContractJsonSerializer` and `JavaScriptSerializer`, so nothing can clash with the libraries Playnite already loads.

### Data model
```csharp
class VrModEntry {
  string Source;          // "PCVR Central", "PCVR Gamer", ...
  string Title;           // raw
  List<string> AlternateTitles;
  string NormalizedTitle;
  List<uint> SteamAppIds;  // several editions can share one mod
  string Url;             // link to attach
  List<string> ModNames;  // e.g. "UEVR profile", "Luke Ross R.E.A.L."
  List<string> Platforms; // MrSurvivor platforms; PCVR Central mod platforms (pcvr/quest/pico)
  bool QuestOnly;         // mod only exists for standalone headsets
  bool NativeVr;          // PCVR Central built-in VR games
  bool TemporarilyUnavailable;
}
```

---

## 3. Fetching (per source)

1. **PcvrCentralSource** (built in milestone 4): no crawl is needed. `/mods` (1,122 games) and `/native-vr` (3,369 games) each embed their whole catalog in the Next.js page data (`self.__next_f.push([1,"..."])` chunks). The plugin joins the chunks, cuts out the `"games":[...]` array and reads it with `JavaScriptSerializer`, so **two requests** cover the site.
   - Each game has `slug`, `title`, `steamAppId` (`"$undefined"` for about 100 games not on Steam), `nativeVr` (`none` / `supported` / `vr-only`) and `mods[]` (name, author, status, platforms).
   - Games on both pages are merged by slug. Mods with status `broken` are left out; abandoned, superseded and update-detected mods are kept. A game whose mods only run on `quest`/`pico` is flagged Quest-only.
   - With "Include native VR" off, `/native-vr` isn't fetched.
   - Live run: 4,480 entries (1,120 with mods, 3,369 native VR, 3 Quest-only), 4,382 with Steam AppIDs.
2. **PcvrGamerSource** (built in milestone 3): page through `/wp-json/wp/v2/posts?categories=26&per_page=100` until a short page comes back. Every mod post is in category 26; categories 31/32/33 are only subsets of it. Live run: 106 posts, 33 with Steam AppIDs. Titles are often abbreviated or misspelled ("R.D.R.", "Mass Effect: LE", "Deux Ex"), so title matching leans on the alternate names below.
   - Posts also link other Steam games, such as the modder's own game (the Need for Speed post links CYBRID). A Steam link is used only when the sentence before it says to buy the game and doesn't mention support or donations, when its store slug or link text matches the post title, or when it continues a list of buy links (RAZE XR's six supported games).
   - The game name from the link text, "Buy *Red Dead Redemption* on Steam", or the store slug is kept as an alternate title. Generic text like "HERE" and "THE GAME" is ignored.
3. **MrSurvivorSource** (built in milestone 2): parse `table.game-list-table` rows. The first cell is the title and the second holds platforms split on `|`. A platform note after ` — ` is either "temporarily unavailable" (sets a flag) or the real game name (`Steam — Halo: The Master Chief Collection`, kept as an alternate title). Live run: 69 games, 12 Quest-only. Keep rows whose only platform is "Meta Quest" and set a `QuestOnly` flag on them. Mark "temporarily unavailable" rows but still include them.
4. **InstallerHubSource** (built in milestone 2): `GET api.github.com/repos/Mr-Nlce/PCVR-Mods-Installer-Hub/git/trees/HEAD?recursive=1`, filter `Core/{Folder}/{Folder}-core.ps1` (281 scripts), and fetch each from raw.githubusercontent.com. The scripts don't share one format, so:
   - **Steam AppIDs**: every quoted 3–7 digit number on a line mentioning `AppId`/`APP_ID`, plus `appmanifest_N`. Some scripts list several (Fallout 3 GOTY + base; the Luke Ross script lists 37 games). SteamVR (250820) and Steamworks redistributables (228980) are ignored. About 200 of the 281 scripts give an ID; most of the rest are emulator or non-Steam mods.
   - **Titles**: `WindowTitle`, `-GameTitle`, `-GameName`, `$GAME_NAME`, `-Title` and `-SteamFolderNames`. The first is the title and the rest are alternate titles; if none is found, the title comes from the folder name.
   - **README**: the "currently listed titles" list (351 bullets) adds title-only entries for games no script covers.
   - Live run on 2026-10-28: 393 entries, 192 with Steam AppIDs. Cache keyed by tree SHA, so nothing is re-fetched if the SHA hasn't changed.

All sources use a shared `HttpClient` with a descriptive User-Agent, a timeout, and retry with backoff. A failing source logs and is skipped without aborting the run.

---

## 4. Matching to the Playnite library

Order of precedence for each `Game` in `PlayniteApi.Database.Games`:

1. **Steam AppID**: if `game.PluginId == SteamLibrary` (`CB91DFC9-B977-43BF-8E70-55F46E410FAB`), then `game.GameId` is the AppID. Match it against entries with `SteamAppId`. This is exact and covers most of the library.
2. **Steam link on non-Steam copies**: parse `store.steampowered.com/app/(\d+)` from `game.Links` (GOG and Epic games often have one from metadata).
3. **Normalized title, exact match** (built in milestone 5, `Matching/TitleNormalizer.cs`). Every title becomes a key with no spaces or punctuation (`Alien: Isolation` → `alienisolation`):
   - lowercase, strip diacritics and `™ ® ©`, `&` → `and`, drop `.` and `'` (`R.E.P.O.` → `repo`), Roman numerals I–XX → digits (except `X`, which is usually a letter: X-Wing, Mega Man X)
   - `GTA` → `grand theft auto`; a final `DE` / `LE` / `SE` → Definitive / Legendary / Special Edition
   - remove edition suffixes on both sides: GOTY (incl. "Game of the Year Enhanced"), Definitive, Enhanced, Complete, Deluxe, Ultimate, Gold, Special, Anniversary, Standard Edition, Director's Cut. `Remastered` and `Remake` are kept, since they're usually separate games.
   - remove all `(…)` and `[…]`; a `(2016)`-style year is kept separately and must agree with the game's title year or release year (`Prey (2006)` ≠ the 2017 Prey)
   - mod titles only: drop ` — …` and trailing `VR` / `PCVR` / `XR` / `Quest` / `VRO`, and `Mod` only after `VR` (so `Garry's Mod VR` keeps "Mod"). A mod title is indexed both with and without these rules.
   - ` by Modder` is **not** stripped, since it can't be told apart from titles like "Bound by Flame".
   - A title match is skipped when both sides have Steam AppIDs and none are shared, since that means a different game or edition.
   - Native-VR-only entries match only by Steam AppID or their exact name, so `Fallout 4 VR` never tags flat `Fallout 4` as Native VR.
4. **Fuzzy fallback** (opt-in, off by default): Jaro-Winkler ≥ 0.95 on keys of at least 6 characters, only for games with no exact match, and only when both keys contain the same numbers (so `Titanfall` never pairs with `Titanfall 2`). Fuzzy hits go to a **review dialog** (milestone 7) instead of being applied.

Check on saved data: with PCVR Central's 4,482 games as a stand-in library, the other three sources matched 378 of 521 entries. A manual review of the title matches found one ambiguous case ("Tomb Raider", 1996 vs 2013), which the Steam AppID check resolves in a real library.

A manual override list in settings (`include` / `exclude` by Game Id) handles the cases matching gets wrong.

---

## 5. Applying to Playnite

Built in milestone 6. `Applying/TagPlanner.cs` works out every change without touching Playnite, so it's unit-tested. `Applying/LibraryTagger.cs` then applies the changes inside `BufferedUpdate()` with one `Games.Update(...)` call.
- **Tags**: `VR Mod` when any matched entry has a mod. **`Native VR`** when an entry says the game has built-in VR; a game with both gets both, and a native-only game gets `Native VR` alone. Optional `VR Mod: Meta Quest` for Quest-only mods and `VR Mod: <Source>` per-source tags, both off by default. Tag names come from settings, and tags are created if missing.
- **Links**: one per distinct mod page URL, named `VR Mod (Source)`, `VR Mod (Source, Meta Quest)` or `Native VR (Source)`. Nothing is added when the game already has a link to that URL, including one the user made. A plugin link whose label changed (e.g. it became Quest-only) is renamed.
- **Ownership**: the plugin only ever removes the configured tag names (and `VR Mod: …` tags) and links named `VR Mod (…)` / `Native VR (…)`. Other tags and links are never touched.
- **Removal pass** (on by default): games that no longer match lose the plugin's tags and links. **If any source fails to fetch, no tags are removed that run, and that source's links are kept**, so an outage doesn't strip tags from your library. Links from disabled sources are removed.
- **Excluded games** (`ExcludedGameIds` in settings; the context menu comes in milestone 7) lose the plugin's tags and links even with the removal pass off.
- Fuzzy matches are never applied here; they wait for the review dialog.
- **Confirm first**: after matching, a dialog lists the counts (games gaining or losing each tag, links added or removed) and asks before anything changes.
- **Undo**: the applied changes are saved to `last-run.json` in the plugin's data folder. *Undo last run* reverses exactly those changes and leaves edits made by hand since then alone. Only the most recent run can be undone.
- Renaming a tag in settings leaves the old tag on games; the plugin then manages the new name.

---

## 6. UX

Built in milestone 7.
- **Main menu → Extensions → VR Mod Tagger**
  - *Update VR mod tags (all games)*: loads each list (saved copy if recent, else fetch), matches, asks about new similar-title matches, shows the change summary and asks before applying. Progress dialog is cancellable.
  - *Refresh source data and update tags*: same, but fetches every list.
  - *Review similar-title matches...*: shows every suggestion, including ones already answered, so answers can be changed.
  - *Undo last run*
- **Game right-click menu → VR Mod Tagger:** *Check for VR mods* (selected games; for up to 10 games it lists what was found per game, then offers the changes), *Exclude from VR mod tagging* (removes the add-on's tags and links and stops tagging), *Include in VR mod tagging again* (offers to check right away).
- **Saved lists** (`Caching/`): each source's entries are saved as JSON in the add-on's data folder under `cache/`. A list newer than *Keep saved lists (days)* (default 7) is reused without a download. If a fetch fails, the older saved list is used and the dialog says so, which counts as success for the removal pass. Lists saved by an older add-on version (format number) are ignored. PCVR Central without native VR is saved separately.
- **Similar-title review**: a dialog lists suggestions with a "Same game" checkbox each. Answers are stored per game and entry in settings (`FuzzyDecisions`), so a suggestion is only asked once. Unticked means "no". Approved ones are tagged like exact matches.
- **After library updates** (opt-in setting, off by default): runs automatically using only recent saved lists, with no downloads and no questions. It skips the run if any enabled list isn't saved recently, so it never removes tags because of a missing list. The result is reported as a notification and can be undone.
- **Notifications**: every applied run posts a Playnite notification such as "updated 37 games (12 newly tagged "VR Mod", 3 newly tagged "Native VR")".
- **Settings:** toggles per source, tag names, include native VR, Quest and per-source tags, removal pass, fuzzy matching and threshold, auto-run, saved-list age, GitHub token, and a count of excluded games.
- Not built: per-mod-type tags (`UEVR`, `Luke Ross`). Mod names are stored, so these can be added later.

---

## 7. Milestones

1. **Scaffold**: create the project from Playnite's GenericPlugin template, `extension.yaml`, settings, and logging. Verify it loads in Playnite.
2. **GitHub + MrSurvivor sources**: the cheapest sources, with AppIDs. Add unit tests on saved HTML/ps1 fixtures.
3. **PCVR Gamer source**: WP REST pagination.
4. **PCVR Central source**: done. The page data on `/mods` and `/native-vr` holds the full catalog, so no crawler is needed.
5. **Normalizer + matcher**: table-driven unit tests with real-world titles (`Half-Life 2: Episode One`, `DOOM (2016) VR — KHARVOX`, `Halo MCC VR`).
6. **Library tagger**: done (tags, links, removal pass, confirm dialog, undo). Still to do: test against a copy of a real Playnite library.
7. **UX polish**: done (menus, game right-click menu, review dialog, saved lists, auto-run, notifications).
8. **Package**: `Toolbox.exe pack` → `.pext`. Optionally add an installer manifest for Playnite add-on database submission.

---

## 8. Risks & open questions

- **Scraping fragility**: three of the four sources are HTML or undocumented. Isolate each parser, add fixture tests, and fail soft.
- **Site terms and load**: respect `robots.txt` (PCVR Central disallows only `/admin`, `/forums`, `/review`), throttle requests, cache, and consider contacting the PCVR Central maintainers about a JSON export.
- **What counts as "has a VR mod"**: decide whether `[WIP]` and "temporarily unavailable" entries qualify. Proposed default: yes, but tagged `VR Mod (WIP)` when the setting is on.
- **Mod quality**: PCVR Central exposes badges (`Excellent`, `Experimental`, `6DOF`). This could be surfaced as tags later.
