# MusePSona 项目解析树

MusePSona 把官方 Muse Dash 谱面从 bundle / CSV 接到可编辑的 CustomAlbums **MDM**。谱面编辑器在并列目录 **`MusePSona 编辑器`**；本目录只保留自制谱解析/导出工具和 **Unity 游玩端**。编辑器打出的 `.mdm` 已能在 `PlayClient` 里正常播放（F8 Spine 外形含大型 0A/0B、**x01 Boss 本体** / 手动 DFJK）。

## 项目树

```
MusePSona/
├── 一键启动解析.bat              # ASCII 包装 scripts/launch_parse.py（UTF-8 菜单，避开 cmd 中文乱码）
├── 自制谱.bat                    # ASCII 包装 scripts/launch_editor.py（拷到 LocalAppData 再开编辑器）
├── decoded_maps/                 # map_decoder 唯一 CSV 导出目录（生成物，不提交）
├── 规则解析/
│   ├── musepsona项目解析树.md
│   ├── 官谱MDM完整生成方案.md    # MusicInfo 星级、delay、编辑器契约、缺口
│   ├── 自制包飞近规则.md        # 本机 CustomAlbums 认 0S；chart.json 输家；摆锤突击者飞近统计 / 同拍异速
│   ├── 官谱事件还原思路.md      # 通道 15：type0 / ≥9 脚本演出
│   └── mdm物件转化词典.md       # ibms → MDM；§1.3e 背景（scene_01 方块墙/地面齿轮、scene_02 弹窗与爱心店 Sliced、scene_03 井/火把/`s03_light` 固定、scene_05 无 Light 层、scene_06 鱼错开/`gradient` 固定、scene_07 过山车/呼吸灯/摩天轮/远光、scene_08 冰块父曲线/Mid 镜头 Y/cattle 钟楼/中景循环宽 62.1、scene_09 远景分层 48.14）；§1.4 骨骼；§2.1 Unity Boss 本体已生成
├── scripts/
│   ├── _paths.py                 # 游戏目录 / 提取_归类后 / 自制谱 / 导出 / PlayClient song.mdm
│   ├── launch_parse.py           # 一键启动解析.bat：UTF-8 菜单，[1] bundle→MDM（点长0F不进13/14），[4] 对照官包
│   ├── launch_editor.py          # 自制谱.bat：拷 LocalAppData 再开编辑器
│   ├── map_decoder.py            # 保留：music_*.bundle → 元数据/CSV
│   ├── official_to_mdm.py        # 权威：music_* + song_* + notedata → MDM
│   ├── csv_mmg_convert.py        # 兜底：已导出 CSV → MDM；BMS 写出被官方管线引用
│   ├── chart_project.py          # 自制谱工程：新建 / 预检 / 打包到 导出
│   ├── extract_md_sprites.py     # 可选：gamescene_01–12/common → sprites/scene_XX/ 静图主体
│   ├── extract_md_scene_bg.py    # 可选：scene_XX 预制 Bg/Near/Mid/Far → sprites/scene_XX/bg/（scene_01～03、07、08 见词典 §1.3e）
│   ├── extract_md_spine.py       # 可选：F8 .skel+.atlas+png → StreamingAssets/MdLooks/spine（gitignore）
│   ├── samples/mopemope_map1.csv # 31 列官谱 CSV 样例（csv2mdm 兜底用）
│   ├── tools/                    # 一次性分析/修补（scene_02/06/08、摆锤裁切）
│   └── utils/
│       ├── verify_official_speed.py   # 校验官谱 0S 速度档与逐音符 speed
│       └── verify_umpopoff_speed.py   # 同上，umpopoff 专用
├── 自制谱/                        # 自制谱工作目录（每曲一文件夹：info.json + map*.bms + 媒体）
├── 导出/                          # MDM 落点（官谱启动器与编辑器共用；桌面写失败时见 LocalAppData）
├── PlayClient/                    # 自制谱 Unity 游玩端（读 StreamingAssets/Charts/song.mdm）
│   ├── MusePSona.Play.Runtime/    # Flight / BossTimeline / AnimBinding（sync 进 Unity Plugins）
│   ├── MusePSona.Play.Tests/      # RuntimeTests：近战 hurt、17 后不自动回场、FalseLeave
│   └── Unity/Assets/Scripts/
│       ├── Play/                  # PlayDirector **partial**（类型名不变，进 Play 仍 AutoMount）
│       │   ├── PlayDirector.cs         # 会话：加载 MDM、时钟、Update、切图与共用状态
│       │   ├── PlayDirector.Stage.cs   # 相机、占位车道、判定圈
│       │   ├── PlayDirector.Input.cs   # DFJK、换谱换歌
│       │   ├── PlayDirector.Actors.cs  # 飞近物件实例、资源、渲染层
│       │   ├── PlayDirector.ActorSpine.cs     # Spine 飞近、命中/离场、0E 连线定位
│       │   ├── PlayDirector.ActorPlacement.cs # 通用物件、钟摆、静图轨道摆放
│       │   ├── PlayDirector.HoldRendering.cs  # Hold 头尾星、长条网格、裁切/透明度
│       │   ├── PlayDirector.Boss.cs    # x01 本体
│       │   ├── PlayDirector.Combo.cs   # 连击 HUD
│       │   ├── PlayDirector.Hits.cs    # 判定字与打击表现
│       │   ├── PlayDirector.Fx.cs      # 谱面屏幕事件
│       │   ├── PlayDirector.Hud.cs     # IMGUI 状态栏 / 难度钮
│       │   ├── PlayDirector.Girl.cs    # 角色资源、轨道高度、跳跃/滞空状态
│       │   ├── PlayDirector.GirlHold.cs    # Hold、双押分组、副轨虚影
│       │   └── PlayDirector.GirlActions.cs # 手动输入、命中与攻击/受伤动画
│       ├── MdSpine.cs             # F8 3.8.99 直读
│       ├── MdSceneBg.cs           # 分层背景
│       └── MdLooks.cs             # 静图 / 判定圈贴图键
│   └── Unity/Assets/StreamingAssets/MdLooks/ # 本机游戏素材（.gitignore，不提交/分发）
│       ├── sprites/               # extract_md_sprites.py --all：scene_XX 静图 + manifest.json
│       └── spine/                 # extract_md_spine.py：.skel + atlas + png + index.json
```

并列目录 **`../MusePSona 编辑器/`**：WPF 源码 + `dist\` 发布。`打包到工具合集.ps1` 刷新 `dist\`（exe）和 `dist\lib\`（依赖 DLL / JSON）。`启动编辑器.bat` / `启动MusePSona编辑器.bat` 会先把 `dist` 拷到 `%LOCALAPPDATA%\MuseDashChart\run` 再启动（避开桌面目录的低完整性，否则写 `导出\` 会被系统拦）。环境变量 `MUSEPSONA_EDITOR_ROOT` 指向编辑器根，路径仍落到本仓库 `自制谱/`、`导出/`、`PlayClient/`。

`一键启动解析.bat` **只做官谱**：`[1]` 指定 `music_*.bundle` → MDM（先 list 再选 map；1 槽点长 0F 转 `24` 星统计 `star24=`，同刻撞键丢掉；type7 `23` 仍是音符；length=0 的点长 0F 不进 13/14 统计 `drop0F=`）`[2]` `map_decoder list` `[3]` 打开 `导出\` `[4]` 自制谱对照官包（含 0F 残渣叠键）。自制谱不要走这个 bat 来转录。

`[2]` 仍走 `map_decoder.py` 导出 CSV，不改其列格式。`[1]` 默认走 `official_to_mdm.py`。完整生成方案见 `规则解析/官谱MDM完整生成方案.md`。

---

## 官谱转化 MDM 实现思路

权威输入是 **游戏 bundle**，不是 31 列 CSV。CSV 方案（`csv_mmg_convert.csv2mdm`）只作已导出表的兜底；格子换算、通道 13/14/53/54、zip 打包这三块被官方管线直接引用。

以 mopemope 为例，游戏把一首歌拆成两个 Addressable 包：

| bundle | 内容 | 角色 |
|--------|------|------|
| `music_mopemope_assets_all.bundle` | 4 个 `StageInfo`（map1–4）+ 整曲 `AudioClip mopemope_music`（111s / 44.1k 立体声） | **谱面 + 正曲** |
| `song_mopemope_assets_all_<hash>.bundle` | `Texture2D/Sprite mopemope_cover`（440×440）+ `AudioClip mopemope_demo`（20.7s） | **封面 + 试听** |
| `config_others_assets_notedata_*.bundle` | `NoteConfigData` JSON（uid → type / pathway / **ibms_id**） | **模板表** |

`StageInfo` 字段（Unity MonoBehaviour，解码器已读）：`bpm`、`difficulty`、`music`、`scene`、`md5`、`mapName`、`sceneEvents`（mopemope 为空列表）、`serializationData.SerializedBytes`。

### 数据流

```
music_<id>_assets_all.bundle          StageInfo.SerializedBytes
        │                             MusicData.tick + 内嵌 MusicConfigData
        │                               time / note_uid / length / pathway
        ▼
official_to_mdm.parse_music_data
        │  join notedata.uid
        │  ibms_id → BMS 2 字符（缺省才回退 CSV 的 type 表）
        ▼
csv_mmg_convert.write_bms / info.json   ← 引用 CSV 方案的写出层
        │  同曲 map1..mapN 打进一个 MDM
        │  song_* 的封面 → cover.png
        ▼
<曲名>.mdm  →  谱面编辑器 ChartIo
```

`map_decoder.py` **保留**：`list` / `export` 仍只解 `tick` + 尝试 `obj_id`，用于对账和 CSV 中间数据。它不解 `note_uid`，不能当编辑器工程源。

### 1. 原版放置记录（SerializedBytes）

Sirenix 二进制里每条 `MusicData` 以 utf-16 `tick` 为锚（Decimal 秒，与 CSV `time_sec` 一致；mopemope_map1 另有一条 tick=0 的空占位，转录时丢掉）。窗口内再取：

- `note_uid`：utf-16 六位模板键（`000301`、`051101`…），**不是** CSV 的 `obj_id`
- `length`：Decimal 秒；点击为 0，长按/连打 > 0
- `pathway`：Odin 常省略默认 0，只有空中才写出；缺省用模板 `NoteConfigData.pathway`

这与 `音符配置参考\Muse Dash音符配置说明.md` 的 `MusicConfigData` 六字段一致。运行时 `is_air` / `is_long_pressing` 不进 bundle，由模板 + length 推导。

### 2. 模板 join → BMS

`NoteConfigData.ibms_id` 已是编辑器要的 2 字符（mopemope 实测 `#00113:00230023` 这类官方 id，而不是 CSV 兜底的全盘 `01`）。

| 游戏 `type` | 枚举 | 通道 | 备注 |
|-------------|------|------|------|
| 1 Monster | 用 ibms_id | 14 / 13 | 点击 |
| 2 Block | ibms_id | 14 / 13 | 齿轮 |
| 3 Press | `0F` 或 ibms | 54 / 53 | length 头尾两点 |
| 4 Hide | ibms | 14 / 13 | ghost |
| 5 Boss | ibms | 14 / 13 | |
| 6 Hp | 常为 `22` | 14 / 13 | 红心；Unity common `0002_hp` |
| 7 Music | ibms `23` | 14 / 13 | 音符条；官谱路径保留（Unity `0003_score`）。CSV 兜底路径在 `SKIP_TYPES` 中跳过编辑器键 `10` |
| 8 Mul | `0G` / **`16` / `17`**（抄 ibms，禁止一律 `0G`） | 点长走 14/13；length>0.02 走 54/53 | 普通连打 vs Boss 连打 |
| 0 / 9–17 | 有 ibms 才写 | **15** | 演出/脚本；无 ibms 的 type 0 丢掉 |

格子：先 **检索整格设备延迟** D=`n×(240/BPM/192)`（拍内相位决胜，秒会吸到整格；BPM160 的 0.35 设置 → 45格=0.3515625s），`tick−D` 再吸到 192。通道 15 与 `0S` 同一轴。`--capture-delay auto|0|N格|秒`。`info.json.delay` 只表示整格吃音偏移。

### 3. 打包思路：元素归进 MDM，曲目文件夹只落这一包

CustomAlbums 的 `.mdm` 就是归类容器（`MdmPacker.PackNames`）。**正曲、开头试听、封面和所选谱面打进同一个 MDM，每种媒体只出现一次**，不随 map 份数翻倍。

```
music_*.bundle          →  StageInfo 谱面 + AudioClip 正曲
song_*_assets_all_*.bundle →  cover + AudioClip 试听（demo）
notedata                →  ibms_id / type
        │  ffmpeg：PCM WAV → libvorbis music.ogg / demo.ogg
        ▼
{曲名}/                          # 磁盘落点，方便打开看
  cover.png
  music.ogg                      # 正曲，一份
  demo.ogg                       # 开头试听，一份
  {曲名}.mdm                     # zip 内同样这些文件各一份：
      info.json
      chart.json                 # 有同刻输家才写；CustomAlbums 忽略
      mapN.bms …                 # 所选难度合并
      cover.png
      music.ogg
      demo.ogg
```

- 只选 map1 → MDM 里只有 `map1.bms`，音频封面仍各一份  
- 选 1,3 或全部 → 多个 `mapN.bms` 合并进**这一个** MDM，媒体不复制  
- 无 ffmpeg 时正曲/试听无法写成 ogg，只留文件夹里的 WAV，MDM 不塞假后缀  

启动器 `[1]` 先 `list` 再问 map 编号。编辑器打开该 `.mdm` 即可读到封面和音频。

### 4. 命令

```bash
# 先解析列出难度
python scripts/official_to_mdm.py list "D:\steam\...\music_mopemope_assets_all.bundle"
# 导出到曲目文件夹（--maps 省略 = 全部合并）；启动器 [1] 用 --out/--maps
# 输出父目录：--out 或位置参数均可
python scripts/official_to_mdm.py export "D:\steam\...\music_mopemope_assets_all.bundle" --out 输出父目录 --maps=1
python scripts/official_to_mdm.py export "D:\steam\...\music_mopemope_assets_all.bundle" 输出父目录 --maps=1,3
# --denoise N：间隔小于 N 拍的变速点合并，压掉 Hidden 谱的快速闪烁
python scripts/official_to_mdm.py export "<bundle>" --out 输出父目录 --denoise 2

# 自制 MDM / 导出文件夹 对照官包（第二参数可省略，按文件夹名搜 music_*_assets_all.bundle）
python scripts/compare_mdm_official.py 导出/change_the_game
python scripts/compare_mdm_official.py 某谱.mdm "D:\\steam\\...\\music_change_the_game_assets_all.bundle" --json
# 只对照自制有的 map。键从 mapN.bms 读（**只有 53/54** 两两收条；13/14 上的点长 0F/0G 不要收成跨曲长条）；mdchart 只补飞近档。启动器 [4] 同功能。

# 保留：只解析、导出 CSV（不传输出目录时默认写仓库根 decoded_maps）
python scripts/map_decoder.py selftest            # 解码 cthugha 与基准 CSV 对比
python scripts/map_decoder.py list [bundle目录]
python scripts/map_decoder.py export [bundle目录] [输出目录] [--limit N] [--dict 文件 | --no-dict]

# 兜底：已有 31 列 CSV
python scripts/csv_mmg_convert.py csv2mdm scripts/samples/mopemope_map1.csv [out.mdm]
python scripts/csv_mmg_convert.py batch <CSV目录> [输出目录]       # 别名 batch_mdm
python scripts/csv_mmg_convert.py csv2mmg | mmg2csv | batch_mmg ...  # MMG 二进制互转
```

导出结果：`<父目录>/<曲名>/` 下的 `info.json`、`mapN.bms`、`chart.json`（BMS 表达不了的飞近速度覆盖）、`cover.png`、`music.ogg`、`demo.ogg` 和 `<曲名>.mdm`。

启动器 `[1]`：指定 `music_*.bundle`（自动配 song_* 封面）。不再提供 mopemope 一键样例项。CSV 兜底仍可用 `csv_mmg_convert.py`。

### 5. 和编辑器的契约

- 打开：`ChartIo` → `MdmPacker.Unpack` → `FolderChart.Load`（音符吸到 192 槽）
- 保存：`FolderChart.Save`（先 192 槽）+ `MdmPacker.PackFolder`
- 不追求和官方 SerializedBytes 字节级一致；id 用模板 `ibms_id`；时间永远是 **0.0 对齐的 192 槽**，不把设备延迟写进音符
- `info.json.delay` 只表示整格吃音；游戏内个人延迟由 `official_to_mdm.py` 检索成 **n×(240/BPM/192)**（不要把 0.35 填进 Delay 框）
- 网格每拍 4/8/12/16/24/48（48=整槽；已去掉不能整除 192 的 32）
- 飞近：编辑器存**逐音符速度**（每颗可玩键 `ApproachRate` 1/2/3）；`0S` 导入 → 逐音符（`ApplyMarkersToNotes`），导出 → 逐音符台阶 + `chart.json` 输家；变速事件 `0X/0Y/0Z` 是批量改档标记（`StampNotes` 同步）；`0S` 与通道 15 同一条 192 轴
- 齿轮 / 条内键：`notes_to_taps_holds` 只丢掉长按/连打**中间刻**的 `0F`/`0G`，条内 `0H`、`23`、`0D`、`0C`、`0E` 等全留（旧逻辑只放行 `0H`：12 张谱丢 509 个齿轮，reanimate map4 少 180+ 颗叠键）。Boss 齿轮 `18` 写在长按头类型里仍可能被丢掉。**1 槽点长 `0F`** 转 `24`（`02_top` 星，导出 `holdStar=`）。type7 `23` 保持 `0003_score`，不要画成长条头星。length=0 的点长 `0F` 不进 13/14（导出 `droppedHoldTaps` / `drop0F=`；`write_bms` 二次拦截）

---

## 依赖

### Python 3.8+（解码需 3.14+ 与 UnityPy）

```bash
pip install UnityPy numpy
```

转录 MDM：`official_to_mdm.py` 需要 UnityPy（读 bundle）；CSV 兜底只用标准库。

### 谱面编辑器

需要 .NET 8 Windows：工具合集根 `启动MusePSona编辑器.bat`，或 `MusePSona 编辑器\启动编辑器.bat`（拷到 `%LOCALAPPDATA%\MuseDashChart\run` 再跑 exe）。改代码后在编辑器目录跑 `打包到工具合集.ps1`（`dotnet publish` → `dist\` + `dist\lib\`）。不要从桌面 `dist\` 直接双击 exe：该目录带 **Low Integrity**，写 `MusePSona\导出` 会被「另存为无权限 / Access denied」。

命令行工具 `mdchart`（`src\MuseDashChart.Cli`）：`new`、`pack`、`unpack`、`json`、`selftest`、`inspect`、`lint`、`bpm`、`bpm-pcm`（后两个不在帮助文本里）。`lint <文件夹|.mdm>` 跑与「导出 MDM」相同的预检 + 做谱规则，按 `error` / `warn` / `hint` 逐行输出（MDM 解到临时目录，不在原处留 `_open`）。旧参数 `--no-flyin` 已无作用，传了也不报错。

导出检查（`ExportPreflight` → `ChartLint`）。**不在预检里探测目标目录是否可写**。打包策略见下节「导出策略」。谱面规则仍只警告/提示，不拦导出（缺 Scene/PLAYER/BPM/键位仍拦截）。提示单列在「提示（不影响游玩）」下：

时间轴契约：导入/保存/导出一律 192 槽（0.0 对齐）。Delay 只加减整格吃音，不写进音符。设备延迟由官谱转录检索。

| 级别 | 规则 |
|---|---|
| 警告 | 齿轮 `0H` 在同轨长按/连打中间且对侧轨没有长按（官谱从不出现；对侧按住时正常，不报） |
| 警告 | 齿轮与同轨键同一拍（官谱全库仅 12 例；按 192 槽） |
| 提示 | 空中轨 `0I` 档 1（官谱没有） |

已移除（官谱本身大量使用，报出来全是噪音）：同刻混速警告、摆锤/突击者「建议带飞近」提示（连同主页勾选框；旧 `settings.json` 里的 `FlyInHints` 键读入时忽略）、齿轮档与前后键都不同提示。同刻混速仍按 0S 规则写出（幽灵让路→多数票→平票取小，少数派进 `chart.json`），只是不再提示。无提示时导出对话框不出现「提示」段。状态栏悬停有一行当前规则说明。下拉框聚焦时空格/方向键不再抢去播放或挪物件。

齿轮 `0H` 写在 13/14，长按在 53/54，编辑器保存/导出 MDM **不会**丢掉同轨长按内部的 `0H`（与 `official_to_mdm.py` 一致）。Boss 齿轮 ibms `18` 走轨道通道，不是 BMS 事件通道 `18`；调色板「Boss 齿轮」可正常放置。打包白名单：`cover.png/gif/jpg/jpeg` 与 **仅** `music/demo.ogg|mp3`。wav/flac 可留在工作目录给编辑器预览，**不会**打进 MDM；缺 ogg/mp3 时预检写「CustomAlbums 只认 ogg/mp3，请转码后再导出」（已有其它错误则升为错误）。`FolderChart.Save` 会删掉已移除难度的 `mapN.bms`。写出 0S 时 `StampNotes` 只作用于副本，不改编辑器内存。临时 zip 直接在可写目录建 `.tmp`（不再先写探测文件）。成品 `File.Copy` 到 `导出\<id>\<id>.mdm`；失败则保留 LocalAppData 那份。**导出 MDM 成功只更新状态栏，不再弹成功窗。** 解包先写到 `工作目录\_unpacking\` 再替换；占用则重试，仍失败则写 `文件名.new` 并弹出新旧混杂警告。`mdchart lint/inspect` 解 MDM 到临时目录后删除，不留 `_open`。

启动第一屏没有「打开自制谱」按钮；用 **打开文件夹…** 或顶栏 **打开**（初始目录 `自制谱\`）。**导出 MDM** 不再弹出系统另存为。

### 导出策略（自制谱 MDM）

权威成品只有一份 zip：`info.json` + `map*.bms` + `chart.json`（CustomAlbums 忽略）+ 白名单媒体。不写系统另存为，不在桌面目录探测 `_pack`。

| 步骤 | 路径 | 失败时 |
|------|------|--------|
| 1. 临时 zip | `%LOCALAPPDATA%\MuseDashChart\` 或 exe 旁 `_pack\*.tmp` | 换下一候选目录 |
| 2. 权威 `.mdm` | `%LOCALAPPDATA%\MuseDashChart\export\<id>.mdm` | exe 旁 `_pack\<id>.mdm` |
| 3. 桌面副本 | `MusePSona\导出\<id>\<id>.mdm` | 保留第 2 步，仍算导出成功 |
| 4. 游玩端 | Unity **进入 Play** 时自动取第 2/3 步里**最新** `.mdm` → `StreamingAssets/Charts/song.mdm` | 关掉菜单勾选后需手拷；Play 中占用则先停再 Play |

「导入 Unity」只是立刻写 `song.mdm` 的快捷方式，**不是**导出必经步骤。指定旧谱用 Unity 菜单 **从导出选择 MDM**。`MusePSonaMenu.cs` 必须 `using System;`。

飞近档是逐音符模型：每颗可玩键的 `ApproachRate` 就是它的档（`EffectiveRate`），0X/0Y/0Z 只是批量改档标记，`StampNotes` 写进键里；`At(t)` 取 t 及之前最后一颗可玩键的档；读 BMS 时 0S 直接贴到键上，不生成 0Y/0Z 标记；没有可玩键就不写 0S。


```bash
cd "..\MusePSona 编辑器"
dotnet run --project src\MuseDashChart.Cli -- inspect <谱面文件夹或.mdm>
dotnet run --project src\MuseDashChart.Cli -- lint <谱面文件夹或.mdm>
dotnet test tests\MuseDashChart.Tests
```

测试 **52** 个 `[Fact]` 全过（反射跑测试程序；测试项目没有 `[Theory]`/fixture）。受限沙箱里 `dotnet test` 可能因 testhost 打不开父进程句柄（Win32Exception 5「拒绝访问」）而起不来。

### Unity 游玩端（`PlayClient`）

**已验证**：编辑器导出的 MDM 拷到 `Assets/StreamingAssets/Charts/song.mdm` 后，打开 `Play.unity` 按 Play。F8 Spine 怪物外形（含 scene_03 **0A/0B 大型完整**）沿轨迹飞近判定线；**Boss 本体 x01 已生成**（`BossTimeline` + `MdSpine.LoadBoss`）。默认 **autoplay**；`F1` 随时切换自动/手动，`F2` 手动模式重开，`F3` 自动模式重开，手动判定使用 D/F（空中）与 J/K（地面）。

- Unity **2022.3.62f3c1**（`E:\Program Files\unity\2022.3.62f3c1\Editor`），Hub 打开 `PlayClient\Unity`。Scripting Backend **Mono**，Api Compatibility **.NET Standard 2.1**。不要 IL2CPP 做第一期。
- 插件：`sync_unity_plugins.ps1` 把 netstandard2.1 的 Runtime / Core 拷进 `Assets/Plugins`（`.meta` 必须合法 YAML）。缺 `System.Runtime.CompilerServices.Unsafe` 时 Play 会 FileNotFound。
- `PlayDirector`（`Unity/Assets/Scripts/Play/` 一组 **partial**，类型名仍是 `PlayDirector`；场景 **不序列化** 该组件，进 Play 由 `PlayDirector.cs` 的 `AutoMount` 挂上）：默认 autoplay。飞近 + Spine 外形已通。读 `0S` + `chart.json`。正交相机 **size 3.75**（`Flight.CameraSize`）。空中/地面判定圈已接图集 Sprite（不要整页 atlas；缺包才矢量环）：`HitPointOut` 65×63 三段弧逆时针转（`JudgeOutBox=0.65`）+ `HitPointIn` 22×22 内环（`JudgeInBox=0.22`，禁止 `localScale=1`）；顶点色分轨；击中切 `HitPointInCatch`。打击字 `ScorePerfect`/`ScoreGreat`/`ScorePass`；无 MISS 气泡。`ScorePass` 只对躲避齿轮，字画在对侧轨。改 Runtime 后必须 `sync_unity_plugins.ps1`。长按头尾都计连击、不绑 `0102_air`；条 `*_hold_body` Simple 拉伸，音符 `*_hold_note_up/down` 整张 880×118 overlay（`note_down` 含谱号），挂 Root 不挂 Body，格距 `HoldBarBox×880/118×1.2`，UV 钉完整时长、接住从左裁、2Hz 漂；**未接住的长按不钉判定线**，从头过线起按车道速度飞过并 0.2s 内 alpha→**0.391**（官机 `LongPressHurt`/`SetAlpha`）；接住才 `pinHead`。头尾星 360°/s，绕贴图重心（约在几何中心下方 5.5/149）转；整条钉官机 renderer 高度（空中 y=1.20、地面 −0.95），不走通用轨。条在键下层（`HoldEndBox=1.54`、`HoldBarBox=0.96`）。红心 `22` / 音符 `23` 走 common **`0002_hp` / `0003_score`**（`in_air`/`in_road`/`out`）。带 `shadow` 的飞近物件和 Boss 本体，投影中心对 `ShadowTrackY`（红线下面 0.20，地板上沿中间）；本体留在投影上方。没有投影槽才用判定帧 `BodyOffset` 对 `LaneY`。幽灵判定前 0.15 s 线性渐隐；连打钉在判定线脉动；`1O–1W` 切场景换 `MdSceneBg` 分层背景（Animator 横向循环；`25/26` 显隐、`2N/2O` 冻结、`27/28/29` 上下滚；scene_01 地板 3.84/0.75、方块列竖滚、scene_02 弹窗阶跃/两套 Mid_H1、scene_03 井 flip 与火把见词典 §1.3e）。`Flight.HitFrames`：`13`/`18` = 21/18/15，`14`/`15` 按切景（城堡 31/28/24，scene_01 是 23/20/17），升降怪 30/26/23。直线横向 **`LaneSpeed` = 13.85u / 普通帧时长**（短判定帧同速、更近生成，上下轨对齐）；突击者仍按本键时长走完全程。升降怪 y 全程 ease-out。中线：连打与近战 11/12（弹幕 13/14/15、双押 0E 走本轨）。判定窗仍是练习值。怪物 **Spine 3.8.99** 由 `MdSpine` 直读 F8（`scale` **0.005**，PMA）；**0A=x13、0B=x14**；官中脸走本包 `head_hexie` 网格（不要头盔包框补丁、不要共用图集串绑 `head`）。摆锤 0D/0J 与突击者 0C/0I 走 F8 `in_*`（x15/x16；对侧轨包，不翻转 ScaleY）；判定前不钉 HitX。齿轮 0H/18 走 x03/x09，**无出场**，过线继续 `in_*` / ThroughX，地面对下轨。普通连打 0G 走 x04 `in_down_27/23/20`，钉中线；漏接 ThroughX。双押 0E 走 x05 本轨。F8 `shadow` 投影及 glow/fx/smoke/scrap 等槽都画（不进主体框）。缺包才静图+吊绳 / 方块 / 圆柱。角色战斗 Spine 已接：自动与手动共用原生 hit/press/hurt/double_hit/连打序列，Hold 副轨普通打击走独立 `*_ghost` 包，双押和连打/近战按官机锁中轨。场景 `Play.unity` **不序列化** `PlayDirector` 组件（域重载会打 Unknown），进 Play 由 `AutoMount` 挂上。显示逻辑按职责修改：资源实例 `Actors`、Spine 进出场 `ActorSpine`、普通/钟摆定位 `ActorPlacement`、长条 `HoldRendering`、角色轨道 `Girl`、Hold/虚影 `GirlHold`、角色判定动画 `GirlActions`；判定圈 `Stage`、本体 `Boss`、连击 `Combo`、打击字 `Hits`、按键 `Input`、IMGUI `Hud`。
- **Boss 本体**（词典 §2.1）：`x01` 站位判定线→右缘 **3:1**（`BodyAlong=0.75`），高度用待机 `shadow`→`ShadowTrackY`。图层 **Default 3300/80**，测试角色单独 **Character** 层，闪光 **Overlay**。贴图：x01/`char_*` 先 `Atlas.FlipV`、`_PmaTex=1`；不要把 `ufo_light` 当双押光柱。`1A` 入场、`1B`/`17` 退场、`1L`/`1M` 显隐；`1A` 之前与藏/退后本体不在场，弹幕/`1E` **不**把 x01 召回。`11/12` 冲刺后打中播完整 `hurt`，漏接把 `close_atk` 续播到回场；`16` 有前置，打完从开头播完整 `multi_atk_out`；有长度的 `17` 判定瞬间瞬移连打，打完往右离场，不播 `multi_atk_48_end` 前置。`13/14/15` 飞近时本体 `far_atk_*` 再 `*_end` 0.7s。改 **Runtime DLL** 后才跑 `PlayClient/sync_unity_plugins.ps1`；只改 Unity 脚本停 Play 再进即可。细节只维护词典，此处不复述 clip 名。
- 判定窗口现以官机 `config_others_assets_notedata` 为准：攻击 Perfect ±50ms、Great ±80ms，超过 Late 80ms 立即 Miss；Music/红心到点后接触窗口 160ms。上方长说明中的“判定窗仍是练习值”是旧状态。
- 进入 Play 时 **默认自动导入最新 MDM**（菜单 **MusePSona → 进入 Play 时自动导入最新 MDM** 可关）。
- **不是** 官机 Addressables，`.skel` **不**进 MDM。外形约定：`mdm物件转化词典.md` §1.4；Boss 演出：§2.1。

详见 `PlayClient/README.md`。


## 使用

### 自制谱 → Unity（已通）

```text
启动编辑器.bat（或 启动MusePSona编辑器.bat）
  打开文件夹 → MusePSona\自制谱\<id>\
  导出 MDM     → %LOCALAPPDATA%\MuseDashChart\export\<id>.mdm
                 尽量再拷 MusePSona\导出\<id>\<id>.mdm
Unity Play.unity → 停掉再 Play（自动同步最新 MDM 到 song.mdm）
```

不必每次点「导入 Unity」。指定某一份旧谱时再用 Unity 菜单 **从导出选择 MDM**。

`自制谱.bat` / 根目录 `启动MusePSona编辑器.bat` 走 `scripts/launch_editor.py`：robocopy 到 `%LOCALAPPDATA%\MuseDashChart\run` 再启动（避开桌面 Low Integrity）。`一键启动解析.bat` 走 `scripts/launch_parse.py`（UTF-8 菜单，导出目录用 `_paths.export_dir()`）。

### 官谱 → 编辑器

```text
一键启动解析.bat
  [1] 指定 music_*.bundle → MDM（先 list 再选 map，落到 导出\）
  [2] 列出游戏曲目（map_decoder list）
  [3] 打开 导出\
```

```bash
# 直接给 bundle = export 全部 map
python scripts/official_to_mdm.py "<游戏>\...\music_mopemope_assets_all.bundle" --out 输出父目录
python scripts/map_decoder.py list
python scripts/map_decoder.py export <bundle目录> <输出目录>
```


### Spine 编辑器：版本、授权、导出

改图、导入官机 skel、再导出给 Unity，一律钉 **Spine 3.8.99 Professional**。

| 版本 | 用途 |
|---|---|
| **3.8.99 Professional**（推荐） | 导入 F8、改网格/贴图/皮肤、导出给 PlayClient。MD 物件和角色大量 mesh、权重、`*_hexie` 裁剪，**Essential 改不了** |
| 3.8.75 | 能打开看；**不要导出**。写出的版本号是 `3.8.75`，`SkeletonBinary`/`SkeletonJson` 会抛 `Unsupported skeleton data`。不勾 Nonessential 会 `mesh internal edges lost`（再打开破图、hexie 缺边） |
| Spine 4.x | **不要用**。打开 3.8.99 skel 会一直 MISSING，骨架上也没有 Images |
| Enterprise | 公司多座席才需要 |

店面买的是当前 **Professional** 授权（4.x 页面）；同一序列号在 **Spine Launcher** 里加装 **3.8.99**，默认用 3.8.99 干活。不要把 4.x 当主力去动这些资源。

导出（从 `.spine` 工程，不要用导出的 skel 再「打开」当源文件）：

- Skeleton：**Binary** `.skel`；**勾上 Nonessential data**；Scale **1**（Unity 再乘 **0.005**）。
- Atlas：PMA、Filter Linear、PNG。三件套放一起：`.skel` + `.atlas` + `.png`，另写 `scale.json`：`{"scale": 0.005}`。
- 落到 `PlayClient/Unity/Assets/StreamingAssets/MdLooks/spine/<scene>/<条目>/`。
- 日志里 `JavaSoft\Prefs` error 5、Welcome **410** 可忽略；**不能忽略** `Nonessential unchecked` / `internal edges lost`。

在 **3.8.99** 里看完整物件（路径含中文时仍常读不到图，拷 `E:\md-spine`）：

1. 欢迎页没有导入。先 **新建项目**，再菜单 **导入数据**（不要「打开项目」去开 `0504` 文件夹）。
2. 数据选 `0504.skel`。层级树最下面的 **图片**（不是附件上的「图片路径」、也不是 Spine设置→图形）才是 Images。
3. 附件名带前缀（`0504/images/body`）时，Images 必须是 **包含 `0504` 的那一层**。填 `…\0504` 或 `…\images` 会多一层找不到。
4. 先 `python tools/unpack_atlas.py --root "<scene>/mod/spine"` 把图集拆成散图（编辑器不用整张 atlas）。Images 填 **`E:\md-spine`**（例：`0504\images\body.png`），**取消「限制扫描」** 再刷新。
5. Setup 姿势是散开的；切 **动画** 播 `hurt_*` / `in_*` / `out_p` 才是游戏动作。工程另存 `E:\md-spine\0504.spine`。菜单 **纹理解包器** 可替代 unpack 脚本。


游戏素材（可选）：`python scripts/extract_md_sprites.py [--scene 01 | --all]` 只读本机 `gamescene_XX_assets_all_*` 与 `gamescene_common_assets_all_*`，按 Spine atlas 裁出各物件主体部件，并从图集裁 `HitPointOut/In/InCatch`（禁止整页 atlas），另从 `gamemain` 抽 `ScorePerfect/Great/Pass` 与 Gold 字，直接写到 `PlayClient/Unity/Assets/StreamingAssets/MdLooks/sprites/scene_XX/` 与 `sprites/common/`。`extract_md_scene_bg.py` 另抽对局分层背景到 `sprites/scene_XX/bg/`（layout.json + png，gitignore；scene_01～03、07、08 规则见词典 §1.3e）。只取主体部件，不是完整骨骼姿态。`--all` 覆盖 01–12。atlas 部件名因场景而异（`body`/`body1`/`wooden`、`images_air`/`image_air`），抽取时按前缀回退。**atlas 的 `xy.y` 是页内从上往下**（libGDX）；PNG 若 pad 成 POT，多出来的行在上方，裁切 `y = (pngH - atlasH) + atlasY`，rotate 时先按页内交换宽高。不要用 `pngH - atlasY - h`（左下原点）——摆锤会裁成图集碎块。`wooden` 是侧视木质悠悠球，不是角色脸。`scene_07` 无 wooden。愚人 `gamescene_11` 是 1K 专用（`1110/1111/1112_road` = `40/41/42`），不要抽成双轨标准包。`_cy`/`_fool`/`dave`/`touhou_black` 不是标准 xNN 物件包，不抽。摆锤静图重裁：`python scripts/recrop_pendulum.py`。

物件外观（矢量）：空中青蓝 / 地面粉紫（向 scene_01 配色靠拢），普通怪带高光和朝左的眼睛、按 ibms 分大小（01-03 小、04-09 中、0A/0B 大）；Boss = 紫色大炸弹；判定圈 = 四段白环 + 中心小圈；长按 = 粗胶囊条 + 两端星形；齿轮 = 白色尖齿锯片 + 紫色轮毂；红心带高光、音符为 ♫。幽灵 = 半透明填充 + 虚线圈；摆锤 = 白边圆 + 连到吊点的吊线（吊点在上 0D、在下 0J 倒置）；突击者 = 白边箭头 + 右侧拖尾（拖尾朝上 0C、朝下 0I 倒置），两者沿用轨道色；连打 = 红色白边同心圆；飞近档 2 / 3 的物件左上角常驻 2 / 3 道黄 / 红箭头（与进度条档位标记同色）。图例写在「显示 ID」的悬停提示里。

解析结果已与 `mdchart json` 逐颗比对（chrome_vox 4 难度 + umpopoff，共 3538 颗，时间/长度/通道/ID/轨道/飞近档全部一致）。飞近档速度倍率：游戏内实测为 **1 : 1.158 : 1.333**（普通键判定帧 44/38/33，各档行程 13.85u 只差时间；短判定帧直线同 px/s、缩短行程；摆锤、突击者等另有帧数，见 `mdm物件转化词典.md` §1.2–1.3）。三种模式都已按判定帧/30 定时长（旧的 `RATE_MUL` **1 : 1.5 : 2** 与固定 1.6 s 提前量已删除）。每颗物件按自身档位定速（已在屏上的不随换档变），小节线按所在时刻的 `0S` 档。卷轴 −/+ 只是全局时长倍率。连击规则未改（齿轮/音符/红心不计；`18` 因 `isGear` 也不计连击）。HUD 分类：`categoryOf` 把 `0H` 与 **`18` 都算齿轮**（Boss 计数 0），`0I`/`0J` 算 boss。抓取工具见 MDSceneGrabber README（F8 dump、F9 逐帧、F10 遥测无 20 s 上限、`AfterDumpCommand`、`consolidate.py`、`scene_alias.json`）。


## 路径配置

运行时优先 `scripts\_paths.py`：

- `MUSE_DASH_DIR`：游戏根（含 `MuseDash.exe`）
- `MUSE_DATA_DIR`：`提取_归类后`（含 `csv/`）

官谱 CSV 默认：`%MUSE_DATA_DIR%\csv\`（未设置时由 `scripts\_paths.py` 搜索常见目录）  
模板 bundle：游戏 `StandaloneWindows64\config_others_assets_notedata_*.bundle`


---

## 已知问题与待清理

以下为代码走读记录，尚未修复。已从本表拿掉：同轨长按内 `0H` 被转录丢掉、ChartLint 同刻混速/摆锤突击者飞近/齿轮异档、打包 wav 进 zip、解包直接覆盖、`StampNotes` 改内存、导出留 `_md_pack`、编辑器无发布脚本、Unity 插件 YAML / Unsafe 缺依赖、Play 黑场景、导出目标目录探测误拦。

自制谱 MDM → Unity Spine 预览 **已通**（飞近 + 大型 0A/0B + **x01 Boss**，见上文「Unity 游玩端」）。


### Python 脚本

| 位置 | 问题 |
|------|------|
| `csv_mmg_convert.py` `_sec_to_tick` | 只用表头 BPM 把秒换算成格子，却又写出 `#BPMxx` 变速；变速 CSV 在变速点后的音符时间会错（官谱路径只传单段 BPM，不受影响） |
| `一键启动解析.bat` `CSVDIR` | 已不再从菜单直接 csv2mdm；若手工把 `map_decoder` 的 CSV 喂给 `csv2mdm` 仍会 0 物件（缺 `type` / `length_sec`） |
| `official_to_mdm.py` `_find_ffmpeg` | 相对路径：`FFMPEG` 环境变量、`scripts\ffmpeg.exe`、`MusePSona 编辑器\dist\ffmpeg.exe`（及 `dist\lib`）、`Malody 谱面转换器\ffmpeg.exe`。缺则只留 WAV |
| `utils/verify_umpopoff_speed.py:13` | 写死本机 umpopoff bundle 绝对路径 |
| `official_to_mdm.py` `parse_map_selection` | `--maps` 非法输入直接抛 `ValueError`，`main` 未捕获 |
| `official_to_mdm.py:814` | 多张谱 delay 不同时，取最后处理的那张 |
| 两个 `verify_*` | `approach_at` 与 `official_to_mdm.approach_at` 语义不同 |

`decoded_maps` 已统一为仓库根目录的单一出口；旧的 `scripts\decoded_maps` 与演示导出均已清理。该目录只存放本机可重建的 CSV，不提交到 Git。

### 谱面编辑器 / 遥测缺口

| 位置 | 问题 |
|------|------|
| 桌面完整性 | `工具合集` 曾标 Low Integrity，exe 直接从 `dist\` 启动写不了 `导出\`；须走 `启动编辑器.bat` 的 LocalAppData 拷贝 |
| `MdmPacker.Unpack` | 目标占用：重试后仍失败则写 `文件名.new` 并弹新旧混杂警告（原文件未覆盖） |
| CustomAlbums zip | **wav/flac 不打进 MDM**（工作目录可留作预览）；缺 ogg/mp3 时预检警告或升错误 |
| `Cli/Program.cs`、`Editor.csproj` | 引用 `Malody 谱面转换器\ffmpeg.exe` 的本机路径（`dist\ffmpeg.exe` 优先） |
| `自制谱.bat` / 官谱 bat | 中文路径在 cmd 默认代码页会乱码；已改为 ASCII bat + Python UTF-8 启动 |
| 遥测 | 未录到：`0I` 地面档 1/3、`0J` 地面/空中档 1（`0I` 空中档 1 官谱没有） |

大文件：Unity `PlayDirector` **已按职责拆分**到 `Scripts/Play/`。物件从单个 1085 行文件拆为资源实例 / Spine / 通用摆放 / Hold 渲染；角色从单个 953 行文件拆为生命周期与轨道 / Hold 与虚影 / 输入与命中动画。拆分文件仍属于同一个 `partial PlayDirector`，只能移动职责完整的方法与辅助类型，不要复制状态字段或改变 Update/判定调用顺序。当前 `MusePSona.Play.Tests` 为 **84** 项；仍建议拆：`MainWindow.xaml.cs`（约 2300 行）、`TimelineView.cs`（约 1500 行，末尾混入音频播放器）。Editor、`ChartIo` 工作目录选择、`MdmPacker` 媒体打包仍缺专门测试。

### 待清理文件

- `MusePSona 编辑器\_tmp\`、`dist-next\`：可删的旧构建/日志。
- `MusePSona 编辑器\_ref\`：旧打包探测目录，可忽略。
- `MusePSona 编辑器\dist\`：由 `打包到工具合集.ps1` 发布；工作谱在 `MusePSona\自制谱`，MDM 在 `导出` 或 `%LOCALAPPDATA%\MuseDashChart\export`。
- `dist-next\`：旧版构建，和 `dist\` 各带一份 68 MB 的 `ffmpeg.exe`。
- `dist\` 混入运行时数据：`work\`、`albums\`、`editor.log`、`MuseDashChart\`（设置 / 最近列表 / session）。
- 编辑器根目录测试残留：`chart.json`、`info.json`、`map1.bms`、`Daydream - 玻璃天使 (Glass Angel).mp3`。
- `scripts\__pycache__\`、`scripts\utils\__pycache__\`、各项目 `bin\` / `obj\`（合计约 234 MB）。
- `.gitignore` 未排除 `dist*/`、`_tmp/`、`导出/`、`*.mp3`。
