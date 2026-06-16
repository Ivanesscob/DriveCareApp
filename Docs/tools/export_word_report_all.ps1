# Export DriveCare report Word documents (technologies, listings, guide)
$ErrorActionPreference = 'Stop'
$Tools = $PSScriptRoot
$Docs = Split-Path $Tools -Parent

. (Join-Path $Tools 'report_word_common.ps1')

function Export-ListingsDoc {
    param([string]$JsonPath, [string]$OutDoc)
    $data = Get-Content $JsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $session = New-WordReportSession

    Add-ReportLine -Session $session -Text $data.title -Kind 'title'
    Add-ReportLine -Session $session -Text $data.intro -Kind 'body'

    foreach ($listing in $data.listings) {
        Add-ReportLine -Session $session -Text $listing.caption -Kind 'listing_caption'
        if ($listing.source) {
            Add-ReportLine -Session $session -Text $listing.source -Kind 'note'
        }
        Add-ReportCodeBlock -Session $session -Lines $listing.code
        Add-ReportLine -Session $session -Text '' -Kind 'body'
    }

    Save-ReportDoc -Session $session -Path $OutDoc
}

function Export-ListingsGuideDoc {
    param([string]$JsonPath, [string]$OutDoc)
    $data = Get-Content $JsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $session = New-WordReportSession

    Add-ReportLine -Session $session -Text $data.title -Kind 'title'
    if ($data.intro) {
        Add-ReportLine -Session $session -Text $data.intro -Kind 'body'
    }

    foreach ($section in $data.sections) {
        Add-ReportLine -Session $session -Text $section.heading -Kind 'heading'
        if ($section.intro) {
            Add-ReportLine -Session $session -Text $section.intro -Kind 'body'
        }
        foreach ($item in $section.items) {
            $lines = @(
                ($item.report_section)
                ('Listing: ' + $item.listing_id)
                ($item.when)
                ($item.sample_text)
            )
            foreach ($line in $lines) {
                Add-ReportLine -Session $session -Text $line -Kind 'body'
            }
            Add-ReportLine -Session $session -Text '' -Kind 'body'
        }
    }

    if ($data.suggested_new_paragraphs) {
        Add-ReportLine -Session $session -Text $data.suggested_new_paragraphs_title -Kind 'heading'
        foreach ($p in $data.suggested_new_paragraphs) {
            Add-ReportLine -Session $session -Text $p.section -Kind 'body'
            Add-ReportLine -Session $session -Text $p.text -Kind 'body'
            Add-ReportLine -Session $session -Text '' -Kind 'body'
        }
    }

    Save-ReportDoc -Session $session -Path $OutDoc
}

$techJson = Join-Path $Tools 'report_technologies_content.json'
$techDoc = Join-Path $Docs 'DriveCare_Report_Section_1_5_1_6.docx'
$listJson = Join-Path $Tools 'report_listings.json'
$listDoc = Join-Path $Docs 'DriveCare_Report_Appendix_Listings.docx'
$guideJson = Join-Path $Tools 'report_listings_guide.json'
$guideDoc = Join-Path $Docs 'DriveCare_Report_Listings_Guide.docx'

Export-ReportFromContentJson -JsonPath $techJson -OutDoc $techDoc
Export-ListingsDoc -JsonPath $listJson -OutDoc $listDoc
Export-ListingsGuideDoc -JsonPath $guideJson -OutDoc $guideDoc

Write-Host "OK: $techDoc"
Write-Host "OK: $listDoc"
Write-Host "OK: $guideDoc"
