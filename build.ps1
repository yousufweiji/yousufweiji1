$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$buildRoot = Join-Path $taskRoot 'build'
New-Item -ItemType Directory -Force -Path $buildRoot | Out-Null
Copy-Item -LiteralPath (Join-Path $taskRoot 'src/web') -Destination $buildRoot -Recurse -Force
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$resources = Get-ChildItem -LiteralPath (Join-Path $taskRoot 'src/web') -File | Where-Object { $_.Name -ne 'production-source.html' } | ForEach-Object { '/resource:' + $_.FullName + ',web.' + $_.Name }
& $compiler @resources "/resource:$taskRoot/src/embedded/cep.zip,payload.zip" "/resource:$taskRoot/src/embedded/cep-setup.exe,cep-setup.exe" /nologo /target:winexe /platform:anycpu /optimize+ "/out:$buildRoot/Toolkit.exe" "/win32manifest:$taskRoot/src/app.manifest" /r:Microsoft.CSharp.dll /r:System.Xml.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll (Join-Path $taskRoot 'src/Desktop.cs') (Join-Path $taskRoot 'src/NativeFormats.cs') (Join-Path $taskRoot 'src/CepInstaller.cs') (Join-Path $taskRoot 'src/CepManager.cs') (Join-Path $taskRoot 'src/NativeSceneGraph.cs') (Join-Path $taskRoot 'src/NativeCanvasForm.cs')
if ($LASTEXITCODE -ne 0) { throw 'Desktop build failed.' }
Write-Output 'Built Toolkit.exe'
