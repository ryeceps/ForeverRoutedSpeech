param([string]$Dotnet='', [string]$CMake='cmake', [string]$ToolchainDirectory='', [switch]$SkipNative, [switch]$SkipArchive)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(-not $Dotnet) {$Dotnet=Join-Path $root '.tools/dotnet/dotnet.exe'}
if(-not $ToolchainDirectory) {$ToolchainDirectory=Join-Path $root '.tools/llvm-mingw-20260922-ucrt-x86_64'}
$Dotnet=(Resolve-Path -LiteralPath $Dotnet).Path
$runtime=Join-Path $ToolchainDirectory 'bin'
function Checked([string]$File,[string[]]$Arguments) {& $File @Arguments; if($LASTEXITCODE -ne 0) {throw "Command failed: $File"}}
Push-Location $root
try {
    if(-not $SkipNative) {
        $env:Path=$runtime+';'+$env:Path
        Checked $CMake @('-S','native','-B','native/build','-G','MinGW Makefiles','-DCMAKE_BUILD_TYPE=Release',('-DCMAKE_C_COMPILER='+ (Join-Path $runtime 'clang.exe')),('-DCMAKE_CXX_COMPILER='+ (Join-Path $runtime 'clang++.exe')),('-DCMAKE_MAKE_PROGRAM='+ (Join-Path $runtime 'mingw32-make.exe')))
        Checked $CMake @('--build','native/build','--parallel','4')
    }
    & (Join-Path $PSScriptRoot 'Get-Turbo.ps1')
    & (Join-Path $PSScriptRoot 'Get-VCRuntime.ps1')
    Checked $Dotnet @('restore','app/Gui/SpeakForever.Gui.csproj','--locked-mode')
    Checked $Dotnet @('run','--project','tests/SpeakForever.Core.Tests','-c','Release')
    Checked $Dotnet @('run','--project','tests/VoiceRouter.Tests','-c','Release')
    $package=Join-Path $root 'dist/ForeverRoutedSpeech'
    Checked $Dotnet @('publish','app/Gui/SpeakForever.Gui.csproj','-c','Release','--self-contained','true','--no-restore','-o',$package)
    New-Item -ItemType Directory -Force -Path (Join-Path $package 'models'),(Join-Path $package 'licenses') | Out-Null
    Copy-Item -LiteralPath (Join-Path $root 'native/build/voice_router_native.dll') -Destination $package
    Get-ChildItem $runtime -Filter '*.dll' | Where-Object Name -Match '^(libwinpthread|libc\+\+|libunwind)' | Copy-Item -Destination $package
    Copy-Item -LiteralPath (Join-Path $root 'models/router.bin'),(Join-Path $root 'models/router-policy.json'),(Join-Path $root 'models/ggml-large-v3-turbo-q5_0.bin') -Destination (Join-Path $package 'models')
    Copy-Item -LiteralPath (Join-Path $root 'LICENSE'),(Join-Path $root 'README.md') -Destination $package
    Copy-Item -LiteralPath (Join-Path $root 'addon'),(Join-Path $root 'docs') -Destination $package -Recurse -Force
    foreach($obsolete in @('Bindings.xml','VoiceRouter.lua','Preview.lua','VoiceRouter.toc')) {
        $obsoletePath=Join-Path $package ('addon/VoiceRouter/'+$obsolete)
        if(Test-Path -LiteralPath $obsoletePath) {Remove-Item -LiteralPath $obsoletePath}
    }
    New-Item -ItemType Directory -Force -Path (Join-Path $package 'scripts') | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Install-Addon.ps1') -Destination (Join-Path $package 'scripts')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Start.ps1') -Destination (Join-Path $package 'scripts')
    Copy-Item -LiteralPath (Join-Path $root 'Start.cmd') -Destination $package
    New-Item -ItemType Directory -Force -Path (Join-Path $package 'prerequisites') | Out-Null
    Copy-Item -LiteralPath (Join-Path $root 'installer/redist/vc_redist.x64.exe') -Destination (Join-Path $package 'prerequisites')
    Copy-Item -LiteralPath (Join-Path $root 'native/build/_deps/whisper-src/LICENSE') -Destination (Join-Path $package 'licenses/whisper.cpp.txt')
    Copy-Item -LiteralPath (Join-Path $root 'native/build/_deps/fasttext-src/LICENSE') -Destination (Join-Path $package 'licenses/fastText.txt')
    Copy-Item -LiteralPath (Join-Path $ToolchainDirectory 'LICENSE.TXT') -Destination (Join-Path $package 'licenses/LLVM-MinGW.txt')
    Checked $Dotnet @('run','--project','tests/Routing.Smoke','-c','Release','--',$package)
    if(-not $SkipArchive) {
        $archive=Join-Path $root 'dist/ForeverRoutedSpeech-windows-x64.zip'
        Compress-Archive -Path (Join-Path $package '*') -DestinationPath $archive -Force
        (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() | Set-Content ($archive+'.sha256')
        Write-Output $archive
    }
} finally {Pop-Location}
