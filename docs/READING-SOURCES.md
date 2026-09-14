# 订阅来源接口核查 · 0.5.5

核查日期：2026-09-14，当前 Windows 网络。范围为内置 12 个来源；任意用户自定义 RSS／Atom 仍按填写的地址访问，不自动改投第三方。

## 结论与改动

RSS／Atom 本身就是给程序订阅使用的结构化接口。此前 Nature 的失败不是“RSS 不能供程序使用”，而是其发布端在部分请求中返回 HTML Client Challenge。本轮所有官方 RSS 都曾成功返回，不能据此保证以后不再拦截。

| 来源 | 已确认的官方程序接口 | 0.5.5 使用方式 |
| --- | --- | --- |
| PRL | APS RSS；Harvest JSON API | RSS 目录；缺失摘要通过 Harvest API 补充 |
| PRX | APS RSS；Harvest JSON API | 同上 |
| PRB | APS RSS；Harvest JSON API | 同上 |
| PRE | APS RSS；Harvest JSON API | 同上 |
| JACS | ACS 官方 RSS | 保留 ASAP RSS；未核实可直接替换订阅目录的通用公开 REST API |
| Nature | 官方 RSS；Springer Nature Meta API | 配置独立来源密钥后使用 Meta API，否则 RSS |
| Nature Communications | 官方 RSS；Springer Nature Meta API | 同上 |
| Nature Chemistry | 官方 RSS；Springer Nature Meta API | 同上 |
| Science | 官方 RSS | 保留官方 RSS；未核实可直接替换的通用公开 REST API |
| Scientific American | 官方公开 RSS | 保留 RSS；未把它混入 Nature 学术期刊 Meta 查询 |
| BBC World | BBC 官方新闻 RSS | 保留 RSS；未采用节目／音频 API 代替新闻目录 |
| The Guardian World | 官方 RSS；Content API | 配置独立来源密钥后使用 Content API，否则 RSS |

“未核实”不等于断言出版社绝无商务接口。面向机构的全文 TDM 服务、COUNTER 使用统计 API 和批量研究数据下载，不等于适用本阅读器的公开增量摘要目录，未擅自采用。Crossref 继续仅补充已有 DOI 的元数据，不声称它提供完整刊物目录。JACS 等没有新增可用摘要 API 的来源保留原有公开页面摘要补充逻辑；本版并非禁用一切 HTML 请求。

## 官方依据

- [APS RSS](https://journals.aps.org/feeds) 面向阅读器；[Harvest 入口](https://harvest.aps.org/) 和 [API 文档](https://harvest.aps.org/docs/harvest-api) 说明 JSON 元数据、Bearer 密钥及开放内容权限。
- [ACS RSS 目录](https://pubs.acs.org/pages/rss) 明确提供阅读器订阅。JACS 使用 ASAP 地址，不重新启用已失效的旧 showFeed 地址。
- [Springer Nature 开发者入口](https://dev.springernature.com/)、[Meta 请求与字段示例](https://dev.springernature.com/docs/advanced-querying/example-advanced-queries/) 和 [产品范围](https://datasolutions.springernature.com/products/meta-data/) 提供结构化元数据与摘要能力。账户实际开通范围及摘要覆盖需授权后验证。
- [Science RSS 入口](https://www.science.org/content/page/email-alerts-and-rss-feeds)；本次文档页普通请求受限，但其正式 science RSS 返回并解析成功。未用 Science Partner Journals 的说明替代 Science 主刊说明。
- [Scientific American 联系页面](https://www.scientificamerican.com/contact-us/) 列出公开 RSS。
- [BBC 官方开发者新闻 feeds](https://support.bbc.co.uk/platform/feeds/NewsFeeds.htm) 介绍其新闻 RSS。
- [Guardian 授权入口](https://open-platform.theguardian.com/access/)、[搜索 API 文档](https://open-platform.theguardian.com/documentation/search) 说明密钥、栏目、日期和导读字段。

这些依据说明程序访问渠道，不是对第三方内容任意再分发或云端处理的统一授权；按具体服务开通范围使用。

## 官方 RSS 实测

下表是一次成功检查的返回量，**不是当天发表数，也不是完整历史覆盖数**。最近七天为应用解析后的本地日期范围。

| 来源 | HTTP | 返回记录 | 近七天记录 |
| --- | --- | --- | --- |
| PRL | 200 | 100 | 85 |
| PRX | 200 | 100 | 6 |
| PRB | 200 | 100 | 100 |
| PRE | 200 | 100 | 49 |
| JACS | 200 | 16 | 16 |
| Nature | 200 | 75 | 75 |
| Science | 200 | 36 | 36 |
| Nature Communications | 200 | 8 | 8 |
| Nature Chemistry | 200 | 8 | 2 |
| Scientific American | 200 | 50 | 35 |
| BBC World | 200 | 25 | 25 |
| The Guardian World | 200 | 45 | 44 |

[原始精简结果](validation-v055-rss.json)。Nature Communications 返回八条，只说明当次源返回这八条，不证明近七天只有八篇。

## APS Harvest 实测与目录取舍

无授权请求的 PRX 目录返回 59 条，但混入多个 APS 刊物；PRL 目录请求也没有严格按刊物过滤，未见 PRB／PRE 的完整目录。因此本版不拿未授权 Harvest 目录替换 RSS，防止公开子集被误报为全部内容。

发布版 `--publisher-live` 对四刊分别取 RSS 首个 DOI，再调用官方 JSON 单篇端点，结果：

| 刊物 | DOI | 摘要结果 |
| --- | --- | --- |
| PRL | 10.1103/gg3l-cghb | HTTP 401，明确需要相应授权 |
| PRX | 10.1103/vl1q-p27w | 成功，清理 HTML 后 1316 字符 |
| PRB | 10.1103/pyhy-lc7r | 成功，清理 HTML 后 818 字符 |
| PRE | 10.1103/ypvb-3zn3 | HTTP 401，明确需要相应授权 |

[发布版适配器实测结果](validation-v055-aps.json)。这是各一篇样本，不保证整刊全部开放。程序检查返回 DOI 和刊物，不读取 PDF、全文 XML 或压缩包。接口未提供摘要时可继续尝试既有 Crossref 元数据补充。

## 待授权项目

Springer Nature Meta 和 Guardian Content 的无密钥真实请求均返回 HTTP 401。已完成接口实现和模拟验证，但没有这些服务的用户授权密钥；**真实带密钥请求、Nature 三刊实际覆盖量、API 摘要质量和账户额度均未验证**。界面提供密钥设置及官方申请入口，不预置公共测试密钥，不借用 DeepSeek 密钥。
