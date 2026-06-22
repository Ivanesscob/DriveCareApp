# Packs shablon_modern.document.xml into shablon_tokens.docx (Pro + DriveCare).
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$baseDocx = Join-Path $dir 'shablon.docx'
$documentXml = Join-Path $dir 'shablon_modern.document.xml'
$destPro = Join-Path $dir 'shablon_tokens.docx'
$destUser = Join-Path (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $dir))) 'DriveCare\Resources\Words\shablon_tokens.docx'

if (-not (Test-Path $baseDocx)) { throw "Not found: $baseDocx" }
if (-not (Test-Path $documentXml)) { throw "Not found: $documentXml" }

function Pack-Docx([string]$OutputPath) {
    $tmp = Join-Path $env:TEMP "pack_zakaz_$(Get-Random)"
    $docDir = Join-Path $tmp 'doc'
    if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
    New-Item -ItemType Directory -Path $docDir -Force | Out-Null
    [System.IO.Compression.ZipFile]::ExtractToDirectory($baseDocx, $docDir)
    Copy-Item $documentXml (Join-Path $docDir 'word\document.xml') -Force
    if (Test-Path $OutputPath) { Remove-Item $OutputPath -Force }
    $outDir = Split-Path $OutputPath -Parent
    if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }
    [System.IO.Compression.ZipFile]::CreateFromDirectory($docDir, $OutputPath, [System.IO.Compression.CompressionLevel]::Optimal, $false)
    Remove-Item $tmp -Recurse -Force
}

Pack-Docx $destPro
if (Test-Path (Split-Path $destUser -Parent)) { Pack-Docx $destUser }
Write-Host "Packed modern template:"
Write-Host "  $destPro"
if (Test-Path $destUser) { Write-Host "  $destUser" }
