# Windows 安装与升级（0.5.6 Beta）

推荐使用 `QuietDesk-0.5.6-beta-Setup.exe`。简体中文向导，默认安装到当前 Windows 用户的 `%LOCALAPPDATA%\Programs\QuietDesk`，创建桌面及开始菜单快捷方式。运行库、18 种离线声音和已列明许可的解码器随包提供，不需要另外安装 .NET，不需要管理员权限。

安装前从静隅托盘菜单选择“退出”；安装器会检测已运行实例并提示，不强制结束音乐或覆盖运行中的程序。安装完成后从快捷方式启动。默认不开机启动、不自动播放音乐；按键轻音效默认开启，设置中可以关闭。0.5.6 首次升级将旧配置的轻音效开启一次，此后保存的关闭选择会保留。

用户数据仍位于 `%LOCALAPPDATA%\QuietDesk`，与程序安装目录分开。升级保留声音组合、收藏、阅读数据库和当前 Windows 用户加密的服务密钥。安装包不包含用户数据；换电脑不会带走原电脑的 DeepSeek 或其他 API 密钥，需要在新电脑自行配置。

通过 Windows“已安装的应用”中的“静隅 QuietDesk”卸载。卸载只删除安装器清单内的程序文件、快捷方式和该应用的卸载注册项，保留用户数据及安装目录里的未知文件；不删除导入的本地音乐或 PDF。卸载前同样需要退出应用。

## 开发者构建

使用 NSIS 3.12 官方便携 ZIP（[下载入口](https://nsis.sourceforge.io/Download)、[版本说明](https://nsis.sourceforge.io/Docs/AppendixF.html)），解压到 `.tools/nsis-3.12`，不要求安装编译器。此轮 ZIP SHA256：`56581f90db321581c5381193d796fffcf2d24b2f8fed2160a6c6a3baa67f2c4f`。NSIS 许可随附于 [licenses/NSIS-LICENSE.txt](licenses/NSIS-LICENSE.txt)；安装器脚本保存在 `scripts/installer.nsi`。构建检查使用 Python 3；不需要在最终用户电脑安装 Python。

```powershell
./scripts/build.ps1 -Publish
./scripts/package.ps1
python scripts/build-installer.py
```

输出位于 `artifacts/QuietDesk-0.5.6-beta-Setup.exe`，同时提供便携包 `QuietDesk-0.5.6-beta-win-x64.zip` 和源码包 `QuietDesk-0.5.6-beta-source.zip`。Python 不在 PATH 时可为 `package.ps1` 传入 `-Python <python.exe 路径>`。

`build-installer.py` 在压缩前对发布目录执行凭据检查，生成精确安装／卸载清单和每文件 SHA256。`package.ps1` 同样检查便携包和源码包的暂存内容。检测到用户数据库、配置、日志、证书私钥、明文服务密钥样式或非空 DPAPI 配置值时中止打包。它们不会读取本机实际的 API 配置。检查程序保存在 `scripts/audit-release.py`，合成凭据回归测试为 `python scripts/test-release-audit.py`。

图标使用 `python scripts/make-icon.py` 重建，需要 Pillow；SVG、PNG、ICO 均为项目原创，灵感来自 `Re[ψ(x)] = exp(-x²/2σ²) cos(kx)`，没有使用外部标志或字体素材。桌面计时为静态 WPF 矢量阴极数字，仅剩余时间变化时重绘，不使用辉光模糊滤镜或持续动画。

当前安装包未使用商业代码签名证书签名。构建和卸载验证范围见本版报告；不把无签名或首次下载的 Windows 提示当成安装失败，也不要求关闭系统保护。
