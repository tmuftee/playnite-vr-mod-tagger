# Excerpt (parsed lines only) of the PCVR Mods Installer Hub script of the same name. MIT License, Copyright (c) 2026 Mr-N!ce.
$script:Contract=New-PCVRInstallerContract -Id $script:Id -GameName 'Fallout 3 VR' -Acquisition GitHub -AntivirusNotice -ReleasePageUrl $script:Page -RequiredInstalledFileGroups $script:Proof
    foreach($app in @('22370','22300')){try{$found=Find-SteamGameFolder -AppId $app -SteamFolderNames @('Fallout 3 goty','Fallout 3') -ProbeExe 'Fallout3.exe' -HubGameId $script:Id;if($found){$c+=$found}}catch{}}
    $p=Get-GameFolderInteractive -GameName 'Fallout 3 (Steam, version 1.7.0.4)' -ProbeFile 'Fallout3.exe' -ManualUrl 'https://store.steampowered.com/app/22370/'
            if(-not(Save-HubRememberedGameFolder -GameId $script:Id -Title 'Fallout 3 VR' -GameDir $game -ProbeFiles @('Fallout3.exe','Data\Fallout3.esm') -StateName 'base_game_path')){throw 'The Steam game location could not be saved.'}
