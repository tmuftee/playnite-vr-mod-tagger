# Builds the plugin and packs it into a .pext (a zip that Playnite installs by opening or drag-and-drop).
$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot 'src\VrModTagger\VrModTagger.csproj'
$output = Join-Path $PSScriptRoot 'src\VrModTagger\bin\Release'
$dist = Join-Path $PSScriptRoot 'dist'

dotnet build $project -c Release -nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

$manifest = Get-Content (Join-Path $output 'extension.yaml')
$id = ($manifest | Where-Object { $_ -match '^Id:' }) -replace '^Id:\s*', ''
$version = (($manifest | Where-Object { $_ -match '^Version:' }) -replace '^Version:\s*', '') -replace '\.', '_'

New-Item -ItemType Directory -Force $dist | Out-Null
$zip = Join-Path $dist "$id`_$version.zip"
$pext = [System.IO.Path]::ChangeExtension($zip, '.pext')
Remove-Item $zip, $pext -ErrorAction SilentlyContinue

$files = Get-ChildItem $output -File | Where-Object { $_.Extension -ne '.pdb' }
Compress-Archive -Path $files.FullName -DestinationPath $zip
Move-Item $zip $pext
Write-Host "Packed $pext"
