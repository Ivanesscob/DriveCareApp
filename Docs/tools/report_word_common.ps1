# Shared Word formatting helpers for DriveCare report exports
function New-WordReportSession {
    try {
        $word = New-Object -ComObject Word.Application
    } catch {
        Write-Error 'Microsoft Word not found. Install Word to generate documents.'
    }
    $word.Visible = $false
    $doc = $word.Documents.Add()
    $sel = $word.Selection
    return [pscustomobject]@{
        Word = $word
        Doc = $doc
        Sel = $sel
        IndentPt = $word.CentimetersToPoints(1.25)
        FontName = 'Times New Roman'
        FontSize = 14
    }
}

function Format-ReportParagraph {
    param(
        $Paragraph,
        [ValidateSet('body', 'caption', 'heading', 'title', 'listing_caption', 'code', 'note', 'bibliography')]
        [string]$Kind = 'body',
        $Session
    )
    $pf = $Paragraph.Format
    $pf.SpaceBefore = 0
    $pf.SpaceAfter = 0
    $Paragraph.Range.Font.Name = $Session.FontName

    switch ($Kind) {
        'title' {
            $Paragraph.Range.Font.Size = $Session.FontSize
            $Paragraph.Range.Font.Bold = $true
            $pf.Alignment = 0
            $pf.FirstLineIndent = 0
            $pf.LineSpacingRule = 1
            $pf.LineSpacing = 18
        }
        'heading' {
            $Paragraph.Range.Font.Size = $Session.FontSize
            $Paragraph.Range.Font.Bold = $true
            $pf.Alignment = 0
            $pf.FirstLineIndent = 0
            $pf.LineSpacingRule = 1
            $pf.LineSpacing = 18
        }
        'caption' {
            $Paragraph.Range.Font.Size = $Session.FontSize
            $Paragraph.Range.Font.Bold = $false
            $pf.Alignment = 1
            $pf.FirstLineIndent = 0
            $pf.LineSpacingRule = 1
            $pf.LineSpacing = 18
        }
        'listing_caption' {
            $Paragraph.Range.Font.Size = $Session.FontSize
            $Paragraph.Range.Font.Bold = $false
            $pf.Alignment = 1
            $pf.FirstLineIndent = 0
            $pf.LineSpacingRule = 0
            $pf.LineSpacing = 12
        }
        'code' {
            $Paragraph.Range.Font.Name = 'Courier New'
            $Paragraph.Range.Font.Size = 10
            $Paragraph.Range.Font.Bold = $false
            $pf.Alignment = 0
            $pf.FirstLineIndent = 0
            $pf.LeftIndent = $Session.IndentPt
            $pf.LineSpacingRule = 0
            $pf.LineSpacing = 12
        }
        'note' {
            $Paragraph.Range.Font.Size = 12
            $Paragraph.Range.Font.Bold = $false
            $Paragraph.Range.Font.Italic = $true
            $pf.Alignment = 0
            $pf.FirstLineIndent = $Session.IndentPt
            $pf.LineSpacingRule = 1
            $pf.LineSpacing = 18
        }
        'bibliography' {
            $Paragraph.Range.Font.Size = $Session.FontSize
            $Paragraph.Range.Font.Bold = $false
            $pf.Alignment = 0
            $pf.FirstLineIndent = 0
            $pf.LeftIndent = 0
            $pf.LineSpacingRule = 1
            $pf.LineSpacing = 18
        }
        default {
            $Paragraph.Range.Font.Size = $Session.FontSize
            $Paragraph.Range.Font.Bold = $false
            $pf.Alignment = 3
            $pf.FirstLineIndent = $Session.IndentPt
            $pf.LineSpacingRule = 1
            $pf.LineSpacing = 18
        }
    }
}

function Add-ReportLine {
    param(
        $Session,
        [string]$Text,
        [ValidateSet('body', 'caption', 'heading', 'title', 'listing_caption', 'code', 'note', 'bibliography')]
        [string]$Kind = 'body'
    )
    if ([string]::IsNullOrWhiteSpace($Text)) {
        $Session.Sel.TypeParagraph()
        return
    }
    $Session.Sel.TypeText($Text)
    $Session.Sel.TypeParagraph()
    $para = $Session.Doc.Paragraphs.Item($Session.Doc.Paragraphs.Count)
    Format-ReportParagraph -Paragraph $para -Kind $Kind -Session $Session
}

function Add-ReportCodeBlock {
    param(
        $Session,
        [string[]]$Lines
    )
    foreach ($line in $Lines) {
        Add-ReportLine -Session $Session -Text $line -Kind 'code'
    }
}

function Save-ReportDoc {
    param($Session, [string]$Path)
    if (Test-Path $Path) { Remove-Item $Path -Force }
    $null = $Session.Doc.SaveAs2([string]$Path, 16)
    $Session.Doc.Close()
    $Session.Word.Quit()
    [System.Runtime.Interopservices.Marshal]::ReleaseComObject($Session.Word) | Out-Null
}

function Test-FigureCaption([string]$Text) {
    return ($Text -match '^Рисунок\s+\d+\s+[–-]')
}

function Export-ReportFromContentJson {
    param(
        [string]$JsonPath,
        [string]$OutDoc
    )
    $content = Get-Content $JsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $session = New-WordReportSession

    Add-ReportLine -Session $session -Text $content.title -Kind 'title'
    if ($content.intro) {
        Add-ReportLine -Session $session -Text $content.intro -Kind 'body'
    }

    foreach ($section in $content.sections) {
        if ($section.heading) {
            Add-ReportLine -Session $session -Text $section.heading -Kind 'heading'
        }
        foreach ($para in $section.paragraphs) {
            if (Test-FigureCaption $para) {
                Add-ReportLine -Session $session -Text $para -Kind 'caption'
            } else {
                Add-ReportLine -Session $session -Text $para -Kind 'body'
            }
        }
    }

    Save-ReportDoc -Session $session -Path $OutDoc
}
