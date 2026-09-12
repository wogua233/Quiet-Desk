# Quiet Desk / 静隅 — Sound credits

The six sound recordings are distributed separately from application code, under their original licenses below. No endorsement by the authors is implied.

Files were obtained from Blanket, commit `9d229d2be7cb6619135d55ff9e49926e40298686`, at:
https://github.com/rafaelmardojai/blanket/tree/9d229d2be7cb6619135d55ff9e49926e40298686/data/resources/sounds

Blanket's sound license manifest:
https://github.com/rafaelmardojai/blanket/blob/9d229d2be7cb6619135d55ff9e49926e40298686/SOUNDS_LICENSING.md

| Pack file / display name | Original author | Upstream editing | Original source | License |
|---|---|---|---|---|
| rain.wav / 细雨 | alex36917 | Porrumentzio | https://freesound.org/people/alex36917/sounds/524605/ | CC BY 4.0 |
| stream.wav / 溪流 | gluckose | none listed | https://freesound.org/people/gluckose/sounds/333987/ | CC0 1.0 |
| waves.wav / 海浪 | Luftrum | Porrumentzio | https://freesound.org/people/Luftrum/sounds/48412/ | CC BY 4.0 |
| wind.wav / 微风 | felix.blume | Porrumentzio | https://freesound.org/people/felix.blume/sounds/217506/ | CC0 1.0 |
| birds.wav / 森林 | kvgarlic | Porrumentzio | https://freesound.org/people/kvgarlic/sounds/156826/ | CC0 1.0 |
| fireplace.wav / 炉火 | ezwa | none listed | https://soundbible.com/1543-Fireplace.html | Public Domain |

CC BY 4.0: https://creativecommons.org/licenses/by/4.0/ — credit, source, license link and changes retained here.

CC0 1.0: https://creativecommons.org/publicdomain/zero/1.0/

Quiet Desk modifications: first up to 60 seconds selected; converted to stereo 44.1 kHz / PCM 16-bit WAV; one-second end-to-start crossfade; level normalization. No generative replacement sounds. The script `scripts/prepare-sounds.py` reproduces this conversion. Input/output hashes and numerical checks are recorded in `docs/sound-validation.json`.

The forest entry is a bird recording; the fireplace entry is a fireplace recording, not an outdoor campfire. These labels describe the actual bundled recordings.

## 0.2 新增声音

以下录音同样独立于应用代码分发；许可适用于各自声音文件。图书馆录音为 CC BY-NC 4.0，仅供非商业使用。本版本面向个人工作学习；商业再分发前必须移除或另行取得该录音授权。

| 文件 / 名称 | 作者 | 原始来源 | 许可与处理 |
|---|---|---|---|
| windowrain.wav / 窗边雨 | alex36917 | https://freesound.org/people/alex36917/sounds/524605/ | CC BY 4.0；同一雨声录音的低通处理版本，模拟隔窗听感 |
| storm.wav / 远雷 | digifishmusic | https://freesound.org/people/digifishmusic/sounds/41739/ | CC BY 4.0；经 Blanket 编辑；低通、软压缩、降低响度 |
| night.wav / 夏夜虫鸣 | Lisa Redfern | https://soundbible.com/2083-Crickets-Chirping-At-Night.html | Public Domain |
| cafe.wav / 咖啡馆 | stephan | https://soundbible.com/1664-Restaurant-Ambiance.html | Public Domain；原录音为餐厅背景，低通处理 |
| train.wav / 列车 | SDLx | https://freesound.org/people/SDLx/sounds/259988/ | CC BY 3.0；经 Blanket 编辑 |
| leaves.wav / 树叶沙沙 | o_ciz | https://freesound.org/people/o_ciz/sounds/475448/ | CC0 1.0；公开 HQ MP3 预览转码 |
| keyboard.wav / 轻键盘声 | Sorinious_Genious | https://freesound.org/people/Sorinious_Genious/sounds/561102/ | CC0 1.0；公开 HQ MP3 预览、低通、降低响度 |
| fan.wav / 风扇 | Rvgerxini | https://freesound.org/people/Rvgerxini/sounds/546174/ | CC0 1.0；公开 HQ MP3 预览，短循环交叉淡化后重复 |
| library.wav / 图书馆 | lwdickens | https://freesound.org/people/lwdickens/sounds/263502/ | CC BY-NC 4.0；公开 HQ MP3 预览、降低响度，仅非商业使用 |
| white.wav / 白噪声 | Quiet Desk 项目生成 | scripts/prepare-expanded-sounds.py | 项目原创程序生成，允许使用、修改与再分发 |
| pink.wav / 粉红噪声 | Quiet Desk 项目生成 | scripts/prepare-expanded-sounds.py | 同上，频域整形，固定随机种子 |
| brown.wav / 棕噪声 | Quiet Desk 项目生成 | scripts/prepare-expanded-sounds.py | 同上，频域整形，固定随机种子 |

新增录音统一转换为 44.1 kHz 双声道 PCM 16-bit，移除直流分量，最多截取 180 秒，通常使用 2 秒首尾交叉淡化并限制峰值。实际长度、输入及输出 SHA256、峰值和接缝指标见 sound-validation-v02.json。源文件不足 180 秒时保留其实际可用长度，不将重复片段称为新的长录音。

- CC BY 3.0: https://creativecommons.org/licenses/by/3.0/
- CC BY-NC 4.0: https://creativecommons.org/licenses/by-nc/4.0/
- 轻音效由应用内正弦与包络合成，不使用 Apple 或其他系统音效素材。
