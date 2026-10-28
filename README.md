# VR Mod Tagger for Playnite

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

## Privacy and network use

The add-on downloads public pages from the four sites above, plus the GitHub API for the Installer Hub. It sends nothing about your library anywhere.

Downloaded lists are saved in the add-on's data folder and reused for 7 days by default, so most runs make no requests at all. Requests identify themselves with a `VrModTagger/<version>` User-Agent that links to this repository.

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
