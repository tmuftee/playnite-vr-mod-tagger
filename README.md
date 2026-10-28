# VR Mod Tagger for Playnite

> **AI disclosure.** Most of this add-on's code, tests and documentation were written by Claude, Anthropic's AI model, through Claude Code. The owner set the goals, explained what the add-on should do, tested it on a real Playnite library, troubleshot problems, and reviewed and approved the changes. Please review it with that in mind, and report anything that looks wrong.

A [Playnite](https://playnite.link/) add-on that finds which games in your library can be played in VR, either through a community VR mod or because they have VR built in. It tags them and links each game to its mod page.

> **Beta.** It works on the author's library, but it changes tags and links on many games at once. Back up your Playnite library before the first run. Every run asks first and can be undone.

## What it does

- Reads the VR mod lists of four community sites:
  - [PCVR Central](https://pcvrcentral.com/): mods and games with built-in VR
  - [PCVR Gamer](https://pcvrgamer.net/)
  - [MrSurvivor Installers](https://mrsurvivor-installers.com/)
  - [PCVR Mods Installer Hub](https://github.com/Mr-Nlce/PCVR-Mods-Installer-Hub)
- Matches them to your games by Steam ID, by a Steam store link on GOG/Epic copies, or by title. Title matching ignores punctuation, editions and "VR" suffixes, so "Diablo® II: Resurrected™" matches "Diablo 2 Resurrected".
- Adds tags:
  - **VR Mod**: a VR mod exists for the game
  - **Native VR**: the game has VR built in, so no mod is needed
  - Optional: **VR Mod: Meta Quest** for Quest-only mods, and one tag per source
- Adds links such as `VR Mod (PCVR Gamer)`, `VR Mod (MrSurvivor, Meta Quest)` or `Native VR (PCVR Central)`.

It only ever removes its own tags and links. Other tags, and links you added yourself, are never touched.

## Install

1. Download the `.pext` file from the [latest release](https://github.com/tmuftee/playnite-vr-mod-tagger/releases).
2. Drag it onto the Playnite window, or double-click it, and confirm.
3. Restart Playnite when asked.

Requires Playnite 10 on Windows.

## Use

**Extensions → VR Mod Tagger**

- **Update VR mod tags (all games)**: loads the lists, matches your library, shows what will change and asks before applying.
- **Refresh source data and update tags**: the same, but downloads every list again.
- **Review similar-title matches…**: only when similar-title matching is turned on in settings.
- **Undo last run**: reverses exactly what the last run changed.

**Right-click a game → VR Mod Tagger**

- **Check for VR mods**: for the selected games.
- **Exclude from VR mod tagging** / **Include in VR mod tagging again**

Settings are in Playnite's **Add-ons** window, under **Extensions settings → Generic → VR Mod Tagger**: sources, tag names, Quest and per-source tags, removal of stale tags, similar-title matching, automatic updates after library updates, and how long downloaded lists are kept.

## Privacy, security and network use

**What it reads.** From Playnite's database, the add-on reads each game's name, release year, tags and links, plus which library it's from and its ID there, used to recognise Steam games by their Steam ID. It doesn't read or touch your store logins, other add-ons' data, or anything outside Playnite's database and its own data folder.

**What it sends.** Nothing about you or your library leaves your PC. It only downloads public pages, over HTTPS, from:
- pcvrcentral.com
- pcvrgamer.net
- mrsurvivor-installers.com
- api.github.com and raw.githubusercontent.com

Requests identify themselves with a `VrModTagger/<version>` User-Agent linking to this repository. Downloaded lists are saved in the add-on's data folder and reused for 7 days by default, so most runs make no requests at all.

**What it writes.** Only tags and links on your games, and only after you confirm (or, if you turn on automatic updates, as described in settings). It removes only its own tags and links, and the last run can be undone.

**Treating the sites as untrusted.** The add-on never runs anything it downloads. The Installer Hub's PowerShell scripts are read as text and never executed. Downloaded data is also limited and checked:
- Only `https://` links to the four sites themselves are ever added to your games. `file:`, custom-protocol and other-site links are dropped, and any such link already carrying the add-on's name is removed.
- Downloads are capped at 32 MB.
- Text matching on downloaded pages stops after 5 seconds per page.
- The data is parsed as plain values, never as program types.

If a site fails these checks, it's skipped for that run and the last saved list is used.

**GitHub token (optional).** It's only needed if GitHub rate-limits you, and is only ever sent to `https://api.github.com`. It's stored unencrypted in the add-on's settings file, so use a fine-grained token with no permissions; public data needs none.

Found a security problem? See [SECURITY.md](SECURITY.md).

## Reporting problems

Please [open an issue](https://github.com/tmuftee/playnite-vr-mod-tagger/issues/new/choose). Include the game's name as Playnite shows it, what you expected, and the VR Mod Tagger lines from `%AppData%\Playnite\extensions.log`.

Wrong or missing VR mod information usually comes from the source sites, so check the game's page there too.

## Building

Needs the .NET SDK 8 or later. The add-on targets .NET Framework 4.6.2, which builds through the reference-assemblies package.

```powershell
dotnet test tests/VrModTagger.Tests
./pack.ps1   # builds and writes dist/<id>_<version>.pext
```

The tests run offline against small excerpts of each site in `tests/VrModTagger.Tests/Fixtures`. Set `VRMOD_LIVE_TESTS=1` to also run them against the live sites.

Releases are built by GitHub Actions when a `v*` tag is pushed. The tag must match `Version` in `extension.yaml`.

## Credits

The VR mod information comes from the sites above and the modders they list. This add-on only links to them. Support the modders directly.

## License

[MIT](LICENSE)
