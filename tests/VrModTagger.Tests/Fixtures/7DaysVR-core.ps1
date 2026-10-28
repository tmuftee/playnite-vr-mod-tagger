# Excerpt (parsed lines only) of the PCVR Mods Installer Hub script of the same name. MIT License, Copyright (c) 2026 Mr-N!ce.
$Host.UI.RawUI.WindowTitle = "7 Days to Die VR Mod Installer"
$GAME_NAME = "7 Days To Die"
 $candidate = Join-Path $lib "steamapps\common\$GAME_NAME"
 $gamePath = Try-FindSteamGame -Folder $GAME_NAME -Title "7 Days to Die"
if (-not $gamePath) { $gamePath = Find-SteamGameFolder -AppId "251570" -SteamFolderNames @("7 Days To Die") }
