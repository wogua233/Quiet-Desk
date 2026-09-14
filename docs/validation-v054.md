# 静隅 0.5.4 文章数量与序号

2026-09-14，Windows 本机，Release / win-x64 自包含发布。

## 修改

- 旧版底部数字其实是当前页数量；现明确显示筛选总数和本页数量。
- 总数与分页查询共用 SQL 条件，在同一数据库事务快照读取，不通过加载全量 Article 对象计数。
- 标题前显示筛选排序后的连续序号，跨页延续，每页最多 200 条；序号不保存到文章 JSON，不进入 AI 内容或缓存。
- 首末页禁用无效翻页；筛选结果减少时回收到有效末页。空结果为 0 / 0。
- 仅未读模式暂时保留正在阅读的已读项时，以“阅读中”标识，不混入未读数量或分配虚假序号。

## 验证

发布成功，91 项 PASS，退出码 0，详见 [原始检查记录](validation-v054-checks.txt)。分页数据集含 401 篇已订阅文章及 1 篇未订阅文章，检查第一页 1–200、第二页 201–400、末页 401、总数／本页数量、未读过滤、收藏跨日期计数、失效页码恢复、倒序重排、空结果、序号不持久化。

阅读布局生成 760×560 下 100%、125%、150%、200% 及 520×560 下 100% 离屏图；实际查看了 760 和 520 的 100% 图，数字和标题可见，底部数量未遮挡正文。这是布局渲染检查，不是系统 DPI 或实际点击验收。

本轮未调用收费 AI、未修改用户数据、未替换正在运行的软件实例。没有进行新的音乐长测，未声称实际翻页鼠标交互或所有 DPI 实机验证已通过。

```powershell
./scripts/build.ps1 -Publish
./artifacts/QuietDesk-v0.5.4-win-x64/QuietDesk.exe --reading-test --out D:/Games/music/artifacts/v054/reading-tests.txt
./artifacts/QuietDesk-v0.5.4-win-x64/QuietDesk.exe --reading-test --layout --out D:/Games/music/artifacts/v054/reading.png
./scripts/package.ps1
```
