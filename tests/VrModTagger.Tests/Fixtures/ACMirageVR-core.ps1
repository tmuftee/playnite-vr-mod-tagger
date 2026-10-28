# Excerpt (parsed lines only) of the PCVR Mods Installer Hub script of the same name. MIT License, Copyright (c) 2026 Mr-N!ce.
$Host.UI.RawUI.WindowTitle = "Assassin's Creed Mirage VR Installer"
$STEAM_APPID       = "3035570"
 $gamePath = Try-FindSteamGame -Folder $GAME_STEAM_FOLDER -Title "Assassin's Creed Mirage" -AppID $STEAM_APPID -ExeName $GAME_EXE
 $gamePath = Find-SteamGameFolder -AppId $STEAM_APPID -SteamFolderNames @($GAME_STEAM_FOLDER) -ProbeExe $GAME_EXE `
