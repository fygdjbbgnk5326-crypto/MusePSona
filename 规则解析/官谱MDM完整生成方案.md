# 完整 MDM 官谱生成方案

目标：从游戏 Addressable 资源生成 **CustomAlbums 可玩、编辑器可打开** 的一首歌一包 `.mdm`，语义对齐官方谱，不追求 SerializedBytes 字节级复原。

编辑器侧已具备：打开/保存 MDM、顶栏 map+星级、曲绘预览、正曲 BPM、预览曲可选短音频（缺则警告）。生成器应对齐同一套契约。

---

## 一、产物定义

`.mdm` = zip，文件名固定（`MdmPacker.PackNames`）：

| 条目 | 必选 | 来源 |
|------|------|------|
| `info.json` | 是 | 曲库 `MusicInfo` + `StageInfo` |
| `map1.bms`…`map5.bms` | 至少一张 | 所选 `StageInfo` |
| `chart.json` | 有同刻输家才写 | `0S` 吃不进的飞近覆盖；CustomAlbums 忽略 |
| `cover.png` / `cover.gif` / `cover.jpg` / `cover.jpeg` | 否（缺则警告） | `song_*` Texture2D |
| `music.ogg` / `music.mp3` | 否（缺则警告；仅 wav 时预检警告/升错误） | `music_*` 整曲 AudioClip |
| `demo.ogg` / `demo.mp3` | 否（缺则警告） | `song_*` 试听 AudioClip |

磁盘落点（每种媒体只写一份，不随 map 翻倍）：

```
{输出父目录}/{曲名}/
  info.json / mapN.bms / chart.json   # chart.json 有同刻输家才写
  cover.png
  music.ogg          # 正曲；BPM 只从这里/StageInfo 来
  demo.ogg           # 开头试听，短音频；不读 BPM
  {曲名}.mdm         # 上面文件打进 zip（CustomAlbums 忽略 chart.json）
```

规则：

- 先解析 `music_*`，列出 map，再选：`1` / `1,3` / 全部。
- 多 map **合并进同一个 MDM**；音频、封面、试听仍各一份。
- 正曲用于播放与 BPM；预览曲只作试听（编辑器限制 ≤40s）。
- `delay` 进 `info.json`，**不改音符秒数**。键和 `0S` 永远是 0.0 对齐的 192 标准槽。吃音只加减 **整格**（`delay = n × 240/BPM/192`）。游戏内个人延迟要先变成同样公式的秒再从 tick 扣掉：BPM160 槽长=0.0078125s，0.35 设置 → 45格=**0.3515625s**，不是 0.35s。

---

## 二、官谱资源图

一首歌拆三处（mopemope 实测）：

```
StandaloneWindows64/
  music_mopemope_assets_all.bundle
      StageInfo  mopemope_map1..map4     ← 谱面
      AudioClip  mopemope_music  111s    ← 正曲
  song_mopemope_assets_all_<hash>.bundle
      Texture2D  mopemope_cover  440²    ← 曲绘
      AudioClip  mopemope_demo   20.7s   ← 试听
  config_others_assets_notedata_*.bundle
      NoteConfigData[]                   ← uid → type / pathway / ibms_id
  config_others_assets_defaultanchor_*.bundle
  config_others_assets_album*.bundle
      MusicInfo JSON                     ← 曲名/作者/星级/谱师
```

### 2.1 `StageInfo`（谱面壳）

`dump.cs`：`musicDatas`、`delay`、`mapName`、`music`、`scene`、`difficulty`、`md5`、`bpm`、`sceneEvents`、`dialogEvents`。

- `difficulty` = **档位序号**（map1→1，map2→2），不是 UI 星级。
- `bpm` = 标称 BPM（网格用）。mopemope=`100`，`sceneEvents` 为空。
- `delay` = 音频对齐；写入 info，不平移 `tick`。
- `musicDatas` 在 `serializationData.SerializedBytes`（Sirenix），UnityPy 不直接给 List。

### 2.2 `MusicData` + `MusicConfigData`（一条放置）

SerializedBytes 以 utf-16 `tick` 为锚，内嵌 `configData`：

| 字段 | 含义 |
|------|------|
| `tick` / `time` | Decimal **秒**（与 CSV `time_sec` 一致） |
| `note_uid` | 六位模板键，如 `051101` |
| `length` | 秒；点击 0，长按/连打 >0 |
| `pathway` | Odin 常省略 0；空中才写出。缺省用模板 |

丢掉 `tick=0` 且无 uid 的占位。`is_air` / `is_long_pressing` **不进 bundle**。

### 2.3 `NoteConfigData`（模板）

`uid` join → `type`（NoteType 0–17）、`pathway`、**`ibms_id`（2 字符，官方 BMS 键）**。

### 2.4 `MusicInfo`（曲库，几乎等于 info.json）

`dump.cs:852285`：`name` `author` `bpm` `music` `demo` `cover` `scene` `levelDesigner` `levelDesigner1–5` **`difficulty1–5`（星级字符串）** `unlockLevel`。

`map_decoder.load_official_dict` 已从 album/defaultanchor 抽出 name/author；完整生成应把同一节点的 difficulty/designer 一并读出。

---

## 三、生成流水线

```
1. 定位
   music_<id>_assets_all.bundle
   song_<id>_assets_all*.bundle
   notedata + MusicInfo JSON

2. 解析谱面（先于导出）
   列出 mapN / StageInfo.difficulty / note 数 / bpm / scene
   用户选择：单档 或 多档合并

3. 解音符
   parse MusicData → join notedata
   type 1–8 → 可玩键；type 0 或 ≥9 且 ibms 非空 → 通道 15 事件
   type 3/8 且 length>长按门槛（至少 2 槽，且 ≥0.02s）→ 53/54 头尾（id 用 ibms：`0F` / `0G`/`16`/`17`）；其余 13/14（同轨长按内保留非长按头中间刻的键；点长 `16/17` 不要抬成 0.05s 长条）。**1 槽点长 `0F` 转 `24`**（`02_top` 星）；同刻同轨已有其它可玩键则丢掉。type7 `23` 保持音符。专辑 type7 `23` 只做 `star24/对表` 统计，不当硬开关（同句常换轨）。length=0 的点长 `0F` 不写 13/14。`24` 必须进 `IsPlayableP0`。`write_bms` 再拦一层。
   BMS id = ibms_id；缺则 type 表兜底（01/0H/0F/21/**11**/22/10/0G）。type 8 缺 ibms 时用 des：`连击后退场`→17、`boss_01_连击`→16、`连击_平移/下落`→0G。type 5 兜底是 **11** 不是 0I。

4. 写 BMS（与编辑器 BmsWriter 同形）
   #GENRE scene    #BPM StageInfo.bpm    #PLAYER 1
   #PLAYLEVEL ← MusicInfo.difficultyN（星级），没有则用 1
   tick = time_sec / (240/BPM) 先吸到 192 整数槽，再 gcd 压缩
   sceneEvents 里的变速 → #BPMxx + 通道 08（时间仍按绝对秒反推格子）

5. 媒体各一份
   cover → cover.png
   正曲 WAV → ffmpeg libvorbis music.ogg（失败则文件夹留 wav，MDM 不塞假后缀）
   试听 → demo.ogg；过长仍可打进官谱包（官方 demo≈20s）；编辑器新建限制 40s

6. info.json ← MusicInfo 为主，StageInfo 补缺

7. 曲目文件夹 + zip MDM（媒体进包各一份）
```

---

## 四、info.json 对照表

CustomAlbums / 编辑器 `AlbumInfoFile` ← 官谱：

| info.json | 官谱 | 现状 |
|-----------|------|------|
| name | MusicInfo.name | 有 |
| name_romanized | 无则空 | 空 |
| author | MusicInfo.author | 有 |
| levelDesigner / 1–5 | MusicInfo 同名字段 | 有（缺则空） |
| bpm | StageInfo.bpm（数字） | 有 |
| scene | StageInfo.scene / MusicInfo.scene | 有 |
| delay | StageInfo.delay（SerializedBytes Decimal） | 有 |
| difficultyN | **MusicInfo.difficultyN 星级** | 有 |
| unlockLevel | MusicInfo.unlockLevel | 有 |

顶栏「1 ★5」里的 **5** 必须来自 `MusicInfo.difficulty1`，不能用 `StageInfo.difficulty`。

BMS `#TITLE` 可用 mapName；`#ARTIST` 用 MusicInfo.author。

---

## 五、与编辑器契约

| 行为 | 约定 |
|------|------|
| 打开 | `ChartIo`：`.mdm` unpack 或文件夹 `info.json`+`map*.bms` |
| 顶栏难度 | map 档 + info 星级；改星级写回 difficultyN |
| 曲绘 | 预览包内 cover；可更换 |
| BPM | 只认正曲 / info.bpm；预览曲不检测 |
| 飞近 | 编辑器存**逐音符速度**（每颗可玩键 `ApproachRate` 1/2/3）；`0S` 台阶导入导出；变速事件 `0X/0Y/0Z` 是批量改档标记，色带/事件列表按逐音符变化点派生 |
| 预览曲 | 缺：警告可导出；新建：>40s 拒绝选用 |
| 导出 | 无 ogg/mp3 正曲 → 警告（已有其它错误则升为错误）；wav/flac 只给编辑器预览、不进 zip；无封面/预览曲 → 警告；无 Scene/PLAYER/BPM/键位 → 拦截。不探测桌面 `_pack`。权威文件：`%LOCALAPPDATA%\MuseDashChart\export\<id>.mdm`；尽力拷 `MusePSona\导出\<id>\<id>.mdm` |
| 保存 | `FolderChart.Save`（删未用 `mapN.bms`；`StampNotes` 只动副本）+ `MdmPacker.PackFolder`（临时 `.tmp` zip，再 `File.Copy` 到 `.mdm`；媒体白名单 ogg/mp3 + cover png/gif/jpg/jpeg） |
| 游玩端 | Unity **进入 Play** 自动取上述目录最新 `.mdm` → `Charts/song.mdm`。「导入 Unity」只是立刻手拷，Play 占用时会失败 |

生成器写出的包应能被上述路径无损打开：星级、封面、正曲、试听、所选 map 齐全。自制谱编辑器导出的 MDM **已在 Unity PlayClient 验证可播**（F8 Spine：大型 0A/0B、**x01 Boss**、摆锤 x15、突击者 x16、齿轮 x03/x09；通道 15 的 `1A`–`1M` 驱动本体）。

---

## 六、已实现 / 仍不进包

**已实现（`official_to_mdm.py` + 启动器 [1]）**

- 先 list 再选 map；多 map 合一 MDM
- 曲目文件夹；cover / music / demo 各一份并打进 zip
- ffmpeg WAV→ogg
- SerializedBytes：tick、uid、length、pathway + notedata ibms_id
- 13/14、53/54；type 0 / ≥9 事件进通道 15；同轨长按内保留条上叠键（丢掉 `0F`/`0G` 中间刻；`18` 写在长按头类型里仍可能丢掉）
- `MusicInfo`：曲名、作者、星级 `difficultyN`、谱师、unlockLevel
- `StageInfo.delay` 吸到整格后写入 `info.delay`（默认 0）。设备延迟由检索从 tick 扣掉，不进 info.delay。
- 网格 BPM 用 `StageInfo.bpm` 数字（MusicInfo 里 `100～200` 这类展示串不写入 info.bpm）
- 飞近：自动检索整格 D → `tick−D` → 192 槽 → `0S`；通道 15 同一轴。`--capture-delay auto|0|N格|秒`（秒会吸到整格）。同槽输家 → `chart.json`。君往何处 map3：D=45格=0.3515625s，0S 在 0.75 / 78.375 / 78.75 / 141.375。
- CSV 兜底 `csv2mdm`
- `map_decoder` list/export CSV **保留**

**不进 MDM（资源里读不全或自制谱不需要）**

- 对话框 `dialogEvents`：无 MDM 通道
- 画面流速 `0O–0W`：官谱不写（飞近走 `0S`）

事件（`NoteType` 0 与 ≥9，切场景 / Boss 入场 / 显隐 / 滤镜）**进** MDM 通道 15，见 `官谱事件还原思路.md`。Unity 按该通道播 x01（词典 §2.1）。

---

## 七、命令

```bash
python scripts/official_to_mdm.py list   "<游戏>\...\music_<id>_assets_all.bundle"
python scripts/official_to_mdm.py export "<同上>" --out 输出父目录 --maps 1
python scripts/official_to_mdm.py export "<同上>" 输出父目录 --maps 1          # 位置参数与 --out 二选一
python scripts/official_to_mdm.py export "<同上>" --out 输出父目录            # 全部 map 合并
python scripts/official_to_mdm.py export "<同上>" --out 输出父目录 --denoise 2   # 压 Hidden 谱快速闪烁

python scripts/map_decoder.py list
python scripts/map_decoder.py export <bundle目录> <输出目录>
```

启动器 `[1]`：指定 music bundle（先解析再选 map）。CSV 兜底用 `csv_mmg_convert.py`。自制谱导出/试玩不走此启动器，见 `musepsona项目解析树.md`「自制谱 → Unity」。
