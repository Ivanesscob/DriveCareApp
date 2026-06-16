$ErrorActionPreference = 'Stop'
$t = Get-Content (Join-Path $PSScriptRoot 'example_impl_text.txt') -Raw -Encoding UTF8

$marker = [char]0x0033 + '.' + [char]0x0031 + '.' + [char]0x0031
# 3.1.1
$start = $t.LastIndexOf($marker)
if ($start -lt 0) {
    $marker2 = '3.1.1'
    $idx = 0
    $count = 0
    while (($idx = $t.IndexOf($marker2, $idx)) -ge 0) {
        $count++
        if ($count -ge 2) { $start = $idx; break }
        $idx++
    }
}

$end = $t.IndexOf('3.2', $start + 500)
if ($end -lt 0) { $end = [Math]::Min($start + 50000, $t.Length) }

$s = $t.Substring($start, $end - $start)
$s = $s -replace '\x07', "`n"

$out = Join-Path $PSScriptRoot 'example_section3_body.txt'
$s | Out-File -FilePath $out -Encoding UTF8
Write-Host "Start=$start End=$end Len=$($s.Length)"
