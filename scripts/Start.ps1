$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$key='HKLM:\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64'
$runtime=Get-ItemProperty -LiteralPath $key -ErrorAction SilentlyContinue
if(-not($runtime.Installed -eq 1 -and ($runtime.Major -gt 14 -or ($runtime.Major -eq 14 -and $runtime.Minor -ge 44)))) {
    $installer=Join-Path $root 'prerequisites/vc_redist.x64.exe'
    $signature=Get-AuthenticodeSignature -LiteralPath $installer
    if($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') {throw 'Microsoft runtime signature invalid'}
    $process=Start-Process -FilePath $installer -ArgumentList '/install /quiet /norestart' -Verb RunAs -WindowStyle Hidden -PassThru -Wait
    if($process.ExitCode -notin @(0,1638,3010)) {throw "Runtime installation failed: $($process.ExitCode)"}
}
Start-Process -FilePath (Join-Path $root 'ForeverRoutedSpeech.exe') -WorkingDirectory $root
