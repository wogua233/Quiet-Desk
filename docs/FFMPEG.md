# FFmpeg 解码器与许可

静隅通过独立子进程使用 FFmpeg；PCM 经标准输出进入有界混音缓冲，不链接到播放器进程。仅 HLS/AAC 直播启动解码器，停止、取消和退出时终止并回收子进程。

- 固定版本：N-126497-g5b614efc7e-20260911。
- 二进制：BtbN `autobuild-2026-09-11-13-20` 的 `win64-lgpl-shared` 变体；下载地址及 SHA256 见 `ffmpeg-manifest.json`。
- 核验：`ffmpeg -L` 声明 LGPL 3 或更新版本；构建启用 `--enable-version3 --enable-shared`，没有 `--enable-gpl` / `--enable-nonfree`。完整输出见 `ffmpeg-license-check.txt`、`ffmpeg-build.txt`。
- 原始程序未修改，DLL 名称保留。允许替换为兼容的自行构建版本；播放器不对解码器施加逆向工程限制。
- `Assets/FFmpeg/LICENSE.txt` 为 LGPLv3，`COPYING.GPLv3` 为其引用的 GPLv3 正文，`FFmpeg-LICENSE.md` 为该提交的许可说明。
- 对应 FFmpeg 源码随包位于 `docs/ffmpeg-source.zip`，提交 `5b614efc7e`；构建配方 `docs/ffmpeg-build-recipes.zip`，提交 `cc8f0958be119db774cdaf6c50065651a4901e72`。配方 `scripts.d` 列出依赖的源地址及构建配置，归属各原作者。

使用构建配方复现需 Linux/bash/Docker，按照其中 README 执行 `./makeimage.sh win64 lgpl-shared` 和 `./build.sh win64 lgpl-shared`。复现 FFmpeg 时须固定到上述源码提交，不使用 master 的新内容。正常构建静隅无需重编 FFmpeg，源码包已附经校验的运行文件。

上游每日发行有保留期限；下载脚本在文件消失或哈希不符时明确失败，不自动改用 latest。

参考：[FFmpeg 许可说明](https://ffmpeg.org/legal.html)、[对应源代码](https://github.com/FFmpeg/FFmpeg/tree/5b614efc7e)、[固定发行页](https://github.com/BtbN/FFmpeg-Builds/releases/tag/autobuild-2026-09-11-13-20)。
