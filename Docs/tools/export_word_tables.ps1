# Export table structures to Word (.docx) with native Word tables
$ErrorActionPreference = 'Stop'
$Root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$Docs = Join-Path $Root 'Docs'
$OutDoc = Join-Path $Docs 'DriveCare_Database_Table_Structures.docx'

& (Join-Path $PSScriptRoot 'generate_db_docs.ps1') | Out-Null

$RuJson = Join-Path $PSScriptRoot 'table_desc_ru.json'
$RelJson = Join-Path $PSScriptRoot 'table_relationships_ru.json'
$Edmx = Join-Path $Root 'DriveCareCore\Data\BD\Model1.edmx'
$ru = Get-Content $RuJson -Raw -Encoding UTF8 | ConvertFrom-Json
$rel = Get-Content $RelJson -Raw -Encoding UTF8 | ConvertFrom-Json

[xml]$xml = Get-Content $Edmx -Encoding UTF8
$schema = $xml.edmx.Runtime.StorageModels.Schema

function Get-SqlType($p) {
    $t = $p.Type
    if ($t -eq 'nvarchar') {
        if ($p.MaxLength -eq 'Max') { return 'nvarchar(max)' }
        return "nvarchar($($p.MaxLength))"
    }
    if ($t -eq 'decimal') { return "decimal($($p.Precision),$($p.Scale))" }
    if ($t -eq 'datetime2') {
        if ($p.Precision) { return "datetime2($($p.Precision))" }
        return 'datetime2'
    }
    if ($t -eq 'time') {
        if ($p.Precision) { return "time($($p.Precision))" }
        return 'time'
    }
    return $t
}

$entities = @{}
$fks = @{}
foreach ($et in $schema.EntityType) {
    $name = $et.Name
    if ($name -eq 'sysdiagrams') { continue }
    $pk = @($et.Key.PropertyRef | ForEach-Object { $_.Name })
    $cols = @()
    foreach ($p in $et.Property) {
        $cols += [pscustomobject]@{ name = $p.Name; type = (Get-SqlType $p); prop = $p }
    }
    $entities[$name] = @{ columns = $cols; pk = $pk }
}
foreach ($a in $schema.Association) {
    $rc = $a.ReferentialConstraint
    if (-not $rc) { continue }
    $depRole = $rc.Dependent.Role
    $depCol = $rc.Dependent.PropertyRef.Name
    $princRole = $rc.Principal.Role
    $depEnd = $a.End | Where-Object { $_.Role -eq $depRole }
    $princEnd = $a.End | Where-Object { $_.Role -eq $princRole }
    $depTable = $depEnd.Type -replace '^Self\.', ''
    $princTable = $princEnd.Type -replace '^Self\.', ''
    if (-not $fks.ContainsKey($depTable)) { $fks[$depTable] = @{} }
    $fks[$depTable][$depCol] = $princTable
}
$logical = Get-Content (Join-Path $PSScriptRoot 'logical_foreign_keys.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($tp in $logical.PSObject.Properties) {
    $tname = $tp.Name
    if (-not $fks.ContainsKey($tname)) { $fks[$tname] = @{} }
    foreach ($pair in $tp.Value) { $fks[$tname][$pair[0]] = $pair[1] }
}
foreach ($prop in $ru.extra_tables.PSObject.Properties) {
    $tname = $prop.Name
    $rows = $prop.Value
    $cols = @()
    foreach ($row in $rows) {
        $cols += [pscustomobject]@{ name = $row[0]; type = $row[1]; constraints = $row[2]; desc = $row[3] }
    }
    $entities[$tname] = @{ columns = $cols; pk = @('RowId') }
}

function Get-FieldDesc($name) {
    $prop = $ru.fields.PSObject.Properties[$name]
    if ($prop) { return $prop.Value }
    if ($name -match 'Id$' -and $name -ne 'RowId') {
        return ($ru.ref_prefix + ' ' + $name.Substring(0, $name.Length - 2))
    }
    return $name
}
function Get-Constraints($col, $pk, $fkMap) {
    $parts = @()
    if ($pk -contains $col.name) {
        $parts += 'PK'
        if ($col.name -eq 'RowId') { $parts += 'DEFAULT NEWID()' }
    }
    if ($col.prop.Nullable -eq 'false') { $parts += 'NOT NULL' } else { $parts += 'NULL' }
    if ($col.prop.StoreGeneratedPattern -eq 'Computed') { $parts += 'COMPUTED' }
    if ($col.prop.StoreGeneratedPattern -eq 'Identity') { $parts += 'IDENTITY' }
    if ($fkMap.ContainsKey($col.name)) { $parts += ($ru.fk_arrow + ' ' + $fkMap[$col.name]) }
    return ($parts -join ', ')
}

try {
    $word = New-Object -ComObject Word.Application
} catch {
    Write-Error 'Microsoft Word not found. Install Word or open DriveCare_Database_Table_Structures.md in Word manually.'
}
$word.Visible = $false
$doc = $word.Documents.Add()
$sel = $word.Selection

function WLine([string]$text, [switch]$Heading, [switch]$SubHeading) {
    if ($Heading) { $sel.Font.Bold = $true; $sel.Font.Size = 16 }
    elseif ($SubHeading) { $sel.Font.Bold = $true; $sel.Font.Size = 12 }
    else { $sel.Font.Bold = $false; $sel.Font.Size = 11 }
    $sel.TypeText($text)
    $sel.TypeParagraph()
}

WLine $ru.title -Heading
WLine $ru.intro
WLine $ru.fk_report_note
WLine ''

$priority = @('Users','Roles','Employees','Workshops','Tasks','Cars')
$order = $priority + ($entities.Keys | Where-Object { $_ -notin $priority -and $_ -ne 'sysdiagrams' } | Sort-Object)
$num = 1

foreach ($tname in $order) {
    $td = $ru.tables.PSObject.Properties[$tname]
    $about = if ($td) { $td.Value } else { $ru.default_about }
    WLine ("$($ru.table_word) $tname $($ru.stores_prefix) $about $($ru.structure_in) 2.$num.")
    WLine -SubHeading ("$($ru.table_word) 2.$num $($ru.table_sep) $($ru.table_caption) $tname")

    $fkMap = if ($fks.ContainsKey($tname)) { $fks[$tname] } else { @{} }
    $rowCount = $entities[$tname].columns.Count + 1
    $table = $doc.Tables.Add($sel.Range, $rowCount, 4)
    $table.Borders.Enable = $true
    $table.Cell(1,1).Range.Text = $ru.col_field
    $table.Cell(1,2).Range.Text = $ru.col_type
    $table.Cell(1,3).Range.Text = $ru.col_constraints
    $table.Cell(1,4).Range.Text = $ru.col_desc
    $r = 2
    foreach ($c in $entities[$tname].columns) {
        if ($c.constraints) { $con = $c.constraints; $d = $c.desc }
        else { $con = Get-Constraints $c $entities[$tname].pk $fkMap; $d = Get-FieldDesc $c.name }
        $table.Cell($r,1).Range.Text = $c.name
        $table.Cell($r,2).Range.Text = $c.type
        $table.Cell($r,3).Range.Text = $con
        $table.Cell($r,4).Range.Text = $d
        $r++
    }
    $sel.SetRange($doc.Content.End - 1, $doc.Content.End - 1)
    $sel.TypeParagraph()

    $linkProp = $rel.PSObject.Properties[$tname]
    if ($linkProp) {
        WLine ("$($ru.links_label): $($linkProp.Value)")
    }
    WLine ''
    $num++
}

if (Test-Path $OutDoc) { Remove-Item $OutDoc -Force }
$savePath = [string]$OutDoc
$null = $doc.SaveAs2($savePath, 16)
$doc.Close()
$word.Quit()
[System.Runtime.Interopservices.Marshal]::ReleaseComObject($word) | Out-Null

Write-Host "Word file: $OutDoc"
