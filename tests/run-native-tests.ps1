$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$testBinary = Join-Path $PSScriptRoot 'CepSafety.exe'
& $compiler /nologo /target:exe "/out:$testBinary" /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll "/resource:$taskRoot/src/embedded/cep.zip,payload.zip" "/resource:$taskRoot/src/embedded/cep-setup.exe,cep-setup.exe" (Join-Path $taskRoot 'src/CepInstaller.cs') (Join-Path $taskRoot 'src/CepManager.cs') (Join-Path $PSScriptRoot 'CepSafety.cs')
if ($LASTEXITCODE -ne 0) { throw 'Native test compilation failed.' }
$testProfile = Join-Path $PSScriptRoot ('cep-safety-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
try {
  & $testBinary $testProfile
  if ($LASTEXITCODE -ne 0) { throw 'CEP safety tests failed.' }
  Copy-Item -LiteralPath (Join-Path $testProfile 'cep-safety-results.json') -Destination (Join-Path $PSScriptRoot 'cep-safety-results.json') -Force
}
finally {
  if (Test-Path -LiteralPath $testProfile) { Remove-Item -LiteralPath $testProfile -Recurse -Force -ErrorAction SilentlyContinue }
  if (Test-Path -LiteralPath $testBinary) { Remove-Item -LiteralPath $testBinary -Force -ErrorAction SilentlyContinue }
}
