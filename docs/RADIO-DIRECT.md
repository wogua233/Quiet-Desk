# 国内直连目录（0.5.6）

2026-09-14 本机实际应用无代理解码通过，共 12 项。分类目录随软件离线提供。境外爵士源在本机 WLAN 路由下可用，不保证所有地区或运营商；电台实时节目可能包含主持或广告。

| 电台 | 风格 | 地区 | 格式 | 来源 |
| --- | --- | --- | --- | --- |
| 0N · Smooth Jazz | 爵士 · 舒缓 | 德国 | MP3 | [公开播放页](https://www.0nradio.com/) |
| 101 Smooth Jazz | 爵士 · 舒缓 | 美国 | MP3 | [公开播放页](https://101smoothjazz.com/) |
| 上海经典947 | 古典 · 舒缓 | 中国 | MP3 | [公开播放页](https://m.qtfm.cn/channels/267/) |
| 中国之声 | 国内直连 · 央广 | 中国 | HLS/AAC | [公开播放页](https://www.radio.cn/pc-portal/home/index.html?type=x) |
| 北京音乐广播 | 流行 · 音乐 | 中国 | MP3 | [公开播放页](https://www.rbc.cn/) |
| 广东音乐之声 | 流行 · 音乐 | 中国 | MP3 | [公开播放页](https://m.qtfm.cn/channels/1260/) |
| 广州金曲音乐广播 | 流行 · 音乐 | 中国 | MP3 | [公开播放页](https://m.qtfm.cn/channels/20192/) |
| 怀集音乐之声 | 流行 · 音乐 | 中国 | MP3 | [公开播放页](https://m.qtfm.cn/channels/4804/) |
| 江苏经典流行音乐广播 | 流行 · 经典 | 中国 | MP3 | [公开播放页](https://www.vojs.cn/2014new/c/g/) |
| 温州音乐之声 | 流行 · 音乐 | 中国 | MP3 | [公开播放页](https://m.qtfm.cn/channels/1149/) |
| 湖北经典音乐广播 | 流行 · 经典 | 中国 | MP3 | [公开播放页](https://m.qtfm.cn/channels/1296/) |
| 经济之声 | 国内直连 · 央广 | 中国 | HLS/AAC | [公开播放页](https://www.radio.cn/pc-portal/home/index.html?type=x) |

地址、来源及验证说明保存在 `src/QuietDesk/Assets/domestic-radio.json`。实际连接和 PCM 解码记录见 [逐项结果](validation-v056-radio.json)。停止后网络连接与解码资源仍按原有机制释放，不为新增目录项常驻打开连接。

公开节目流可能随服务方调整而变化。失败候选不会因能打开官网或外部解码成功就标记为应用可用；本轮排除的源及未验证范围见 [验证报告](validation-v056.md)。
