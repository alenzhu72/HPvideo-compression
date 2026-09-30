# 手机视频批量压缩工具

Windows 中文桌面工具，将多个视频批量转换为适合手机观看的 MP4。每个成功输出均小于 **180,000,000 字节（180MB）**，保留原视频。

## 下载与使用

到本仓库的 **Releases** 下载 `VideoCompressor-Windows-x64-v1.0.0.zip`，解压整个文件夹，双击 `Start.cmd`。

1. 点击“添加视频”，可一次选择多个文件。
2. 选择输出位置。
3. 点击“开始压缩”，完成后点击“打开结果”。

Windows 10/11 64 位，使用系统 .NET Framework 4.x。首次启动从 Gyan 下载 FFmpeg 并校验 SHA-256，之后可离线使用。不需要安装 Python。压缩全程本地执行，不上传视频。公开 Release 不包含第三方二进制。

## 功能

- 通常最高横屏 1280×720、竖屏 720×1280，H.264 / AAC，30fps。
- 根据时长计算码率；输出超限自动重试，最多四次。
- 完成前校验实际文件大小和时长，不通过则报错。
- 同名输出自动编号，不覆盖已有文件。
- 支持停止任务，清理未完成文件，保留已完成结果。
- 单个视频失败后继续处理队列；双击失败行查看原因。

## 本地编译

在 Windows PowerShell 中运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

输出为 `dist\手机视频压缩.exe`。将可信来源的 `ffmpeg.exe` 和 `ffprobe.exe` 放入同一目录后运行。可使用本机已有的 FFmpeg，或从 [FFmpeg 下载页](https://ffmpeg.org/download.html) 选择 Windows 构建。

源代码位于 `src/VideoCompressor.cs`，使用系统自带的 .NET Framework C# 编译器，无 NuGet 依赖。

## 验证记录

v1.0.0 使用合成视频验证了：横屏缩放、竖屏保持方向、无音轨视频、中文及空格路径、输出体积和时长、开始前取消、编码中取消、临时文件清理、无效输入处理。8 秒测试视频在 900,000 字节测试上限下输出为 830,368 字节。

## 限制

- 仅保留第一条非封面视频流及第一条音轨，不保留字幕、章节及额外音轨。
- HDR 色彩转换未专门适配，主要面向普通 SDR 视频。
- 极长视频可能无法在最低码率要求下达到大小限制，此时会报错。
- 工具仅约束输出文件大小，不保证第三方聊天软件接受所有文件。
- 程序尚未进行代码签名。

## 第三方组件

本工具调用独立的 FFmpeg 程序。下载脚本从 Gyan 获取当前 release essentials 构建，包含 GPL 组件。已使用本机 FFmpeg 8.1.1 full_build 验证压缩功能。许可证见 `third-party/FFmpeg-LICENSE.txt`；构建及源代码信息见 [Gyan FFmpeg builds](https://www.gyan.dev/ffmpeg/builds/) 和 [FFmpeg](https://ffmpeg.org/)。如向外再分发其二进制文件，应履行对应许可证和源代码提供义务。

本项目自己的源代码暂未指定开源许可证；仓库可见性不代表授予开源许可。
