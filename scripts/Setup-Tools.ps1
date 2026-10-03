$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$tools=Join-Path $root '.tools'
New-Item -ItemType Directory -Force -Path $tools | Out-Null
function FetchChecked([string]$Url,[string]$Path,[string]$Hash,[string]$Algorithm='SHA256') {
    if(-not(Test-Path -LiteralPath $Path)) {Invoke-WebRequest $Url -OutFile $Path}
    if((Get-FileHash -LiteralPath $Path -Algorithm $Algorithm).Hash -ne $Hash) {throw ('Checksum mismatch: '+$Path)}
}
$sdkZip=Join-Path $tools 'dotnet-sdk-10.0.401.zip'
if(-not(Test-Path (Join-Path $tools 'dotnet/dotnet.exe'))) {
    FetchChecked 'https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.401/dotnet-sdk-10.0.401-win-x64.zip' $sdkZip '24b670ad3d923bfcf47df6c3b034152398b42f6dbc388e10d783aee1cfb5e5817d399fc0ae2a12cfa822a55e61d34830ccb15c50ef6efee437ab874bb7c79430' 'SHA512'
    Expand-Archive -LiteralPath $sdkZip -DestinationPath (Join-Path $tools 'dotnet') -Force
}
$compilerZip=Join-Path $tools 'llvm-mingw.zip'
if(-not(Test-Path (Join-Path $tools 'llvm-mingw-20260922-ucrt-x86_64/bin/clang.exe'))) {
    FetchChecked 'https://github.com/mstorsjo/llvm-mingw/releases/download/20260922/llvm-mingw-20260922-ucrt-x86_64.zip' $compilerZip 'e3ad77d117a4bea19a7a3b333341824d79a5a371004a10e25b8504e7b3047666'
    Expand-Archive -LiteralPath $compilerZip -DestinationPath $tools -Force
}
Write-Output 'Local .NET SDK and LLVM-MinGW ready. Install CMake 3.31.6 and Python 3.12 with lupa 2.6 for native builds and Lua mock tests; see README.'
