param([Parameter(Mandatory)][string]$AddOnsDirectory,[Parameter(Mandatory)][ValidateRange(1,9999999)][int]$Interface)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (-not (Test-Path -LiteralPath $AddOnsDirectory -PathType Container)) { throw 'Choose the actual Forever Interface/AddOns directory.' }
$target = Join-Path $AddOnsDirectory 'VoiceRouter'
New-Item -ItemType Directory -Force -Path $target | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'addon/VoiceRouter/VoiceRouter.lua') -Destination $target
Copy-Item -LiteralPath (Join-Path $root 'addon/VoiceRouter/Preview.lua') -Destination $target
Copy-Item -LiteralPath (Join-Path $root 'addon/VoiceRouter/Bindings.xml') -Destination $target
(Get-Content -Raw (Join-Path $root 'addon/VoiceRouter/VoiceRouter.toc.in')).Replace('@INTERFACE@', [string]$Interface) | Set-Content -Encoding UTF8 (Join-Path $target 'VoiceRouter.toc')
Write-Output 'VoiceRouter installed. Reload the game UI to load it. The companion automatically detects the visible strip, current groups, and joined channels with Classic chat defaults enabled.'
