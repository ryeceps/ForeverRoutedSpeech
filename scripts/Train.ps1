param([string]$Python='python',[string]$FastText='',[string]$OutputDirectory='')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(-not $FastText) { $FastText=Join-Path $root 'native/build/fasttext.exe' }
if(-not $OutputDirectory) { $OutputDirectory=Join-Path $root 'models' }
& $Python (Join-Path $root 'training/train.py') --fasttext $FastText --output $OutputDirectory
if($LASTEXITCODE -ne 0) {throw 'Training or evaluation failed.'}
