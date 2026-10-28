# Excerpt (parsed lines only) of the PCVR Mods Installer Hub script of the same name. MIT License, Copyright (c) 2026 Mr-N!ce.
$Host.UI.RawUI.WindowTitle = 'Black Mesa VR Installer'
$APP_ID = '362890'
        [pscustomobject]@{ Name='Black Mesa VR'; Args="-applaunch $APP_ID $LAUNCH_ARGS"; BlueShift=$false },
        [pscustomobject]@{ Name='Black Mesa Blue Shift VR'; Args="-applaunch $APP_ID $LAUNCH_ARGS -game bshift"; BlueShift=$true }
        $picked = Get-GameFolderInteractive -GameName 'Black Mesa' -ProbeFile $GAME_EXE -ManualUrl 'https://store.steampowered.com/app/362890/Black_Mesa/'
