$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$folder=Join-Path $root 'models'
New-Item -ItemType Directory -Force -Path $folder | Out-Null
$target=Join-Path $folder 'ggml-large-v3-turbo-q5_0.bin'
$expected='394221709cd5ad1f40c46e6031ca61bce88931e6e088c188294c6d5a55ffa7e2'
if(-not(Test-Path -LiteralPath $target)) {
    $temporary=$target+'.download'
    Invoke-WebRequest 'https://huggingface.co/ggerganov/whisper.cpp/resolve/5359861c739e955e79d9a303bcbc70fb988958b1/ggml-large-v3-turbo-q5_0.bin' -OutFile $temporary
    if((Get-FileHash -LiteralPath $temporary -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected) {throw 'Turbo checksum mismatch'}
    Move-Item -LiteralPath $temporary -Destination $target
}
if((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected) {throw 'Turbo checksum mismatch'}
Write-Output 'Pinned Turbo model verified.'
