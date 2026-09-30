$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw '.NET Framework C# compiler was not found.' }
$destination = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force -Path $destination | Out-Null
$source = Join-Path $PSScriptRoot 'src\VideoCompressor.cs'
$exe = Join-Path $destination '手机视频压缩.exe'
& $compiler /nologo /target:winexe /optimize+ "/out:$exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll $source
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Write-Host "Built: $exe"
Write-Host 'Place ffmpeg.exe and ffprobe.exe beside the application before running.'
