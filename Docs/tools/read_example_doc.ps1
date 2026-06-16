$ErrorActionPreference = 'Stop'
$path = Join-Path $PSScriptRoot 'example_impl.docx'
$word = New-Object -ComObject Word.Application
$word.Visible = $false
$doc = $word.Documents.Open($path)
$text = $doc.Content.Text
$doc.Close()
$word.Quit()
[System.Runtime.Interopservices.Marshal]::ReleaseComObject($word) | Out-Null
$out = Join-Path $PSScriptRoot 'example_impl_text.txt'
$text | Out-File -FilePath $out -Encoding UTF8
Write-Host "Saved: $out"
