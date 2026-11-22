param([switch]$SkipTests)
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
python scripts/download-assets.py
if ($LASTEXITCODE -ne 0) { throw 'Asset verification failed' }
if (-not $SkipTests) {
    dotnet run --project tests/QuietType.Tests -c Release -- .
    if ($LASTEXITCODE -ne 0) { throw 'Core tests failed' }
}
dotnet publish src/QuietType.Windows -c Release -r win-x64 --self-contained true -p:DebugType=None -p:DebugSymbols=false -o artifacts/app
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
New-Item artifacts/app/assets/model -ItemType Directory -Force | Out-Null
New-Item artifacts/app/assets/dictionary -ItemType Directory -Force | Out-Null
Copy-Item assets/model/model.onnx,assets/model/vocab.txt artifacts/app/assets/model
Copy-Item assets/dictionary/frequency.txt artifacts/app/assets/dictionary
Copy-Item assets/asset-manifest.json artifacts/app/assets
Copy-Item LICENSE,THIRD-PARTY-NOTICES.md artifacts/app
Copy-Item LICENSES artifacts/app -Recurse -Force
Copy-Item docs/GETTING-STARTED.txt artifacts/app
Write-Host 'Self-contained Windows app is ready in artifacts/app'
