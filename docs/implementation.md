# 实现结构

- `Core/PlayerModel.cs`：主界面与桌面卡片共享的播放、场景、收藏、队列、计时状态。
- `Audio/`：离线循环提供器、软限幅混音总线、WASAPI 输出、三秒 PCM 环形缓冲、音量与暂停渐变；MP3 电台在独立任务读取，单次读取有超时和取消。
- `Desktop/DesktopHost.cs`：真实 `SHELLDLL_DefView` 子窗口，220×320 DIP，展开混音后增高，拖动、锁定、DPI 和位置处理，宿主失效有限重试；无悬浮窗回退。
- `UI/`：WPF 原生深色界面和托盘入口，无 WebView / Electron，无持续频谱或背景动画。
- `Core/FocusClock.cs`：可测试计时内核，单调经过时间、跨日统计、睡眠间隔排除，完成后静默停止。
- `Core/Models.cs`：LocalAppData 本地状态、原子保存与损坏文件恢复。
- `Core/RadioDirectory.cs`：Radio Browser 查询、备用服务器与最近查询缓存。
- `Verification.cs` / `PerformanceRecorder.cs`：显式验收入口，正常启动不自动执行。

依赖固定 NAudio 2.2.1 与 .NET 10。声音素材适用原作者许可，不复制 Blanket 应用代码。对外不提供网络 API，不监听本地控制端口。

首版在线流限 MP3，采用目录 codec 过滤；本地 WAV/MP3/FLAC 单声道或立体声。场景预设本身不自动开始发声。
