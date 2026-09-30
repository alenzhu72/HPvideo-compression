param([switch]$SetupOnly)
$ErrorActionPreference = 'Stop'
try {
    $engine = Join-Path $PSScriptRoot 'ffmpeg.exe'
    $probe = Join-Path $PSScriptRoot 'ffprobe.exe'
    if (!(Test-Path -LiteralPath $engine) -or !(Test-Path -LiteralPath $probe)) {
        Write-Host '首次运行：正在下载视频处理组件，请稍候。下载完成后可离线使用。'
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        $downloadRoot = Join-Path $PSScriptRoot ('.setup-' + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $downloadRoot | Out-Null
        try {
            $url = 'https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip'
            $archive = Join-Path $downloadRoot 'ffmpeg.zip'
            $client = New-Object Net.WebClient
            try {
                $checksumText = $client.DownloadString($url + '.sha256')
                $client.DownloadFile($url, $archive)
            } finally { $client.Dispose() }
            $match = [regex]::Match($checksumText, '(?i)\b[0-9a-f]{64}\b')
            if (!$match.Success) { throw '无法读取组件校验码。请稍后重试。' }
            $actual = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
            if ($actual -ne $match.Value) { throw '组件下载校验失败，请重新运行。' }
            Expand-Archive -LiteralPath $archive -DestinationPath (Join-Path $downloadRoot 'unpacked')
            $bins = @(Get-ChildItem -LiteralPath (Join-Path $downloadRoot 'unpacked') -Filter ffmpeg.exe -Recurse)
            if ($bins.Count -ne 1) { throw '下载包结构不符合预期。' }
            $buildRoot = Split-Path (Split-Path $bins[0].FullName)
            Copy-Item -LiteralPath (Join-Path $buildRoot 'bin\ffprobe.exe') -Destination $probe
            Copy-Item -LiteralPath $bins[0].FullName -Destination $engine
            foreach ($name in @('LICENSE', 'README.txt')) {
                $notice = Join-Path $buildRoot $name
                if (Test-Path -LiteralPath $notice) { Copy-Item -LiteralPath $notice -Destination (Join-Path $PSScriptRoot ('FFmpeg-' + $name)) }
            }
        } finally {
            $resolvedRoot = [IO.Path]::GetFullPath($PSScriptRoot) + [IO.Path]::DirectorySeparatorChar
            $resolvedDownload = [IO.Path]::GetFullPath($downloadRoot)
            if ($resolvedDownload.StartsWith($resolvedRoot, [StringComparison]::OrdinalIgnoreCase) -and (Split-Path $resolvedDownload -Leaf).StartsWith('.setup-')) {
                Remove-Item -LiteralPath $resolvedDownload -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }
    if (!$SetupOnly) { Start-Process -FilePath (Join-Path $PSScriptRoot '手机视频压缩.exe') }
} catch {
    Write-Host ('启动失败：' + $_.Exception.Message) -ForegroundColor Red
    Write-Host '也可以手动将 ffmpeg.exe 和 ffprobe.exe 放到本文件夹，再运行程序。'
    exit 1
}
