# 静隅 0.5.5 官方来源接口核查与接入

2026-09-14，Windows 本机，Release / win-x64 自包含发布。

## 交付变化

- 对内置全部 12 个来源核实程序订阅渠道，结果及官方依据见 [来源接口清单](READING-SOURCES.md)。官方 RSS 本来就是供程序使用的接口；不能把 HTTP 200 的 HTML 验证页当 RSS 成功。
- 新增 Springer Nature Meta 和 Guardian Content 目录适配器，独立密钥配置后优先调用 API；无密钥保留 RSS，授权失败显示错误而不悄悄改变渠道。
- APS 继续采用 RSS 目录，缺失摘要改走官方 Harvest 单篇 JSON API。未授权目录实测只包含部分内容且未按刊物过滤，故没有拿它替换四刊的 RSS。非开放文章可配置单独的 APS 授权密钥。
- 密钥使用 DPAPI CurrentUser 独立加密，不与翻译服务共享；API 客户端禁止重定向，APS 使用 Bearer，Springer／Guardian 按官方接口使用查询参数，但不记录带密钥地址或响应。
- 来源 API 最多单路请求、至少一秒间隔，按服务持久化每日 200 次应用额度；目录每页 50、最多 10 页，响应每页限制 4 MB。到上限明确显示部分更新。未启用阅读时仍不创建相关客户端或网络任务，无新子进程或依赖。
- 切换渠道不删除已缓存摘要／译文／总结。真实内容变更仍会标记待更新。APS 升级迁移仅一次重置不足摘要的旧获取失败状态；以后重启不循环清除 401 失败。修改 APS 密钥后允许再次获取。

## 验证结果

最终构建成功，**114 项 PASS，退出码 0**，见 [原始检查记录](validation-v055-checks.txt)。包括原有阅读回归和新增密钥序列化保护、服务路由隔离、分页、日期、未知摘要、401、重定向、错误 JSON、请求上限、订阅检查不创建 AI 任务、跨渠道及旧版缓存保留、APS DOI 身份校验、一次性迁移。

网络实测：本轮 12 个 RSS 均有一次 HTTP 200 且成功解析，详见 [RSS 结果](validation-v055-rss.json)。这个结果不保证持续可用，也未写回用户数据库清除历史失败。此前 Nature Communications 的 HTML Client Challenge 现象仍有效。

发布版 APS 适配器实测两篇取得摘要，另两篇返回 401；未借用浏览器 Cookie、未改变代理或 VPN，详见 [APS 结果](validation-v055-aps.json)。Springer／Guardian 的无密钥请求均为 401，未持有授权密钥，真实带密钥读取尚未验收。分页和字段解析通过模拟响应测试，不将其称为真实全部可用。

设置展开页及订阅页生成了 760×560 下 100%、125%、150%、200%，以及 520×560 下 100% 的离屏布局图。人工查看了设置页 760／520 和订阅页 760 的 100% 图：文字可换行，密码框与操作按钮在滚动容器内，主要操作可见。这不是实际系统 DPI、滚轮、焦点或键盘操作验收；这些实机项目未测。

## 短测

隔离阅读服务、1 万条模拟缓存，BBC 实际同步成功 24 条：

| 项目 | 测量值 |
| --- | --- |
| 一次来源同步 | 1.965 秒 |
| 100 次分页查询平均 | 56.042 毫秒 |
| 后续空闲采样 | 30.260 秒 |
| 平均整机 CPU | 0.0194% |
| 进程私有提交内存峰值 | 46,063,616 字节（43.93 MiB） |

[原始短测数据](validation-v055-short.json)。这是隔离阅读服务、不含 WPF／音频的私有提交内存，不能等同于整机应用私有工作集，也不能代替音乐单路／四路性能验收。未进行两小时长测，未产生真实 AI 生成请求，未修改用户订阅或翻译配置，未替换正在运行的旧实例。

## 重现

```powershell
./scripts/build.ps1 -Publish
./artifacts/QuietDesk-v0.5.5-win-x64/QuietDesk.exe --reading-test --out D:/Games/music/artifacts/v055/reading-tests.txt
./artifacts/QuietDesk-v0.5.5-win-x64/QuietDesk.exe --reading-test --publisher-live --out D:/Games/music/artifacts/v055/aps-adapter-live.json
./artifacts/QuietDesk-v0.5.5-win-x64/QuietDesk.exe --reading-test --layout --settings --publisher-settings --out D:/Games/music/artifacts/v055/settings.png
./artifacts/QuietDesk-v0.5.5-win-x64/QuietDesk.exe --reading-test --short-check --out D:/Games/music/artifacts/v055/short-check.json
./scripts/package.ps1
```
