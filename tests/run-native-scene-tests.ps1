$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$exe = Join-Path $PSScriptRoot 'NativeSceneGraphSafety.exe'
try {
  & $compiler /nologo /target:exe "/out:$exe" /r:System.Web.Extensions.dll (Join-Path $root 'src/NativeSceneGraph.cs') (Join-Path $PSScriptRoot 'NativeSceneGraphSafety.cs')
  if ($LASTEXITCODE -ne 0) { throw 'Native scene graph test compilation failed.' }
  & $exe
  if ($LASTEXITCODE -ne 0) { throw 'Native scene graph test failed.' }
}
finally { if (Test-Path -LiteralPath $exe) { Remove-Item -LiteralPath $exe -Force -ErrorAction SilentlyContinue } }
