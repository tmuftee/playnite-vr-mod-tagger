# Changelog

## 0.1.1 (beta) — 2026-10-28

- Fixed: the optional Meta Quest tag now marks every game with a standalone Quest version. Before, it only marked games whose mods were all Quest-only, so games with both a PC mod and a Quest port (Half-Life with Lambda1VR, and 13 others on PCVR Central) were missed.
- Fixed: mod titles that number the first game ("Half Life 1", "Quake 1") now match the store name ("Half-Life", "Quake").
- Saved lists from 0.1.0 are downloaded again once, to pick up the Quest information.

## 0.1.0 (beta) — 2026-10-28

First beta.

- Reads VR mod lists from PCVR Central, PCVR Gamer, MrSurvivor Installers and the PCVR Mods Installer Hub.
- Matches games by Steam ID, by Steam store link (GOG/Epic copies) and by title.
- Adds a **VR Mod** tag, a **Native VR** tag for games with built-in VR, and a link to each mod page.
- Optional tags for Meta Quest-only mods and per source.
- Asks before changing anything, and can undo the last run.
- Removes its own tags and links from games that drop off every list, but never when a list couldn't be loaded.
- Saves downloaded lists for 7 days (configurable) and falls back to them if a site is down.
- Optional similar-title matching with a review window.
- Game right-click menu: check selected games, exclude or include them.
- Optional automatic update after each library update, using saved lists only.
- Treats the source sites as untrusted: only https links to the sources' own sites are added to games, downloads are size-limited, parsing has time limits, and the GitHub token can only be sent to api.github.com.
