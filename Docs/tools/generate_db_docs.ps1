# ASCII-only generator - Russian text from table_desc_ru.json (UTF-8)
$ErrorActionPreference = 'Stop'
$Root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$Edmx = Join-Path $Root "DriveCareCore\Data\BD\Model1.edmx"
$Docs = Join-Path $Root "Docs"
$RuJson = Join-Path $PSScriptRoot "table_desc_ru.json"
$ru = Get-Content $RuJson -Raw -Encoding UTF8 | ConvertFrom-Json

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

function Get-FieldDesc($name) {
    $prop = $ru.fields.PSObject.Properties[$name]
    if ($prop) { return $prop.Value }
    if ($name -match 'Id$' -and $name -ne 'RowId') {
        $ref = $name.Substring(0, $name.Length - 2)
        return "REF:$ref"
    }
    return $name
}

function Fix-RefDesc($text) {
    if ($text -like 'REF:*') {
        return ($ru.ref_prefix + ' ' + $text.Substring(4))
    }
    return $text
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

$RelJson = Join-Path $PSScriptRoot 'table_relationships_ru.json'
$LogicalJson = Join-Path $PSScriptRoot 'logical_foreign_keys.json'
$rel = Get-Content $RelJson -Raw -Encoding UTF8 | ConvertFrom-Json
$logical = Get-Content $LogicalJson -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($tp in $logical.PSObject.Properties) {
    $tname = $tp.Name
    if (-not $fks.ContainsKey($tname)) { $fks[$tname] = @{} }
    foreach ($pair in $tp.Value) {
        $fks[$tname][$pair[0]] = $pair[1]
    }
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

foreach ($prop in $ru.extra_tables.PSObject.Properties) {
    $tname = $prop.Name
    $rows = $prop.Value
    $cols = @()
    foreach ($row in $rows) {
        $cols += [pscustomobject]@{
            name = $row[0]
            type = $row[1]
            constraints = $row[2]
            desc = $row[3]
        }
    }
    $entities[$tname] = @{ columns = $cols; pk = @('RowId') }
}

$md = @("# $($ru.title)", "", $ru.intro, "", $ru.fk_report_note, "")
$priority = @('Users','Roles','Employees','Workshops','Tasks','Cars')
$order = $priority + ($entities.Keys | Where-Object { $_ -notin $priority -and $_ -ne 'sysdiagrams' } | Sort-Object)
$num = 1
foreach ($tname in $order) {
    $td = $ru.tables.PSObject.Properties[$tname]
    $about = if ($td) { $td.Value } else { $ru.default_about }
    $md += "$($ru.table_word) **$tname** $($ru.stores_prefix) $about $($ru.structure_in) 2.$num."
    $md += ''
    $cap = '**' + $ru.table_word + ' 2.' + $num + ' ' + $ru.table_sep + ' ' + $ru.table_caption + ' ' + $tname + '**'
    $md += $cap
    $md += ''
    $hdr = '| ' + $ru.col_field + ' | ' + $ru.col_type + ' | ' + $ru.col_constraints + ' | ' + $ru.col_desc + ' |'
    $md += $hdr
    $md += '|------|------------|-------------|----------|'
    $fkMap = if ($fks.ContainsKey($tname)) { $fks[$tname] } else { @{} }
    foreach ($c in $entities[$tname].columns) {
        if ($c.constraints) { $con = $c.constraints; $d = $c.desc }
        else {
            $con = Get-Constraints $c $entities[$tname].pk $fkMap
            $d = Fix-RefDesc (Get-FieldDesc $c.name)
        }
        $line = '| ' + $c.name + ' | ' + $c.type + ' | ' + $con + ' | ' + $d + ' |'
        $md += $line
    }
    $linkProp = $rel.PSObject.Properties[$tname]
    if ($linkProp) {
        $md += ''
        $md += ('**' + $ru.links_label + ':** ' + $linkProp.Value)
    }
    $md += ''
    $num++
}
[System.IO.File]::WriteAllText((Join-Path $Docs 'DriveCare_Database_Table_Structures.md'), ($md -join [Environment]::NewLine), [Text.UTF8Encoding]::new($false))

function Write-ErPuml($fileName, $tables, $title, [switch]$Compact) {
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine("@startuml $fileName")
    if ($title) {
        [void]$sb.AppendLine("title $title")
    }
    [void]$sb.AppendLine('hide circle')
    [void]$sb.AppendLine('skinparam linetype ortho')
    [void]$sb.AppendLine('skinparam shadowing false')
    [void]$sb.AppendLine('top to bottom direction')
    [void]$sb.AppendLine('')
    $present = $tables | Where-Object { $entities.ContainsKey($_) } | Select-Object -Unique
    $alias = @{}
    foreach ($t in $present) { $alias[$t] = ($t -replace '[^A-Za-z0-9_]','_') }
    foreach ($t in $present) {
        [void]$sb.AppendLine("entity `"$t`" {")
        foreach ($c in $entities[$t].columns) {
            $pk = if ($entities[$t].pk -contains $c.name) { ' *' } else { '' }
            if ($Compact) {
                [void]$sb.AppendLine("  $($c.name)$pk")
            } else {
                [void]$sb.AppendLine("  $($c.name) : $($c.type)$pk")
            }
        }
        [void]$sb.AppendLine('}')
        [void]$sb.AppendLine('')
    }
    $seen = @{}
    foreach ($t in $present) {
        if (-not $fks.ContainsKey($t)) { continue }
        foreach ($col in $fks[$t].Keys) {
            $ref = $fks[$t][$col]
            if ($ref -in $present) {
                $key = "$t|$ref|$col"
                if (-not $seen.ContainsKey($key)) {
                    [void]$sb.AppendLine("$($alias[$t]) }o--|| $($alias[$ref]) : $col")
                    $seen[$key] = $true
                }
            }
        }
    }
    [void]$sb.AppendLine('@enduml')
    [System.IO.File]::WriteAllText((Join-Path $Docs "$fileName.puml"), $sb.ToString(), [Text.UTF8Encoding]::new($false))
}

$PartsJson = Join-Path $PSScriptRoot 'er_parts_ru.json'
$parts = Get-Content $PartsJson -Raw -Encoding UTF8 | ConvertFrom-Json

$oldEr = Get-ChildItem (Join-Path $Docs 'ER_*.puml') -ErrorAction SilentlyContinue
foreach ($f in $oldEr) { Remove-Item $f.FullName -Force }
if (Test-Path (Join-Path $Docs 'DriveCare_ER_AllTables.puml')) {
    Remove-Item (Join-Path $Docs 'DriveCare_ER_AllTables.puml') -Force
}

$partCount = 0
foreach ($prop in $parts.PSObject.Properties) {
    $fileName = $prop.Name
    $meta = $prop.Value
    $tables = @($meta.tables)
    Write-ErPuml $fileName $tables $meta.title -Compact
    $partCount++
}

$classPath = Join-Path $Docs 'DriveCare_Classes_Main.puml'
Copy-Item (Join-Path $PSScriptRoot 'DriveCare_Classes_Main.puml') $classPath -Force

Write-Host "Generated. Tables: $($entities.Count). ER parts: $partCount"
