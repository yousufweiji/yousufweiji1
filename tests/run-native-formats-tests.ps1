$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$testBinary = Join-Path $PSScriptRoot 'NativeFormatsSafety.exe'
$results = Join-Path $PSScriptRoot 'native-formats-results.json'
$profile = Join-Path $PSScriptRoot ('native-formats-' + [Guid]::NewGuid().ToString('N').Substring(0,8) + '.json')
try {
  & $compiler /nologo /target:exe "/out:$testBinary" /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll (Join-Path $taskRoot 'src/NativeFormats.cs') (Join-Path $PSScriptRoot 'NativeFormatsSafety.cs')
  if ($LASTEXITCODE -ne 0) { throw 'Native format test compilation failed.' }
  & $testBinary $profile
  if ($LASTEXITCODE -ne 0) { throw 'Native format tests failed.' }
  Copy-Item -LiteralPath $profile -Destination $results -Force
}
finally {
  if (Test-Path -LiteralPath $profile) { Remove-Item -LiteralPath $profile -Force -ErrorAction SilentlyContinue }
  if (Test-Path -LiteralPath $testBinary) { Remove-Item -LiteralPath $testBinary -Force -ErrorAction SilentlyContinue }
}
