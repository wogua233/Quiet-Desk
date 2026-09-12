# 0.4.0 阅读模块第三方许可

版本来自 NuGet 锁定依赖。阅读功能不自带文章全文或出版社账号，不改变现有声音及FFmpeg许可。

| 组件 | 版本 | 许可 | 来源 |
|---|---|---|---|
| Microsoft.Data.Sqlite / Core | 10.0.12 | MIT | https://github.com/dotnet/efcore |
| SQLitePCLRaw bundle/core/provider/lib.e_sqlite3 | 2.1.12 | Apache-2.0（包装组件）；SQLite引擎为公共领域 | https://github.com/ericsink/SQLitePCL.raw |
| PdfPig | 0.1.16 | Apache-2.0及许可证内附第三方声明 | https://github.com/UglyToad/PdfPig |
| HtmlAgilityPack | 1.13.0 | MIT | https://github.com/zzzprojects/html-agility-pack |

完整文本在 `licenses/Microsoft-Sqlite-LICENSE.txt`、`licenses/SQLitePCLRaw-LICENSE.txt`、`licenses/PdfPig-0.1.16-LICENSE.txt`、`licenses/HtmlAgilityPack-LICENSE.txt`。PdfPig许可证包含所用第三方字体/编码资料声明，随包完整保留。

用户配置的API服务、期刊和新闻内容分别遵循其提供者的条款。阅读模块仅发送用户授权的摘要或点选全文，不提供账号共享、付费墙绕过或浏览器会话提取。

SQLite引擎的公共领域说明见 [SQLite官方版权页](https://www.sqlite.org/copyright.html)。
