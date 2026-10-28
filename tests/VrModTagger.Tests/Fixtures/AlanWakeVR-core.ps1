# Excerpt (parsed lines only) of the PCVR Mods Installer Hub script of the same name. MIT License, Copyright (c) 2026 Mr-N!ce.
$Host.UI.RawUI.WindowTitle = 'Alan Wake VR Installer'
$script:AwContract = New-PCVRInstallerContract -Id $script:AwId -GameName 'Alan Wake VR' -Acquisition GitHub `
        $steam = Find-SteamGameFolder -AppId '108710' -SteamFolderNames @('Alan Wake') -ProbeExe 'AlanWake.exe' -HubGameId $script:AwId
    $picked = Get-GameFolderInteractive -GameName 'Alan Wake (Steam, GOG or Epic; not Remastered)' `
        [void](Save-HubRememberedGameFolder -GameId $script:AwId -Title 'Alan Wake' -GameDir $picked -ProbeFiles @('AlanWake.exe') -UserSelected)
