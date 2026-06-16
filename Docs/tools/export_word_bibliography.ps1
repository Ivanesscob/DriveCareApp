# Export bibliography list to Word
$ErrorActionPreference = 'Stop'
$Tools = $PSScriptRoot
$Docs = Split-Path $Tools -Parent
$JsonPath = Join-Path $Tools 'report_bibliography.json'
$OutDoc = Join-Path $Docs 'DriveCare_Report_Bibliography.docx'

. (Join-Path $Tools 'report_word_common.ps1')

$data = Get-Content $JsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
$session = New-WordReportSession

Add-ReportLine -Session $session -Text $data.title -Kind 'title'

foreach ($entry in $data.entries) {
    $line = "{0}`t{1}" -f $entry.n, $entry.text
    Add-ReportLine -Session $session -Text $line -Kind 'bibliography'
}

Save-ReportDoc -Session $session -Path $OutDoc
Write-Host "Bibliography: $OutDoc"
