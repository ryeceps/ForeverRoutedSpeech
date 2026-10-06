param([Parameter(Mandatory)][string]$AddOnsDirectory,[Parameter(Mandatory)][ValidateRange(1,9999999)][int]$Interface)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (-not (Test-Path -LiteralPath $AddOnsDirectory -PathType Container)) { throw 'Choose the actual Forever Interface/AddOns directory.' }
$target = Join-Path $AddOnsDirectory 'VoiceRouter'
New-Item -ItemType Directory -Force -Path $target | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'addon/VoiceRouter/LocalRouter.lua'),(Join-Path $root 'addon/VoiceRouter/Inbox.lua') -Destination $target
foreach($obsolete in @('Bindings.xml','VoiceRouter.lua','Preview.lua')) {
    $obsoletePath=Join-Path $target $obsolete
    if(Test-Path -LiteralPath $obsoletePath) { Remove-Item -LiteralPath $obsoletePath }
}
(Get-Content -Raw (Join-Path $root 'addon/VoiceRouter/VoiceRouter.toc.in')).Replace('@INTERFACE@', [string]$Interface) | Set-Content -Encoding UTF8 (Join-Path $target 'VoiceRouter.toc')
Write-Output 'ForeverRoutedSpeech addon installed. Reload WoW once. The addon resolves live context locally; no strip, capture setup or channel configuration.'
