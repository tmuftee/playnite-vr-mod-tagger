# Changelog

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
