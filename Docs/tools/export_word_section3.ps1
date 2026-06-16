# Export section 3 Implementation to Word
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'report_word_common.ps1')

function Test-ScreenHint([string]$Text) {
    return $Text.StartsWith('[')
}

function Test-FigureCaption([string]$Text) {
    return ($Text -match '^\u0420\u0438\u0441\u0443\u043d\u043e\u043a\s+\d+')
}

function Test-ListingRef([string]$Text) {
    return ($Text -match '^\u041b\u0438\u0441\u0442\u0438\u043d\u0433\s+3\.')
}

function Export-Section3Doc {
    param([string]$JsonPath, [string]$OutDoc)
    $data = Get-Content $JsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $session = New-WordReportSession

    Add-ReportLine -Session $session -Text $data.title -Kind 'title'
    Add-ReportLine -Session $session -Text $data.intro -Kind 'body'

    foreach ($section in $data.sections) {
        Add-ReportLine -Session $session -Text $section.heading -Kind 'heading'
        foreach ($para in $section.paragraphs) {
            if (Test-ScreenHint $para) {
                Add-ReportLine -Session $session -Text $para -Kind 'note'
            }
            elseif (Test-FigureCaption $para) {
                Add-ReportLine -Session $session -Text $para -Kind 'caption'
            }
            else {
                Add-ReportLine -Session $session -Text $para -Kind 'body'
            }
        }
    }

    Save-ReportDoc -Session $session -Path $OutDoc
}

$json = Join-Path $PSScriptRoot 'report_section3_content.json'
$out = Join-Path (Split-Path $PSScriptRoot -Parent) 'DriveCare_Report_Section_3_Implementation.docx'
Export-Section3Doc -JsonPath $json -OutDoc $out
Write-Host "Section 3: $out"
