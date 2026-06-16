$ErrorActionPreference = 'Stop'
$t = Get-Content (Join-Path $PSScriptRoot 'example_impl_text.txt') -Raw -Encoding UTF8

$patterns = @(
    '3 Реализация',
    '3. Реализация',
    'РЕАЛИЗАЦИЯ',
    'Реализация программ'
)

$start = -1
foreach ($p in $patterns) {
    $idx = $t.IndexOf($p)
    if ($idx -ge 0 -and ($start -lt 0 -or $idx -lt $start)) { $start = $idx }
}

if ($start -lt 0) {
    $start = $t.IndexOf('3.')
}

$end = $t.IndexOf('Заключение')
if ($end -lt 0) { $end = $t.IndexOf('Список использованных') }
if ($end -lt 0) { $end = [Math]::Min($start + 25000, $t.Length) }

$len = [Math]::Min($end - $start, 25000)
if ($start -lt 0) { $s = $t.Substring(0, 25000) } else { $s = $t.Substring($start, $len) }

# split on common paragraph marks
$s = $s -replace '\x07', "`n"
$s = $s -replace '\x0B', "`n"
$s = $s -replace '\x0D\x0A', "`n"

$out = Join-Path $PSScriptRoot 'example_section3.txt'
$s | Out-File -FilePath $out -Encoding UTF8
Write-Host "Start: $start End: $end Len: $($s.Length)"
Write-Host "Saved: $out"
