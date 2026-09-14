# 静隅 0.5.2 订阅响应兼容性修正

2026-09-14，Windows 本机，Release / win-x64 自包含。

## 问题与证据

用户截图显示 Nature Chemistry 更新失败，错误为 XmlReader 禁止 DTD；最近成功时间仍保留，来源条目数为 8。历史失败响应未保存，不能确定那次是正常 RSS 带文档声明，还是验证／登录／错误网页。

本轮使用应用相同 User-Agent、系统默认网络路径请求 `https://www.nature.com/nchem.rss`：HTTP 200，`application/rss+xml; charset=utf-8`，16849 字节，正常 RDF/RSS，没有 DOCTYPE；通过发布版解析器得到 8 条文章。因此当前请求成功，但不能据此保证以后每次均可连接，也不能证明历史失败原因。

## 修正

- XmlReader 使用 DtdProcessing.Ignore：忽略声明，不解析 DTD；XmlResolver 仍为 null，不加载外部资源。文档大小限制保留。
- 先检查根元素：HTML 网页明确提示“来源返回了网页”，不再等到 HTML 正文或实体导致难懂的 XML 错误。
- 其他 XML 格式错误在来源状态中显示中文说明；失败沿用原退避逻辑，不删除已入库文章，不显示为无更新。
- 未改变 API 调用、摘要缓存、音乐功能及用户订阅。

依据：[Microsoft XmlReaderSettings.DtdProcessing](https://learn.microsoft.com/en-us/dotnet/fundamentals/runtime-libraries/system-xml-xmlreadersettings-dtdprocessing)。本轮未启用 DtdProcessing.Parse。

## 验证

自包含发布成功。76 项 PASS，进程退出码 0，详见 [原始检查记录](validation-v052-checks.txt)。包含正常 RSS、忽略外部 DTD 而不请求、外部及内部实体不展开、带 DOCTYPE 的非规范 HTML 错误页、实际 Nature Chemistry 响应解析，以及原有阅读回归检查。

本轮未调用真实收费 AI，无音乐长测。没有复现用户截图中的那一次响应，也未修改用户网络、代理、密钥和运行实例。

```powershell
./scripts/build.ps1 -Publish
./artifacts/QuietDesk-v0.5.2-win-x64/QuietDesk.exe --reading-test --nature-feed-file D:/Games/music/artifacts/v052/nchem-response.xml --out D:/Games/music/artifacts/v052/reading-tests.txt
./scripts/package.ps1
```

真实订阅响应仅保留在本机 artifacts 供检查，不随源码或发布包分发。
