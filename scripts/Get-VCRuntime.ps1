$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$folder=Join-Path $root 'installer/redist'
New-Item -ItemType Directory -Force -Path $folder | Out-Null
$file=Join-Path $folder 'vc_redist.x64.exe'
if(-not(Test-Path -LiteralPath $file)) {
    Invoke-WebRequest 'https://download.visualstudio.microsoft.com/download/pr/bd1c8d9d-ba95-4eee-bc6e-df1fcc876373/CC0FF0EB1DC3F5188AE6300FAEF32BF5BEEBA4BDD6E8E445A9184072096B713B/VC_redist.x64.exe' -OutFile $file
}
if((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne 'CC0FF0EB1DC3F5188AE6300FAEF32BF5BEEBA4BDD6E8E445A9184072096B713B') {throw 'Microsoft runtime checksum mismatch'}
$signature=Get-AuthenticodeSignature -LiteralPath $file
if($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') {throw 'Microsoft runtime signature invalid'}
