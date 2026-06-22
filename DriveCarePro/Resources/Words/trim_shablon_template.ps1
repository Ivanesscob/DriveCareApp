# Removes empty placeholder rows and the customer spare-parts block from shablon_tokens.docx
$ErrorActionPreference = 'Stop'

$docx = Join-Path $PSScriptRoot 'shablon_tokens.docx'
if (-not (Test-Path $docx)) { throw "Not found: $docx" }

$tmp = Join-Path $env:TEMP "trim_zakaz_$(Get-Random)"
New-Item -ItemType Directory -Path "$tmp\doc" -Force | Out-Null
Copy-Item $docx "$tmp\z.zip" -Force
Expand-Archive "$tmp\z.zip" "$tmp\doc" -Force

$xmlPath = "$tmp\doc\word\document.xml"
$utf8 = New-Object System.Text.UTF8Encoding $false
$xmlText = [IO.File]::ReadAllText($xmlPath, $utf8)
[xml]$doc = $xmlText
$ns = New-Object System.Xml.XmlNamespaceManager($doc.NameTable)
$ns.AddNamespace('w', 'http://schemas.openxmlformats.org/wordprocessingml/2006/main')

function Get-RowText([System.Xml.XmlElement]$tr) {
    $parts = $tr.SelectNodes('.//w:t', $ns) | ForEach-Object { $_.InnerText }
    return ($parts -join '')
}

function Test-PlaceholderOnly([string]$text) {
    if ([string]::IsNullOrWhiteSpace($text)) { return $true }
    foreach ($ch in $text.ToCharArray()) {
        if ([char]::IsWhiteSpace($ch)) { continue }
        if ('×xX-_—.:|'.Contains($ch)) { continue }
        return $false
    }
    return $true
}

$rows = @($doc.SelectNodes('//w:tr', $ns))
$start = -1
$end = -1
for ($i = 0; $i -lt $rows.Count; $i++) {
    $t = Get-RowText $rows[$i]
    if ($t.Contains('{{CUSTOMER_PARTS')) {
        $start = $i
        while ($start -gt 0) {
            $prev = Get-RowText $rows[$start - 1]
            if ($prev.Contains('{{PARTS_TOTAL}}') -or $prev.Contains('{{LABOR_COST_SUM}}')) { break }
            $start--
        }
        break
    }
}
if ($start -ge 0) {
    for ($i = $start; $i -lt $rows.Count; $i++) {
        $t = Get-RowText $rows[$i]
        if ($t.Contains('{{LABOR_COST_SUM}}')) {
            $end = $i
            break
        }
    }
    $removeUntil = if ($end -ge 0) { $end } else { $rows.Count }
    for ($i = $start; $i -lt $removeUntil; $i++) {
        [void]$rows[$i].ParentNode.RemoveChild($rows[$i])
    }
}

$rows = @($doc.SelectNodes('//w:tr', $ns))
foreach ($tr in @($rows)) {
    $t = (Get-RowText $tr).Trim()
    if ([string]::IsNullOrEmpty($t)) {
        [void]$tr.ParentNode.RemoveChild($tr)
        continue
    }
    if ($t.Contains('{{')) { continue }
    if (Test-PlaceholderOnly $t) {
        [void]$tr.ParentNode.RemoveChild($tr)
    }
}

[IO.File]::WriteAllText($xmlPath, $doc.OuterXml, $utf8)
Remove-Item $docx -Force
Compress-Archive -Path "$tmp\doc\*" -DestinationPath "$tmp\out.zip" -Force
Move-Item "$tmp\out.zip" $docx -Force
Remove-Item $tmp -Recurse -Force
Write-Host "Trimmed: $docx"
