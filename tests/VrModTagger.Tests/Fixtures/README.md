# Test fixtures

Small excerpts of each source, saved on 2026-10-28, so the parser tests run offline. They are cut down to what the tests need:

| File | Source | What's kept |
|---|---|---|
| `mrsurvivor.html` | [mrsurvivor-installers.com](https://mrsurvivor-installers.com/) | The VR HUB game list table only |
| `pcvrgamer-page*.json` | [pcvrgamer.net](https://pcvrgamer.net/) REST API | 15 posts; only paragraphs with a Steam link and the paragraph before each |
| `pcvrcentral-*.html` | [pcvrcentral.com](https://pcvrcentral.com/) `/mods` and `/native-vr` | A sample of games, with only slug, title, Steam AppID, VR status and mod name/author/status/platforms, in the same page-data format |
| `hub-tree.json`, `*-core.ps1`, `hub-README.md` | [PCVR Mods Installer Hub](https://github.com/Mr-Nlce/PCVR-Mods-Installer-Hub) | Script paths, the script lines the parser reads, and the README title list. MIT License, Copyright (c) 2026 Mr-N!ce |

To check the parsers against the full live sites, run the tests with `VRMOD_LIVE_TESTS=1` set.
