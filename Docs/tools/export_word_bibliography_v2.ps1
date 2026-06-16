$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'report_word_common.ps1')
$data = Get-Content (Join-Path $PSScriptRoot 'report_bibliography.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$session = New-WordReportSession
Add-ReportLine -Session $session -Text $data.title -Kind 'title'
foreach ($entry in $data.entries) {
    $line = "{0}`t{1}" -f $entry.n, $entry.text
    Add-ReportLine -Session $session -Text $line -Kind 'bibliography'
}
$out = Join-Path (Split-Path $PSScriptRoot -Parent) 'DriveCare_Report_Bibliography_v2.docx'
Save-ReportDoc -Session $session -Path $out
Write-Host "Bibliography: $out"
