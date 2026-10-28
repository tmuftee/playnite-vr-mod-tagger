# Excerpt (parsed lines only) of the PCVR Mods Installer Hub script of the same name. MIT License, Copyright (c) 2026 Mr-N!ce.
$Host.UI.RawUI.WindowTitle = "Alien: Isolation VR Mod Installer"
$GAME_NAME = "Alien Isolation"
 $c = Join-Path $lib "steamapps\common\$GAME_NAME"
if (-not $gamePath) { $gamePath = Find-SteamGameFolder -AppId "214490" -SteamFolderNames @("Alien Isolation") }
