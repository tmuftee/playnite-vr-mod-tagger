# Excerpt (parsed lines only) of the PCVR Mods Installer Hub script of the same name. MIT License, Copyright (c) 2026 Mr-N!ce.
# Dark Descent on Steam (AppID 57300) to be installed.
$GAME_APPID = "57300"
$GAME_NAME = "Amnesia The Dark Descent"
            $contract = New-PCVRInstallerContract -Id 'sclerosis' -GameName 'Amnesia VR' -Acquisition External `
 $candidate = Join-Path $lib "steamapps\common\$GAME_NAME"
    if (-not $cand) { $cand = Find-SteamGameFolder -AppId "57300" -SteamFolderNames @("Amnesia The Dark Descent") -ProbeExe "Sclerosis.exe" -GogNames @("Amnesia The Dark Descent") }
if (-not $gamePath) { $gamePath = Find-SteamGameFolder -AppId "57300" -SteamFolderNames @("Amnesia The Dark Descent") -ProbeExe "Sclerosis.exe" -GogNames @("Amnesia The Dark Descent") }
