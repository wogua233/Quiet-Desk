# 静隅 · Quiet Desk

适合工作与学习时使用的 Windows 桌面音乐播放器。采用原生 WPF 界面，以中性深灰与柔粉色为主，支持真正嵌入桌面的紧凑组件。

当前版本 **0.3.4**：修复音频输出缓冲区清零不完整导致的声音残留，自动回归测试及用户本机试听已确认修复。详见 [修复与验证记录](docs/validation-v034.md)。

## 主要功能

- **18 种离线氛围音**：雨声、海浪、森林、咖啡馆、白噪声等，最多四路混音，可保存个人声音组合。
- **音乐与电台**：本地 MP3 / WAV / FLAC 队列，Radio Browser 搜索、收藏，以及独立内置的国内电台目录；支持 MP3、HLS/AAC 直播。
- **独立播放控制**：电台／音乐可单独暂停，不影响环境声；支持选择输出设备。
- **桌面组件**：整卡拖动、位置锁定、左右贴边收起、背景透明度调整与托盘控制。
- **静默专注计时**：暂停、恢复与本地每日统计；计时结束不响铃、不弹窗。

默认不自动播放、不随系统启动，轻音效默认关闭。设置与收藏保存在 `%LOCALAPPDATA%/QuietDesk/`。

## 构建与运行

环境：Windows 11 x64、.NET SDK **10.0.401**（或兼容补丁版本）。技术栈：C#、.NET 10、WPF、NAudio；HLS/AAC 使用按需启动的 FFmpeg 子进程，不使用浏览器界面运行时。

```powershell
# 编译
./scripts/build.ps1

# 生成自包含便携版，无需在运行电脑另装 .NET
./scripts/build.ps1 -Publish

# 运行
./artifacts/QuietDesk-v0.3.4-win-x64/QuietDesk.exe

# 打包便携版与源码
./scripts/package.ps1
```

仓库包含离线声音与固定版本的解码器，因此首次克隆体积较大。运行便携版时保留同目录的 DLL、Assets 和许可文件。

## 文档与许可

- [使用说明](docs/USER-GUIDE.md) · [0.3 更新说明](docs/CHANGELOG-v03.md)
- [验收报告](docs/validation-v03.md) · [实机复核清单](docs/manual-checks-v03.md)
- [声音来源与许可](docs/SOUND-LICENSES.md) · [FFmpeg 版本、源码与许可](docs/FFMPEG.md)

声音素材和第三方组件分别遵循各自许可；其中图书馆录音为 **CC BY-NC 4.0，仅限非商业使用**。国内电台已做实际解码测试，但不能排除系统 VPN 路由影响，也不保证所有地区网络均可用。桌面嵌入依赖 Windows 外壳行为，部分硬件与交互场景仍待实机确认，详见验收报告。
