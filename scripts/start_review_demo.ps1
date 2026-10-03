$ErrorActionPreference = 'Stop'
# Windows user-scoped environment keeps the synthetic credentials outside Git.
function DemoSecret([string]$Name, [bool]$Base64 = $false) {
    $value = [Environment]::GetEnvironmentVariable($Name, 'User')
    if ([string]::IsNullOrWhiteSpace($value)) {
        $bytes = New-Object byte[] 32
        $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
        try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
        if ($Base64) { $value = [Convert]::ToBase64String($bytes) }
        else { $value = -join ($bytes | ForEach-Object { $_.ToString('x2') }) }
        [Environment]::SetEnvironmentVariable($Name, $value, 'User')
    }
    return $value
}
if ($env:OS -ne 'Windows_NT') { throw 'Dieses Startskript verwendet die Windows-Benutzerumgebung. Siehe FREIGABE-DEMO.de.md fuer Linux.' }
$bundleRoot = Split-Path $PSScriptRoot -Parent
if (Test-Path (Join-Path $PSScriptRoot 'compose.review-demo.yml')) { $bundleRoot = $PSScriptRoot }
$env:NORMACASE_REVIEW_DB_PASSWORD = DemoSecret 'NORMACASE_REVIEW_DB_PASSWORD'
$env:SyntheticReview__Credential = DemoSecret 'NORMACASE_REVIEW_DEMO_CREDENTIAL' $true
$env:SyntheticReview__Enabled = 'true'
$env:NORMACASE_REVIEW_DEMO_CONNECTION = "Host=127.0.0.1;Port=54329;Database=normacase_review_demo;Username=normacase_demo;Password=$env:NORMACASE_REVIEW_DB_PASSWORD"
docker compose -f (Join-Path $bundleRoot 'compose.review-demo.yml') up -d --wait
if ($LASTEXITCODE -ne 0) { throw 'Die lokale Demo-Datenbank konnte nicht gestartet werden.' }
Write-Host 'NormaCase: http://localhost:5080 – synthetische lokale Freigabe-Demo'
Write-Host 'Diesen lokalen Zugangsschluessel im Feld Demo-Zugang eingeben:'
Write-Host $env:SyntheticReview__Credential
Write-Host 'Nur synthetische Daten. Beenden: Strg+C. Datenbankhistorie bleibt erhalten.'
$nativeApi = Join-Path $bundleRoot 'api/NormaCase.Api.exe'
if (Test-Path $nativeApi) { & $nativeApi }
else { dotnet run --project (Join-Path $bundleRoot 'src/NormaCase.Api') --configuration Release }
