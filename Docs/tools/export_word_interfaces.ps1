# Export section 2.3 "Interface design" to Word with report formatting
$ErrorActionPreference = 'Stop'
$Root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$Docs = Join-Path $Root 'Docs'
$ContentJson = Join-Path $PSScriptRoot 'interface_report_content.json'
$OutDoc = Join-Path $Docs 'DriveCare_Report_Interface_Design.docx'

$content = Get-Content $ContentJson -Raw -Encoding UTF8 | ConvertFrom-Json

try {
    $word = New-Object -ComObject Word.Application
} catch {
    Write-Error 'Microsoft Word not found. Install Word to generate the document.'
}

$word.Visible = $false
$doc = $word.Documents.Add()
$sel = $word.Selection

$indentCm = 1.25
$indentPt = $word.CentimetersToPoints($indentCm)
$fontName = 'Times New Roman'
$fontSize = 14

function Format-Paragraph {
    param(
        [Parameter(Mandatory = $true)]$Paragraph,
        [ValidateSet('body', 'caption', 'heading', 'title')]
        [string]$Kind = 'body'
    )
    $pf = $Paragraph.Format
    $pf.SpaceBefore = 0
    $pf.SpaceAfter = 0
    $pf.LineSpacingRule = 1
    $pf.LineSpacing = 18
    $Paragraph.Range.Font.Name = $fontName
    $Paragraph.Range.Font.Size = $fontSize

    switch ($Kind) {
        'title' {
            $Paragraph.Range.Font.Bold = $true
            $pf.Alignment = 0
            $pf.FirstLineIndent = 0
            $pf.LeftIndent = 0
        }
        'heading' {
            $Paragraph.Range.Font.Bold = $true
            $pf.Alignment = 0
            $pf.FirstLineIndent = 0
            $pf.LeftIndent = 0
        }
        'caption' {
            $Paragraph.Range.Font.Bold = $false
            $pf.Alignment = 1
            $pf.FirstLineIndent = 0
            $pf.LeftIndent = 0
        }
        default {
            $Paragraph.Range.Font.Bold = $false
            $pf.Alignment = 3
            $pf.FirstLineIndent = $indentPt
            $pf.LeftIndent = 0
        }
    }
}

function Add-TextLine {
    param(
        [string]$Text,
        [ValidateSet('body', 'caption', 'heading', 'title')]
        [string]$Kind = 'body'
    )
    if ([string]::IsNullOrWhiteSpace($Text)) {
        $sel.TypeParagraph()
        return
    }
    $sel.TypeText($Text)
    $sel.TypeParagraph()
    $para = $doc.Paragraphs.Item($doc.Paragraphs.Count)
    Format-Paragraph -Paragraph $para -Kind $Kind
}

function Test-Caption([string]$Text) {
    return ($Text -match '^Рисунок\s+\d+\s+[–-]')
}

Add-TextLine -Text $content.title -Kind 'title'
Add-TextLine -Text $content.intro -Kind 'body'

foreach ($section in $content.sections) {
    Add-TextLine -Text $section.heading -Kind 'heading'
    foreach ($para in $section.paragraphs) {
        if (Test-Caption $para) {
            Add-TextLine -Text $para -Kind 'caption'
        } else {
            Add-TextLine -Text $para -Kind 'body'
        }
    }
}

if (Test-Path $OutDoc) { Remove-Item $OutDoc -Force }
$savePath = [string]$OutDoc
$null = $doc.SaveAs2($savePath, 16)
$doc.Close()
$word.Quit()
[System.Runtime.Interopservices.Marshal]::ReleaseComObject($word) | Out-Null

Write-Host "Word file: $OutDoc"
