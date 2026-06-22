# Rebuilds shablon_tokens.docx from shablon.docx with correct placeholders (table headers unchanged).
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$source = Join-Path (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $dir))) 'DriveCare\Services\Words\shablon.docx'
if (-not (Test-Path $source)) { $source = Join-Path $dir 'shablon.docx' }

$dest = Join-Path $dir 'shablon_tokens.docx'
$tmp = Join-Path $env:TEMP "zakaz_build_$(Get-Random)"
New-Item -ItemType Directory -Path "$tmp\doc" -Force | Out-Null
Copy-Item $source "$tmp\z.zip" -Force
Expand-Archive "$tmp\z.zip" "$tmp\doc" -Force

$xmlPath = "$tmp\doc\word\document.xml"
$xml = [IO.File]::ReadAllText($xmlPath, [Text.Encoding]::UTF8)

function Set-WtIndex {
    param([string]$Xml, [int]$Index, [string]$Text)
    $i = 0
    return [regex]::Replace($Xml, '<w:t([^>]*)>([^<]*)</w:t>', {
        param($m)
        if ($script:i -eq $Index) {
            $script:i++
            return "<w:t$($m.Groups[1].Value)>$([System.Security.SecurityElement]::Escape($Text) -replace '&apos;','''' -replace '&quot;','"')</w:t>"
        }
        $script:i++
        return $m.Value
    }.GetNewClosure())
}

# Simpler: manual escape for XML
function Esc([string]$s) {
    return [System.Net.WebUtility]::HtmlEncode($s)
}

function Set-WtIndex2 {
    param([ref]$Xml, [int]$Index, [string]$Text)
    $i = 0
    $Xml.Value = [regex]::Replace($Xml.Value, '<w:t([^>]*)>([^<]*)</w:t>', {
        param($m)
        if ($i -eq $Index) {
            $script:i = $i + 1
            $inner = $Text -replace '&', '&amp;' -replace '<', '&lt;' -replace '>', '&gt;'
            return "<w:t$($m.Groups[1].Value)>$inner</w:t>"
        }
        $i++
        return $m.Value
    })
}

# Label / header replacements (not table column headers 24-34, 91-98, 126-129)
$labelMap = @{
    'DriveCare' = '{{COMPANY_NAME}}'
    '(юридическое название вашего автосервиса)' = ''
    '(юридический адрес)' = '{{COMPANY_ADDRESS}}'
    '(телефон автосервиса)' = '{{COMPANY_PHONE}}'
    '(дата)' = '{{ORDER_DATE}}'
    '(время)' = '{{ORDER_TIME}}'
}
foreach ($k in $labelMap.Keys) {
    $xml = $xml.Replace(">$k</w:t>", ">$($labelMap[$k])</w:t>")
}
# broken split placeholder from docx
$xml = $xml.Replace('>(юриди</w:t>', '></w:t>')
$xml = $xml.Replace('>че</w:t>', '></w:t>')
$xml = $xml.Replace('>ское название вашего автосервиса)</w:t>', '></w:t>')

$labelSuffix = @{
    'Потребитель:' = 'Потребитель: {{CLIENT_NAME}}'
    'Адрес:' = 'Адрес: {{CLIENT_ADDRESS}}'
    'Телефон:' = 'Телефон: {{CLIENT_PHONE}}'
    'Автомобиль:' = 'Автомобиль: {{CAR_DESCRIPTION}}'
    'VIN-номер:' = 'VIN-номер: {{VIN}}'
    'Год выпуска:' = 'Год выпуска: {{YEAR}}'
    'Двигатель №:' = 'Двигатель №: {{ENGINE_NUMBER}}'
    'Пробег:' = 'Пробег: {{MILEAGE}}'
    'Кузов №:' = 'Кузов №: {{BODY_NUMBER}}'
    'Гос. номер:' = 'Гос. номер: {{PLATE_NUMBER}}'
    'Цвет:' = 'Цвет: {{COLOR}}'
    'Вид ремонта:' = 'Вид ремонта: {{REPAIR_TYPE}}'
    'Причины обращения:' = 'Причины обращения: {{VISIT_REASON}}'
    'Особые данные и рекомендации:' = 'Особые данные и рекомендации: {{SPECIAL_NOTES}}'
}
foreach ($k in $labelSuffix.Keys) {
    $xml = $xml.Replace(">$k</w:t>", ">$($labelSuffix[$k])</w:t>")
}

# Index-based: first data row works (35-44), works total (90), parts row (99-105), parts total (124)
$indexTokens = @{
    35 = '{{WORK_CODE}}'
    36 = '{{WORK_NAME}}'
    37 = '{{WORK_MULTIPLICITY}}'
    38 = '{{WORK_COEFFICIENT}}'
    39 = '{{WORK_PRICE_PER_HOUR}}'
    40 = '{{WORK_TIME}}'
    41 = '{{WORK_COST}}'
    42 = '{{WORK_DISCOUNT}}'
    43 = '{{WORK_AMOUNT}}'
    44 = '{{WORK_EXECUTOR}}'
    90 = '{{WORKS_TOTAL}}'
    99 = '{{PARTS_NUMBER}}'
    100 = '{{PARTS_NAME}}'
    101 = '{{PARTS_UNIT}}'
    102 = '{{PARTS_QUANTITY}}'
    103 = '{{PARTS_PRICE}}'
    104 = '{{PARTS_DISCOUNT}}'
    105 = '{{PARTS_AMOUNT}}'
    124 = '{{PARTS_TOTAL}}'
    156 = '{{SUBTOTAL}}'
}

# Summary lines - append value tokens after labels
$xml = $xml.Replace('>Стоимость работ</w:t>', '>Стоимость работ {{LABOR_COST_SUM}}</w:t>')
$xml = $xml.Replace('>Стоимость запчастей и материалов</w:t>', '>Стоимость запчастей и материалов {{PARTS_COST_SUM}}</w:t>')
$xml = $xml.Replace('>Общая стоимость:</w:t>', '>Общая стоимость: {{SUBTOTAL}}</w:t>')
$xml = $xml.Replace('>Ваша скидка составила:</w:t>', '>Ваша скидка составила: {{TOTAL_DISCOUNT}}</w:t>')
$xml = $xml.Replace('>Всего к оплате:</w:t>', '>Всего к оплате: {{TOTAL_TO_PAY}}</w:t>')

$i = 0
$xml = [regex]::Replace($xml, '<w:t([^>]*)>([^<]*)</w:t>', {
    param($m)
    $cur = $i
    $i++
    if ($indexTokens.ContainsKey($cur)) {
        $val = $indexTokens[$cur] -replace '&', '&amp;'
        return "<w:t$($m.Groups[1].Value)>$val</w:t>"
    }
    return $m.Value
})

[IO.File]::WriteAllText($xmlPath, $xml, [Text.UTF8Encoding]::new($false))
Remove-Item $dest -Force -ErrorAction SilentlyContinue
Compress-Archive -Path "$tmp\doc\*" -DestinationPath "$tmp\out.zip" -Force
Move-Item "$tmp\out.zip" $dest -Force
Remove-Item $tmp -Recurse -Force
& (Join-Path $dir 'pack_shablon_tokens.ps1')
Write-Host "Built: $dest"
