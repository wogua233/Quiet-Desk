# 0.3 国内电台验证

目录内置于 Assets/domestic-radio.json；启动“国内直连”分类无需请求 Radio Browser。显示官方／平台节目名称、格式、最近实际验证及直连待验证说明，不仅根据国家字段筛选。

当前 6 路：上海经典947、广东音乐之声、广州金曲音乐广播、怀集音乐之声、中国之声、经济之声。前四路为公开蜻蜓 FM MP3 流，后二路为央广 HLS/AAC。页面来源随目录条目保存。

2026-09-11 初测及 2026-09-12 最终复测：HTTP 客户端 UseProxy=false，FFmpeg 子进程移除 http_proxy/https_proxy/all_proxy 等代理环境变量；逐路连接并读取非零 PCM。原始峰值与耗时见 evidence-v03/domestic-test.json。测试没有修改 VPN 设置，无法排除系统 VPN 隧道路由，因此目录始终标记“VPN 路由下直连待验证”，不宣称全国网络可用。

官方播放器返回的另一组 ytlive 候选地址实际返回 403，未收入当前目录，也没有尝试绕过访问限制；失败证据见 evidence-v03/domestic-candidate-failures.json。直播地址可能变更，收藏并不承诺永久可用。
