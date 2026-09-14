# 0.4.3 JACS订阅地址修复

日期：2026-09-14。

用户在Edge打开原订阅地址得到404，促使本轮重新核对ACS官方目录。
此前程序请求旧地址得到403，不能仅据此认定机构权限不足。

## 官方来源
ACS当前官方订阅目录：https://pubs.acs.org/pages/rss
JACS ASAP：https://pubs.acs.org/rss/jacsat/asap.xml
JACS当期目录：https://pubs.acs.org/rss/jacsat/currentIssue.xml
本软件采用ASAP。旧action/showFeed地址不再使用。

## 实测
新ASAP地址本机普通请求返回HTTP200，XML包含16个item，并有直接可读摘要。
真实XML通过软件解析验证（DOI、标题和可用摘要），无需登录或浏览器Cookie。
33项阅读测试通过，包括旧JACS订阅迁移保留勾选状态、清除旧ETag/Last-Modified及失败退避；用户自定义URL不会被覆盖。
发布构建通过；不改变音乐或API额度，未使用付费API。
普通文章和收藏数据保留，升级后选中JACS订阅即可由后台获取新地址。
目录16条不等于完整历史或每日数量；具体网络和来源后续状态可能变化。
软件自身ReadingContent HTTP客户端另行实测：新地址HTTP200，16条解析成功，首篇摘要1285字符；结果见reading-sources-v043.json。
