# MDM 物件转化词典

官包 `NoteConfigData.ibms_id` → CustomAlbums / 编辑器 BMS 两字符。可玩键与事件都是 **原样抄 ibms**，不按场景改号。自制谱编辑器按本词典写出的 `.mdm` **已在 Unity `PlayClient` 可播**（`Charts/song.mdm`）：飞近 + F8 Spine（含 scene_03 **0A/0B 大型怪**、场景 **x01 Boss 本体**，以及弹幕 `13/14/15`、齿轮 `18`）。近战 `11/12` 与 Boss 连打 `16/17` **不另画飞近**，判定点就是本体冲到判定线。布局、启动方式见 `musepsona项目解析树.md`。

umpopoff 转录与官机谱面已核对 **完全一致**。307 combo 接长按后变快是 **物件加速**：换用 `speed=3` 的 uid（如 `030203` 长按_地面平移_3），不是通道流速、也不是 08。`speed` **不**写成 `0O–0W`。

案例来自（只列真正填了新空格的曲目）：

- `mopemope`
- `milk`
- `umpopoff`
- `ut_megalovania_map4`
- `zhuguang_zhuzhou`
- `super_battleworn_insomniac`
- `museyao`
- `scarlet_keisatu_no_ghetto_patro_24_zi`
- `worlds_end_loneliness`（map1–4）
- `burn`（map1–3）
- `saishuu_kichiku_imouto_flandre_s`（map5）
- `bad_apple`（map5）
- `serve_sushi_serve`（map2，切鱼）
- `peropero_aniki_ranbu`（map2，愚人）
- `cubibibibism`（map1–4）
- `satsui_azashita`（map1–4）
- `rush_hour`（map1–4）
- `change_the_game`（map1–4）
- `chrome_vox`（map4：近战 `11`、连打 `16/17`、点长 0F 残渣）

没有案例的事件 **MDM 列留空**。

通道：可玩 13/14（点）、53/54（长按/连打头尾）；事件一律 **15**。

---

## 一、可玩键（type 1–8）

同一 ibms 在各场景只换 prefab / `speed` 档，MDM 仍用这一格。

| 官方 ibms | type | 官模板含义（跨场景） | 编辑器名 | MDM | 案例 |
|-----------|------|----------------------|----------|-----|------|
| 01 | 1 | 单击 平移 | 单击 小 | 01 | umpopoff / megalovania_map4 |
| 02 | 1 | 单击 上升 | 单击 上 | 02 | megalovania_map4 |
| 03 | 1 | 单击 下落 | 单击 下 | 03 | megalovania_map4 |
| 04 | 1 | 中型1 平移 | 中型1 | 04 | umpopoff |
| 05 | 1 | 中型1 上升 | 中型1 上 | 05 | zhuguang_zhuzhou（圆形机器人上升） |
| 06 | 1 | 中型1 下落 | 中型1 下 | 06 | zhuguang_zhuzhou（圆形机器人下落） |
| 07 | 1 | 中型2 平移 | 中型2 | 07 | umpopoff |
| 08 | 1 | 中型2 上升 | 中型2 上 | 08 | zhuguang_zhuzhou（方块机器人上升） |
| 09 | 1 | 中型2 下落 | 中型2 下 | 09 | zhuguang_zhuzhou（方块机器人下落） |
| 0A | 1 | 大型1 平移 | 大型1 | 0A | umpopoff / megalovania_map4 |
| 0B | 1 | 大型2 平移 | 大型2 | 0B | megalovania_map4（骷髅骑士） |
| 0C | 1 | 突击者 上升（官 des `突袭小怪_上升`，prefab `xx1601–1606` road/air_up_1..3） | 突击者 | 0C | umpopoff / megalovania_map4 |
| 0D | 1 | 摆锤 下落（官 des `摆锤_下落`，prefab `xx1501–1506` road/air_down_1..3） | 摆锤 | 0D | umpopoff / megalovania_map4 |
| 0E | 1 | 双押 | 双押 | 0E | megalovania_map4 |
| 0F | 3 | 长按（头 length>0，中间点丢掉） | 长按 | 0F | umpopoff / milk / mopemope / megalovania_map4 |
| 0G | 8 | 连打（中线；`连击_平移` / `连击_下落`） | 连打 | 0G | umpopoff / megalovania_map4 / change_the_game map4（60，可与 13 同拍） |
| 0H | 2 | 齿轮 | 齿轮 | 0H | umpopoff / megalovania_map4 |
| 0I | 1 | 突击者 倒（官 des `突袭小怪_下落`，prefab `xx1607–1612` road/air_down_1..3） | 突击者 倒 | 0I | rush_hour map1–4（55）；buttoba_supernova map4（32） |
| 0J | 1 | 摆锤 倒（官 des `摆锤_上升`，prefab `xx1507–1512` road/air_up_1..3） | 摆锤 倒 | 0J | rush_hour map1–4（55）；buttoba_supernova map4（54） |
| 10 | — | 官包 **没有** 此 ibms（编辑器 音符 键） | 音符 | 10 | 勿用官 ibms `23` 去对 10 |
| 11 | 5 | Boss 近战1（`boss_01_近战_48` / `close_atk_1`，打中 `hurt` 击退） | Boss 近战1 | 11 | burn map2/3（各 8）；**change_the_game map1/2 各 1（130.5s）**；map3/4 **没有**（同刻改 `16`）；chrome_vox map4（132.22s） |
| 12 | 5 | Boss 近战2 | Boss 近战2 | 12 | umpopoff |
| 13 | 1 | Boss 弹幕 远程1 | Boss 弹幕1 | 13 | umpopoff |
| 14 | 1 | Boss 弹幕 远程2 | Boss 弹幕2 | 14 | umpopoff |
| 15 | 1 | Boss 弹幕 远程2 另一路 | Boss 弹幕3 | 15 | umpopoff |
| 16 | 8 | Boss 连打1（`boss_01_连击_48` → 本体 `multi_atk_48`） | Boss 连打1 | 16 | zhuguang_zhuzhou map1–3；change_the_game **四张各 1**（map3/4 在 130.5s **替换**低难度的 `11`） |
| 17 | 8 | Boss 连打2（`boss_01_连击后退场_48`。长条：瞬移 `multi_atk_hurt`，再 `multi_atk_hurt_end` 往右退场；点长：直接 `multi_atk_hurt_end`） | Boss 连打2 | 17 | zhuguang_zhuzhou map1–3；change_the_game map2–4（map4 在 70.8s，前面仍铺 `0G`）；chrome_vox map4（37.98s 条 / 141.29s 点） |
| 18 | 2 | Boss 齿轮 | Boss 齿轮 | 18 | umpopoff / megalovania_map4 |
| 20 | 6 | P 点 | P道具 | 20 | scarlet map5（129 个，档 3） |
| 21 | 4 | 隐藏 / 幽灵 | 幽灵 | 21 | umpopoff / megalovania_map4 |
| 22 | 6 | 红心（common `0002_hp`） | 红心 | 22 | megalovania_map4 |
| 23 | 7 | 音符条（common `0003_score`） | 音符条 | 23 | umpopoff / megalovania_map4 |
| 30 | 2 | 光玉 平移 | 中弹 | 同 ibms | flandre map5（11）；bad_apple map5（38） |
| 31 | 2 | 光玉 上升 | 中弹 上 | 31 | scarlet map5 |
| 32 | 2 | 光玉 下落 | 中弹 下 | 32 | scarlet map5 |
| 33 | 2 | 光玉 交错 | 中弹 换轨 | 33 | scarlet map5 |
| 34 | 2 | 星弹 平移 | 小弹 | 34 | scarlet map5 |
| 35 | 2 | 星弹 上升 | 小弹 上 | 35 | scarlet map5 |
| 36 | 2 | 星弹 下落 | 小弹 下 | 36 | scarlet map5 |
| 37 | 2 | 星弹 交错 | 小弹 换轨 | 同 ibms | flandre map5（36） |
| 38 | 2 | 大玉 平移 | 大弹 | 38 | scarlet map5 |
| 39 | 2 | 大玉 上升 | 大弹 上 | 39 | scarlet map5 |
| 3A | 2 | 大玉 下落 | 大弹 下 | 3A | scarlet map5 |
| 3B | 2 | 大玉 交错 | 大弹 换轨 | 3B | scarlet map5 |
| 3C | 2 | Boss 鳞弹 | Boss弹1 | 3C | scarlet map5 |
| 3D | 2 | Boss 鳞弹 交叉 | Boss弹1 换轨 | 同 ibms | flandre map5（94） |
| 3E | 2 | Boss 刀弹 | Boss弹2 | 3E | scarlet map5 |
| 3F | 2 | Boss 刀弹 交叉 | Boss弹2 换轨 | 同 ibms | flandre map5（100） |
| 3G | 2 | Boss 子弹 三合一 | （Names 无） | 3G | scarlet map5 |
| 3H | 2 | Boss 子弹 尿分叉 | （Names 无） | 3H | scarlet map5 |
| 3I | 2 | Boss 子弹 双分叉 | （Names 无） | 3I | scarlet map5 |
| M0–ML | 1/3/52–54 | 切鱼专题（DAVE 联动） | （未进调色板） | 同 ibms | serve_sushi_serve map2（全套 22 码） |
| 40 / 41 / 42 | 1 | 愚人彩蛋 1/2/3（与 问答 40/41 type 50/51 共用号，按 type 拆） | （调色板无） | 40 / 41 / 42 | peropero_aniki_ranbu map2（65 / 53 / 41 个） |

`speed` 只存在于 uid（如 `030203` = 长按地面档3）。转 MDM 仍写同一 ibms（长按还是 `0F`），**不**插 `0Q`。飞近按自制包规则压成通道 **`0S` 台阶**（同刻幽灵让路）；吃不进 `0S` 的键进 `chart.json`。本仓库管线与本机 CustomAlbums 都认 `0S`（不是画面流速 `0O–0W`）。详见 `自制包飞近规则.md`。

### 1.1 摆锤 / 突击者 prefab 与官谱分布

每个「轨道 × 档」各一个 prefab，后缀 `_1/_2/_3` = 飞近档；前缀 `xx` = 场景号（01、05、09…）。

| ibms | 官 des | prefab | 官谱总数（746 首） |
|------|--------|--------|--------------------|
| 0D | 摆锤_下落 | `xx1501–1506` road/air_down_1..3 | 20142 |
| 0J | 摆锤_上升 | `xx1507–1512` road/air_up_1..3 | 158 |
| 0C | 突袭小怪_上升 | `xx1601–1606` road/air_up_1..3 | 15655 |
| 0I | 突袭小怪_下落 | `xx1607–1612` road/air_down_1..3 | 135 |

`0I` 空中档 1 官谱里不存在。

### 1.1b 齿轮 `0H` 官谱分布

全库扫描（与摆锤/突击者同一批 746 首）：

- 绝大多数在地面轨（**97.8%**）。
- 约 **4.4%** 落在长按时段内：典型写法是 **对侧轨长按 + 本轨齿轮流**。同轨长按、对侧没有长按时，官谱**从不**这样配。
- 长按内部的齿轮高度与普通齿轮相同，都贴红线，不靠抬高/压低躲碰撞；游戏用按住状态判穿过，不是改 y。
- 约 **61%** 与对侧轨物件同一拍。
- 同轨键同一拍全库仅 12 例（编辑器会警告）。

### 1.2 出场时间（实测，6.7.0）

所有类型：`showTick` = 生成时刻，`tick − showTick` = **判定帧 / 30 s**（误差 ±3 ms）。动画名直接带判定帧（如 `in_nor_44`、`in_up_39`、`in_26`）。

| 物件 | ibms | 档 1 / 2 / 3（帧） | 秒 |
|------|------|--------------------|----|
| 普通怪、齿轮、幽灵、双押、音符、红心 | 01/04/07/0A/0B、0H、21、0E、23、22 | 44 / 38 / 33 | 1.467 / 1.267 / 1.100 |
| 突击者 | 0C / 0I | 39 / 30 / 24 | 1.30 / 1.00 / 0.80 |
| 摆锤 | 0D / 0J | 26 / 21 / 18 | 0.867 / 0.70 / 0.60 |
| 上升 / 下落怪 | 02/05/08、03/06/09 | 30 / 26 / 23 | 档 3 实测 0.767；动画名 `in_up_*` / `in_down_*` 同这组帧。**不是**突击者 `in_up_39/30/24`，也不是连打下落 `in_down_27/23/20` |
| Boss 远程1 弹 | 13 | 21 / 18 / 15 | 档 3 实测 0.5。横向 **px/s 与普通键同档相同**（见 §1.3），不是把 13.85u 压进 0.5 s |
| Boss 齿轮 | 18 | 21 / 18 / 15 | 与弹 13 同（`boss_1_nor_*`）。Unity `HitFrames` 已对齐，直线飞近使用车道速度 |
| Boss 远程2 弹 | 14 / 15 | 多数场景 **23 / 20 / 17**（`in_boss_2_*`）；城堡 scene_05 才是 31/28/24。Unity 按当前切景选表，不要一律套城堡帧 |
| 连打（下落款） | 0G | 27 / 23 / 20 | 档 3 = 20。Unity `HitFrames` / `in_down_*` 已按此表；不要用天气娃娃 30/26/23 | |

普通键的飞近速度比 = 44 : 38 : 33 取倒数 = **1 : 1.158 : 1.333**（约 400+ 段实测），**不是** 1 : 1.5 : 2。同档 **横向 px/s 与行程 13.85u 只属于普通直线族**（01/0H/21/0E/23/22 等）。短判定帧的直线（13/18，以及 14/15）**同速、更近生成**：档 3 约 1820 px/s，与 `in_nor_33` 一样，所以空中齿轮/骷髅与下轨普通键会对齐；不要用 `x = hitX + 剩余/本键时长 × 13.85`（那会让 15 帧齿轮划过快）。突击者仍是全程 13.85u / 本键时长（档 3 约 2490 px/s），不要改成车道速度。

Unity HUD / `CategoryOf`：`0H`/`18` = 齿轮；`0I`/`0J` = boss（外观仍走突击者/摆锤倒置）。转录 `notes_to_taps_holds`：同轨长按/连打只丢掉中间的 `0F`/`0G` 刻点，**条内其它可玩键全留**（`0H`、`23`、`0D`、`0C`、`0E` 等）。点长 `0F`（length=0）**不要**写进 13/14：官机不画，自制谱会当成小怪和同刻摆锤叠键（chrome_vox map4 有 13 处，其中 3 处与 `0D` 同拍）。**1 槽点长 0F**（length≈240/BPM/192；Cyaegha map3/4 的 42.9 条上星和 108s 远攻）转自制 `24`（`02_top` 星），**不要**写成 `23`（type7 是 `0003_score` 音符）。`24` 必须用同轨另一按键独立打击，不能因正在按住长条而自动结算；击中计连击/加分与长按头相同。`23` 才是接触轨道自动收集且不计连击。`16/17` 点长仍可走 13/14。

### 1.3 出场几何（实测）

16:9，相机正交 size 3.75 → 1 单位 = H/7.5。几何与场景无关（scene_01/02/05/09 对比），突击者纵向弧线细节除外。

- 普通键：x = +10 单位生成（≈1.25W，屏外右侧），判定线 x = −3.85 单位 ≈ **0.2115W**；轨道（自上而下）空中 ≈ 0.35H、地面 ≈ 0.635H。地面齿轮底边贴场景红线（地板顶），不把圆心放在自下 0.241。匀速直线，漏打后继续往左走。横向速度 = `13.85 / (普通判定帧/30)`（Unity `Flight.LaneSpeed`）。
- 投影：带 `shadow` 槽的飞近物件，椭圆中心在红线下面 0.20（`ShadowTrackY` = 1.87，向下），落在地板上沿的中间。本体由骨 `Y` 抬在投影上方。音符 `in_road` 的 `Y` = +1.184，`in_air` 的 `Y` = +3.299，两轨相差 2.12。空中键的投影也在这条地面线上。摆锤/突击者仍对锤面。
- 中线：连打 `0G`/`16`/`17` 与近战 `11`/`12`（官机 masher `body` 判定 y≈**0.478H**，两轨中间；上下轨都能打）。弹幕 `13`/`14`/`15` 走谱面 `pathway`（空中 ≈0.66H、地面 ≈0.37H，自下）。`13` 播 `in_boss_1_*`，`14`/`15` 播 `in_boss_2_*`（城堡 31/28/24，scene_01 是 23/20/17）。怪/弹幕入场 **X 走 `in_*`（根 x=0）**。有 `shadow` 槽的，Y 用判定帧投影中心对一次 `ShadowTrackY`，本体相对投影的 `Y` 骨位移留着。没有投影槽的，Y 仍用判定帧 `BodyOffset` 对一次 `LaneY`。不要每帧 `LaneY − 当前 body.y`（会抹掉升降/拖尾路径）。红心 `0002_hp`、音符 `0003_score` 的 `in_air`/`in_road` **不带飞近**，X 仍走 `TryPose`，Y 跟投影线。`out_*` / 连打 hurt 仍锚判定点。scene_01 的 `0106` 槽名是 **body1/body2**。双押 `0E` 走本轨。上下脸每帧钉到同一条 `LinearX`/`dest.x` 和本轨 Y（03/05/10 空中/地面动画脸心不同，不能跟各自 `X` 骨）。
- 短判定帧直线（`13`/`18`，Unity 里还有 `14`/`15`）：生成时刻仍是 `tick − 判定帧/30`，但 **x 用同一套 LaneSpeed**，所以档 3 的 15 帧齿轮只飞约 `13.85 × 15/33 ≈ 6.3` 单位（大约从 x≈+2.5 进场，一出现就在屏内）。遥测：`boss_1` 档 3 tick−showTick ≈ 0.50 s、px/s ≈ 1820（与幽灵/普通键同档相同），观测进场约 0.48 s。
- 突击者 `0C`：x 从 ≈1.25W 按**本键时长**匀速走（比车道快）；进度 u≈0.25 时身体进入右边缘，沿底边（y≈0.98H）跑，进度 0.37–0.39 跳进轨道（档 2/3；档 1 在 scene_09 实测 0.42、起跳前 y 0.94–0.96H，是档位还是骨骼差异未定）；地面轨落地前过冲约 0.06–0.12H。
- `0I`：x 运动同 `0C`，沿顶边跑再落进轨道，无过冲。≈ 对侧轨 `0C` 关于 ~0.48H 的镜像。
- 摆锤 `0D`：从右上屏外荡入的圆弧，角速度近似恒定。地面：圆心 ≈(0.31W, −0.33H)，r≈0.83H，角度 86°→−9°；空中：圆心 ≈(0.34W, −0.87H)，r≈1.08H，56°→−9°。判定时主体骨骼比轨道高约 0.14H。
- `0J`：从右下荡入，= 对侧轨 `0D` 的上下镜像：`0J` 地面 = `0D` 空中关于 y≈0.478H 镜像；`0J` 空中 = `0D` 地面关于 y≈0.487H 镜像。游戏用同名动画、不做翻转缩放，差异在 Spine 动画内部。
- 上升 / 下落怪（天气娃娃 05/06 等同族）：在 x≈0.86W 从底 / 顶竖直进场，x 匀速收到判定线。y 用全程 ease-out（`(1−p)²`），判定前约 0.3 s **贴近**轨道，不要在 0.3 s 前提前钉死在轨上（钉死会把 23 帧的纵移压进前 0.47 s，看起来砸下来）。

同一拍不同速是官谱合法配置，见 `自制包飞近规则.md`「同一拍不同速」。

Unity `Flight` 已按 §1.2 时长、§1.3 几何绘制时间线（`Flight.HitFrames` / `LaneSpeed` / `TryPose`）。直线横向使用车道速度，升降怪 y 全程 ease-out。动画名：`AnimBinding` 中 02/05/08 → `in_up_30/26/23`，03/06/09 → `in_down_30/26/23`，突击者 0C/0I → `in_up_39/30/24`。通道 15 的 `1O–1W` 按当前时刻切换场景资源。

动画资源优先级：**F8 的 Spine 3.8.99 `.skel`（Unity `MdSpine` 直读）> atlas 静图**。官方 `.spine` 工程还原不了。改图请买/用 **Spine Professional**，Launcher 装 **3.8.99** 导入与导出（不要 3.8.75 导出、不要 4.x）。F8 在 3.8.99 里看完整物件：新建项目 → 导入数据，层级树 **图片** 节点填含 `0504/` 的根目录（中文路径易 MISSING，可用 `E:\md-spine`），取消「限制扫描」。导出勾上 **Nonessential data**，Binary + PMA atlas，Unity `scale` **0.005**。拆图用 Grabber `tools/unpack_atlas.py`。摆锤 `wooden` 裁切：atlas y 从上往下 + POT 顶 pad（`extract_md_sprites.py` / `recrop_pendulum.py`）。角色战斗包在 `char_*_battle`，与 `gamescene_*` 分开。细节见解析树「Spine 编辑器」及「已知问题」。

### 1.3b 长按 miss / 外观（官机 IL2CPP）

接住（Perfect/Great）时头钉在 `HitX`，条子随尾缩短，`EndSec` 后消失。

漏接：**不要**等 `MissAfter`（120ms）再松头——那会把头钉在判定线，记 Miss 时按 `TimeSec` 瞬移到线左（看起来卡住再弹出）。未接住时从头过线开始按 `LaneSpeed` 连续飞过，直到尾过 `OffLeft`。`LongPressController.OnControllerMiss` 是空桩；真正生效的是 **`LongPressHurt`** → `SpineActionController.SetAlpha(0.391)`（`m_HasAlpha`，DOTween **0.2s**）+ `LongPressNoteVisibleController.NoteTransparency`（头尾 `StarTransparencyCoroutine`、条子 `SliderTransparencyCoroutine`，条目标 alpha 约 0.55）。Unity：`Flight.TryHoldSpan(pinHead: 仅 Perfect/Great)`，`HoldMissVisual` 从 `TimeSec` 起 0.2s 落到 **0.391**。

官机长条预制（`010204_air_nor_1`）：根 `SpineActionController`（`actionMode=12`，`SetLength` 拉条子，`rotateRuration` 转星，`rendererPreb` / `clipEffect` / `destroyEffect`）。`0102_air_renderer` 是 `SpriteRenderer` **`x02_*_body`，DrawMode.Simple**（跟时长拉伸五线谱条纹，不是 Repeat UV）。`scene_09` / `scene_10` 地面条子都没有同名 Sprite，只有 Texture2D `0902_road_body` / `1002_road_body`（1024×128）；缺这张时条子会把 `*_road_top` 星星拉长。空中条子仍是 Sprite `x02_air_body`（880×118）。子节点 `startParent/s00_press_top` 头星（尾星运行时克隆，local 0）。`0102_air_renderer` 世界 y=**1.20**，`0102_road_renderer` y=**−0.95**。不要改用通用轨（空中画面 y=1.125、地面 −1），星星下沿会埋进地板、看起来不在轨道中心。头星 Sprite `0102_*_top` **154×149、PPU 100、pivot (0.5, 0.5)**。不透明重心约在几何中心下方 **5.5/149**；`rotateRuration=1` 是 360°/s，绕这个重心转，绕四边形中心会把星星甩到轨道下沿。`fx_note` 是粒子（size 0.8、重力 5），**不是**音符图。

条上音符：bundle 里 `x02_*_note_up/down` 是 **880×118 Texture2D**（PPU 100、pivot 左中），**没有**挂在预制 SpriteRenderer 上。`note_down` 才有高音谱号（约 u=0.21 / 0.85），`note_up` 只有八分音符且列位不同。抽取用整张 Texture2D（`air/ground_hold_note_up/down`），不要 `Sprite.image` Tight 壳。Unity：`*_hold_head/body` 静图，**不要**绑 Spine `0102_air`。音符层是条子的**兄弟节点**（`holdFx` 挂 Root，禁止挂 Body——`localScale.x=剩余长度` 会把音符跟 Simple 一起压扁）。网格=当前可见条 AABB；UV 格距 = `HoldBarBox × (880/118) × HoldNoteSpanMul(1.2)`（比条子等比略疏，对齐官谱间距）。UV **钉在完整时长**：未接住时条头 U=0（左起 ♪、谱号，与官谱一致）；接住后条尾仍对齐完整 U，从**左边**裁掉已消耗段，音符本身不缩放。短条 Clamp、长条 `frac` Repeat（`MdHoldNote`，`DisableBatching`）。约 2Hz 整层上下漂（`HoldNoteBob=0.10`），up/down 叠画。`fx_note` 暂不画。格子 `HoldEndBox=1.54`（154px/100，不要再放到 1.72）、条子 `HoldBarBox=0.96`。整条（条子、头尾星、音符层）钉在上面的 renderer 高度，不走 `LaneY`。叠键不能盖果。判定圈/打击字见 §1.3d。收集物静图还可带 `hp_glow`/`score_glow`（骨骼路径已画 glow 槽）。

### 1.3c 红心 / 音符条（收集物）

| ibms | NoteType | 包 | 贴图 | 动画 |
|------|----------|----|------|------|
| **22** | `Hp=6` | common **`0002_hp`** | `hp` 69×60、`hp_glow`、`shadow` | `in_air` / `in_road` / `in_mount` / `out` |
| **23** | `Music=7` | common **`0003_score`** | `score` 69×70、`score_glow`、`shadow` | 同上 |

- 可击打、**不计连击**（`addCombo=false`）。击中：红心 `BattleRole.AddHp`；音符加分，部分角色 `musicNoteAddHp`。漏了按直线 `TryExitPose` 飞过判定线。飞近帧与普通键相同 44/38/33。
- **不是** 场景 xNN。`AnimBinding.CollectPack` → `0002_hp` / `0003_score`，场景目录没有则 `common/`。`scale.json` **0.005**。不要编成 `0102_air`（那是长按）。
- 空中播 `in_air`、地面播 `in_road`；击中播 `out`（没有 `out_p`/`out_g`）。
- 骨骼锚在槽 `hp`/`score`（及骨 `size`），**不是 root**。`BodyOffset` 必须对贴图中心，否则会漂在两轨中间。高度跟 §1.3 投影：`shadow` 中心对 `ShadowTrackY`（轨道中间），空中/地面都落在同一条地面投影上，本体由 `in_air` / `in_road` 的 `Y` 骨抬起来。与长条同轨时，地面音符叠在条上。
- 无骨骼时静图格子 **0.58**（源图比小怪 85px 小）。编辑器键 `10` 官包没有，不要拿官 `23` 去对 `10`。

### 1.3d 判定圈 / 打击字（Unity 已接）

Unity 显示在 `PlayClient/Unity/Assets/Scripts/Play/`：`PlayDirector.Stage` 管判定圈，`Hits` 管打击字，`Combo` 管连击，`Hud` 管 IMGUI。类型名仍是同一个 partial `PlayDirector`。

官机场景预制：每轨一套 `HitPointOut` + `HitPointIn`（Sprite Atlas，**不要**整张 `SpriteAtlasTexture`）。`extract_md_sprites.py` 用 `Sprite.image` 裁 `judge_out`/`judge_in`/`judge_catch`；`extract_hit_fx` 从 `gamemain` 抽打击字进 `sprites/common`。相机 **size 3.75**、`HitX −3.85`、PPU **100**。

| 元件 | 源 | 像素 | Unity |
|------|----|------|-------|
| 外圈 | `HitPointOut` | 65×63 **三段弧** | `JudgeOutBox=0.65`，`+Z` **160°/s 逆时针** |
| 内圈 | `HitPointIn` | 22×22 **完整环** | `JudgeInBox=0.22`；**禁止**每帧 `localScale=1`（会盖过 FitBox，内圈大于外圈） |
| 接住 | `HitPointInCatch` | 28×28 | 只换贴图，格子仍 0.22。官包只在 `gamescene_01`；`scene_02`–`09` 共用这张 |
| Perfect | `ScorePerfect` | 275×59 | `hit_perfect` |
| Great | `ScoreGreat` | 180×49 | `hit_great` |
| Pass | `ScorePass` | 143×50 | `hit_pass`，**只对躲避齿轮** |
| Gold | `GoldPerfect` / `GoldGreat` | 已抽 | **未接** Fever |
| Combo | 默认 `fx_combo_0/1/2` + `combo_digit_{low,mid,high}_0–9`；`scene_09`（官机 `GlobalSceneDefine.dj_max=9`）走 `Combo_djmax` | 默认 Spine 气泡/**COMBO 字不动**；数字烤 `LuckiestGuy-Regular`，根 **+25px**、数字 local `y=3.85`（anchored 376）。缩放 `ComboCounterIn/Standby/Out`。DJMAX **无** `fx_combo`：字用 `Low/Normal/HighCombo_djmax`（287×121，`combo_djmax_word_*`），挂 Combo 根 **不跟数字缩放**，local `y=4.25`。数字烤 `Teko-Bold` 160、**无描边**、FaceTex 三档（`combo_digit_djmax_{low,mid,high}_0–9`），`em=0.88`，字距 **0.1**。数字 rest `y=3.35`（335）；进场 `ComboCounterInDjmax` 295→335 上滑带回弹，缩放仍用默认 In/Standby/Out。根 **+25px**（不跟官机 Combo_djmax −272.5）。档位 5–19 / 20–49 / 50+ | `<5` 不显示。`In` 只在首次和 50 变色各一次。`extract_djmax_combo` 写 `sprites/common` |
| Miss | — | — | **无** `ScoreMiss` 气泡 |

顶点色（scene_01 SpriteRenderer）：空中外圈白 `a≈0.588`，地面外圈粉紫 `a≈0.903`，空中内圈青 `a≈0.409`，地面内圈品红 `a=1`。空中/地面共用同一张 Sprite，只改色。缺圈才四段方块环。`HitPointOutTriangle` 不用。角色包 `W_fx_Perfect4` 不在 gamescene，暂不接。双押 `ImgDouble*` 与单押同一张字。

`Judge.Grade.Pass`：齿轮 `0H`/`18` 飞过判定线时发一次，不计连击、不进 Perfects/Misses，物件继续 `ThroughX`。PASS 字画在**对侧轨**（地齿轮上天轨，天齿轮上地轨）。普通 Miss 不出字。改 Runtime 枚举后必须跑 `PlayClient/sync_unity_plugins.ps1`，否则 Unity 仍链旧 DLL（CS0117）。

### 1.3e 对局背景（Unity 已接分层 + 循环平移）

官机预制 `scene_XX`（`gamescene_XX_assets_all_*`）：层 `Near` / `Near_air` / `Mid` / `Far` / `Bg`。地板 Animator 常在 Near **子节点**（`scene_02` 的 `Floor` 播 `s02_floor`，根 x 0→−92.16 / **18.67s** = **48×1.92** 块，父级 Near 没有控制器）。**不要把 Floor 循环压成 23.04**（12 块地砖），否则街机 `Mid_I` 掉出循环、凛ちゃん店会重复。SpriteRenderer PPU100；根约 `(-6.67,-3.75)`，正交相机 size **3.75**。横向循环读 StreamedClip 关键帧位移（`coeff[3]`），不要把斜率当成循环宽。Animator 开局帧和 Transform 不一致时，以开局帧为准（`scene_01` 的 `Well` Transform 写 0，曲线停在 x=−3.84）。不要用选歌 `main_bg_*` Spine 当对局背景。判定线 `HitPoint*` 整棵不抽。`arrow` 这个名字不要一律跳过：scene_01 墙边人字纹是 `WellArrow*` 下的 `s01_arrow`。默认局只抽原版激活件：跳过 `_hexie` 和谐替图，以及 `Mid_1_Arknights` / `_Touhou` / `_Wacca` / `s03_ghost_cy2` / `SceneEggs` 等联动换装（父组默认 `m_IsActive=false`）。联动方案以后再加。场景火/幽灵是 Spine（`MeshRenderer` + `s03_ghost`/`s03_fire`，scale **0.005**），不是 Sprite。`scene_10` 默认根无子节点，背景在 `scene_10_miku` 的 `ScrollableGear`。舞台没有 Animator，圈长在 `Scrollable.m_ScrollLength`，速度是普通键车道速度（13.85 / 44 帧）乘 `m_SpeedFactor`，开局相位是 `m_Anchor * 圈长`。`Mid1` 73.2/0.40、`Mid2` 54.9/0.30，一圈都是 19.38s，屏幕和立牌对齐。荧光棒在 `Near` 的子节点上，要拆开。镜音连贴在圈长右缘外 0.9，裁切时要留下。`SpeakerBoxMiddle1` 在 Mid1 左缘外（x≈−13.07），按圈长 73.2 折回后在双子右侧（x≈60.13），裁窗口时不能丢掉。播放时 KAITO 和头上的音符不浮动；镜音双子（含身旁的星）用上下浮动，振幅 0.12、周期 1 秒。`0037` 初音、`0040`/`0041` 两颗音符改用官机 `kagamine`：1.333 秒抬 0.258（正弦振幅 0.129，开局在最低点）。小怪 `1010` 默认皮是身体；翅膀、眼睛、天线在 `Miku` 皮肤。连打 `1004` 的 `body`/`hand_*` 和 `hurt_1/2/3` 的 `1_body`/`2_body`/`3_body` 也在 `Miku`。默认局把 default 和 `Miku` 合成一套皮肤；`RinLen` 不用。setup 没写附件名的槽（连打那三套受击件）不要按槽名填。`1014` 出场会关掉第二套头/盔/头发，Apply 之后不要再按 setup 名从 `Miku` 填回去，否则那些件会在空骨上漂。连打轮换 `hurt_1/2/3`，不要播把主 body 挂回去的 `hurt_4`。`1_body`/`2_body`/`3_body` 名字不以 `body` 开头，扁条裁切不能把它们丢掉，否则只剩火、爱心、触须在场上漂。位置以写回的 layout 为准。呼吸灯每根用官机 `idx` 分频段（0–30），不要共用 band 0。射灯不闪透明度。转轴在灯头。光束中心必须落在灯头当前朝向的射线上（局部 +Y），偏开会只剩灯头、看不到光柱。前灯 `spotlight` 2 秒只摆 3°–4°，舞台暗灯 `spotlight_back` 4 秒扫 10°–20°。双子右侧补上的中号音箱水平翻转后大箱在左，并再向左靠 0.6；旁边短射灯朝 −21°，不播晃动。近景荧光棒待机上下 0.04 / 0.065；舞台棒整排低 0.08，颜色压成 (0.40, 0.43, 0.50)。`scene_11` 愚人包几乎无景。

抽取：`python scripts/extract_md_scene_bg.py --all`（`extract_md_sprites.py` 也会顺带抽），写出本机 `sprites/scene_XX/bg/*.png` + `layout.json`（gitignore，不进 MDM）。只补粒子、不重抽背景：`python scripts/extract_md_scene_bg.py --particles --scene 08`（`--particles` 不跑全量 `copy_looks`）。官机 Sprite **pivot 多为左下 (0,0)**，抽取时改成四边形中心，否则整景会偏到画面左下。每层片数上限 180（scene_02 Near 原 4k+ 粒子会卡死）。`layout.json` 的 `items` 放顶层（带 `gi` 组号，不要用 `g`，会和颜色绿通道撞名），不要嵌进 `groups`，否则 Unity `JsonUtility` 经常解出空数组。弹窗根位移同样放顶层 `movePeriod` / `moveT` / `moveX` / `moveY` / `moveStep`（`JsonUtility` 解嵌套 `moves[].ts` 经常是空的）。Unity `MdSceneBg` 按 `1O–1W` 换包；层 Animator 横向循环；元件自己的 Animator 做浮动（`bobAmp`）和闪灯（`blinkPeriod`）。层名带 `light`/`lamp`/`sign` 且 clip **没有** typeID 212 颜色曲线、也没有位移关键帧时（`s03_bpm_light` / `s06_bpm_light` 空片），**不要**写 `blinkPeriod`：播放端 0.35～1 正弦会把洗光/窗灯打闪。竖向循环写 **`scrollY`**（负值往下）和 `scrollPeriod`，播放用 `Repeat` 包进这一段，不要当成 0.12 的正弦漂。层 **`belt=1`** 时，名字里有 Mid 且速度为 0 也不要发明一条横移皮带。播放端另有一条全局门槛：层名像传送带且速度 **<2.5** 会改成 5u/s。**不要改这个 2.5**。scene_07 过山车是名字 `mid`、循环 56.8/80s（速度 <2.5），只给这一层写 **`belt=1`**（条件：`name==mid` 且 `loop>1` 且 `period≥10` 且 `loop/period<2.5`）。父节点有旋转时，子物体本地坐标要先绕父旋转再加到世界坐标，否则远光会落在轨道上。循环窗右缘取 **`max(origin+loop, 本圈最左一块的 x+loop)`**。层原点常常在第一块贴图左边，只切到 `origin+loop` 会把本圈后半截和下一圈拷贝一起丢掉。骨骼 `order==0` 时播放端画在 **−35**；官机不是 −35 的必须把 order 写进 layout。无后缀、旁边有 atlas 的文本，正文以 `{`/`[` 开头就存 `.json`，不要存成 `.skel`（会优先走 SkeletonBinary 然后失败）。场景道具 `SkeletonDataAsset.scale` 用 **0.005**，不要按 misc 名写成 0.01。layout 只维护 `PlayClient/Unity/Assets/StreamingAssets/MdLooks/sprites/`。不要整场重抽（会先删 png）。**`mid_H*` 不要套默认 bob 0.12**。地砖循环宽已是步长整数倍就保持，不要按单张贴图的忽大忽小间距把官机循环扩宽。没有横移的层，不要用全部物件的 x 跨度去补一条循环。场景火/幽灵走 Spine `PoseLoop`。`25` 藏 / `26` 显；`2N` 冻位移（保持当前偏移）/`2O` 解冻；`27`/`28`/`29` 整景上下滚（`ScreenScrollSpeed=1.6`）。有背景时关掉占位车道条。改 `BgTimeline` 后同样要 `sync_unity_plugins.ps1`。

#### scene_01 方块墙（对照 `gamescene_01`）

`Near_air` 关着，不抽。`s01_star` 是粒子（`star_1/2/3`，贴图 Default-Particle，挂 `Bg`，shape SingleSidedEdge、rot 90°），写进 `layout.particles`，不进静图地图。`par_star` / `par_star2`（scene_02 Near 那种 4k+）整棵跳过。emission 关掉的父节点不抽。scene_10 官机没有对局粒子。scene_11 愚人包跳过。

| 场景 | 粒子 | 挂层 |
|------|------|------|
| 01 | `star_1/2/3` ×3 | `Bg` |
| 02 | `par_Water`（贴图 `s02_Fx`） | `Water`（`Water_All` 对上 `Water`） |
| 03 | `FxParticle` | 根 `gi=-1`，y≈5 |
| 04 | `rain_floor` + `rain1` | 根，雨丝 |
| 05 | `s05_scene_sfx` | 根 |
| 06 | 无 | |
| 07 | 过山车车灯 2 组（icon Fx + Default-Particle） | `mid`；一圈已有两辆，播放端不要再按 loop 左右复制 |
| 08 | 灯笼 `par_spark`×5、樱 `par_petal`、远景 `star_1`/`star_2`/`background_blue` | `Mid_A_0` / `Mid_A_all` / `Mid_B_1` / `Far_all`。Box 形，`star_*` 的 shapeSX=**240**。高 rate 的 `maxP` 封 80。08 坐标按灯笼/樱花片对齐，不直接用 bundle 世界 x |
| 09 | `par_leaves` | 根 |
| 12 | 烟花 + `s12_light` | 见下 |

粒子顶层数组 `layout.particles`，不要嵌进 groups。播放端 `Particles/Additive`。shape **5=Box**（不要当 Circle）。scene_08 粒子和骨骼一样不按 loop 左右复制。

| 层 | Animator | 循环 | 物件 |
|----|----------|------|------|
| `Near` | `s01_floor` | **3.84 / 0.75s**（5.12 u/s） | 地砖 `s01_floor_1` / `_2`，一组是 **A、A、B**（步长 1.28）。只留这一组，播放按镜头补拷贝 |
| `Bg` | 无 | 0 | `s01_bg` 是 1×1 被拉成 **21×10** |
| `Mid` | 无横移 | **0**，`belt=1` | 五列方块竖滚，不横移 |

`floor_1` 的间距在 1.28 和 2.56 之间交替。按单张贴图会把 3.84 扩成 **12.8**，接缝对不齐。官机循环宽已是 1.28 的 3 倍，保持 3.84。`FloorB` 上有两盏 `s01_floor_light`（相对地砖中心约 −0.30 / +0.40）。clip 之后只把 **`s01_floor_light`** 挂回本圈地砖，不要把其它景里 w<0.8 的瓦/鱼/叶当灯丢掉。播放按 k 拷贝时 **只给 scene_01 的 Near** 把 sortingOrder 减 `k × 圈内 order 跨度`：官机越往右 order 越低（−27…−46），只留 AAB 再拷会让下一圈 FloorA 仍是 −30，盖住本圈 FloorB 圆孔和灯。不要套到其它景，08/12 中景会叠错。

方块在 `Mid/Well_Special/Well` 的 `Well1`–`Well5`。`Well` 的 Transform 是 (0,0)，曲线 `s01_well` 开局 **x=−3.84**，5 秒走到 −3.44，10 秒回到 −3.84。抽取用开局帧，再给 **bob 0.2 / 10s**（幅度是 0.4 的一半）。每列自己的 Y 从 **−1.71 走到 −13.23**（**−11.52** = 九格 × 1.28）再跳回，花纹每九格重复：

| 列 | 周期 |
|----|------|
| `Well1` | **8.0167s** |
| `Well2` | **11.1833s** |
| `Well3` | **10.4667s** |
| `Well4` | **9.9167s** |
| `Well5` | **10.9333s** |

写在每块上的是 `scrollY=−11.52`，不是 bob 0.12。单块再 bob 会把井拆散，播放端在 `scrollY` 有值时不要叠正弦漂。父级 `Mid` 没有横移；用方块的 x 跨度编一条皮带，会把左列裁掉、整墙横着滑。播放端见到 `Mid` 且速度为 0 还会再发明一条，所以这层 **`belt=1`**。同一位置叠着的第二张 `Well` 丢掉。`Well_*` 联动（Cytus / Wacca / Touhou / Arknights）和 `Well_ba_*` 保持关。

官机构图是 **墙在左、星空在右**。五列世界 x 约 **−13.07 / −10.51 / −7.95 / −5.39 / −2.83**（中心），右缘 `WellSide` 的 `s01_well_side` 在 **x≈−1.07**（96×1 竖条，不是人字纹贴图）。人字纹是 `WellArrowA/C` 各 60 个 `s01_arrow`（父节点绕 Z **180°**）加上 `WellArrowB` 一列（内层是 **RectTransform**，抽取必须认，否则缺中间列）。`s01_arrow` clip **0.333s、本地 Y 0→−0.18**。A/C 转 180° 后世界 **上滑 +0.18**，中间 B 不转 **下滑 −0.18**。不要写成 bob 0.12。播放端注册 Mover 的 `|scrollY|` 门槛是 **0.05**，不要用 0.5（井列 −11.52 没事，人字纹 0.18 会被丢掉）。不要把井整体 +6.67 推到星空里。`s01_well_side` 不能当成 Chevron 去竖向平铺。绕 Z 四元数 `w≈0` 是 180°，不要把 `w` 换成 1（会变成 90°，箭头摊成横排）。

地面齿轮（`0H`，`GearOnLine`，不是空中轨）可见底边在地板顶下面 **0.20**（`GearBite`）。`s01_floor_1` 顶 y=**−1.670**，即舞台向下的 `GearRoadLine=1.67`。投影仍对 `ShadowTrackY`（红线再下 0.20），不跟齿轮下移。`GearRoadY = GearRoadLine − GearRadius` 留给车道 Y，不要改成咬进后的位置。空中齿轮不咬。

光带 `s01_light_1`：`LightUp` / `LightDown` 缩放把 4px 拉得很长，再 **绕左下角转 −90°**（`LightDown` 另 `flipX`）。中心要先绕 pivot 转过去再交给四边形，只绕贴图中心转会整条偏出画面。`RedLight` 的 Transform x 是 **−3.31**（相对 Mid）。clip `s01_light_act` 绑的是 **SpriteRenderer.m_Color.a**（0.75→0.55），不是 Transform.x，也不是显隐。左侧红光 **固定亮度**（用 SR alpha≈0.678），不要把这条曲线当位移，也不要套播放端 0.35～1 正弦闪。

#### scene_02 街景与弹窗（对照 `gamescene_02`，不要手搓克隆）

街序列（Floor 窗内）：店 → F → **第一组 `Mid_H`（H1 六叠）** → 街机 `Mid_I` → **`Mid_H (2)` 服务器+红绿灯（无 H1 叠窗）** → 店2 → 街机2 → **第二组 `Mid_H (1)`（静止两层脸屏）**。不要在店前/空挡再复制弹窗。PlayClient 对 `Floor`/`Water`/`Mid_2` 写 `phase=52.6` 只为把当前试玩曲的 14/26/57 combo 对上上述地标；**换曲必须重拟合**，同一 combo 不是同一街景时刻。

| 组 | 约 x | Animator | 行为 |
|----|------|----------|------|
| `Mid_H` / `Mid_H1` | 14.59 | **`s02_Mid_H1`** | 周期 **2.8167s**。底盘 `mid_H7` 一直开。inactive 的 `mid_H1_add`～`(4)` 在 **0.85 / 1.10 / 1.35 / 1.60 / 1.85** 逐层开。叠层本体是 **`bg_white` 窗框**，脸屏只有一张 `mid_H7`。 |
| `Mid_H (1)` / `Mid_H1` | 66.64 | **无** | `mid_H1_add` **预制已激活**，其余 add 保持关。静止两层（大脸屏 + 小窗），**不要**把 `s02_Mid_H1` 兜底套上来，也不要 `moveId`。 |
| `Mid_H (2)` | 30.94 | `s02_Mid_H2B` | 服务器机柜。clip 是微闪（约 1.92s 拉一下 IsActive），**不是** 0.12 上下漂。26/27 combo 粉红标题条应钉死。旁侧 `Mid_H2` 用 `s02_mid_H2`，根 Y 只有 ±0.027。 |

`s02_Mid_H1` 根位移（StreamedClip idx 0/1）：每开一层阶跃 **(−0.14, +0.14)**，第一屏往**左上抬**；add 本地 **(+0.14k, −0.14k)**，新窗留在前层。`LoopBlend=0`，播放用 **阶跃**（`moveStep=1`），禁止在 1.85→周期 之间把整组插值滑回（那是左右/上下晃）。Muscle 上的 `KeepOriginalPositionY` 不能当「不抬 Y」——官机六叠是往左上叠进铁网。`mid_H3`/`H4`/`H6` 标题比屏宽，必须裁到最近 `mid_H7`（`mx/my/mw/mh`），否则粉红条会铺到判定线。官机 sorting：`mid_H7` **−43**（后），框 **−42～−40**；叠层小窗 **−39+nest_i**（前），后弹出的盖住先弹出的。inactive 子物体要 `drive` 才抽得出 add 的 `bg_white`；没 Animator 的第二组不要 drive 关掉的 add。外围 **`far_rocket`** 贴图有抽到，但没有 Animator：挂 DOTween Move，相对 **(−20,0) / 38s**、`loops=-1` Restart。起点世界 x≈8.5（镜头右缘外）。item 上 `scrollLoop` 再 `Repeat` 到 **[0,20)** 只会往右挪到 x≥8.5，永远进不了镜头；必须升到 **层 loop=20 / period=38**，让 `k=−1` 拷贝从右侧飞过。贴图是 **98×312 竖条**，官机 Transform **Z≈+75°**（鼻尖朝左上方，沿 −X 飞），抽取写 `rotZ`，四边形旋转，不要把 PNG 先转成横图。贴图已是紫色，不要再乘 SR (0.55,0.46,0.82)。官机 order −78 会被 Far 楼 (−76～−60) 挡住，抽取提到 **−58**（远楼前、近景后）。

`MdSceneBg`：`popAt`/`popPeriod`/`popOff` 显隐；`moveId` 跟顶层 `moveT/X/Y`。遮罩 `ApplyClip` 也要跟 `moveId`，否则脸屏裁切跟不上左上抬。

字和图案的横移带符号：正值沿轴往 +X，负值往 −X。`scrollAngle` 是这条轴相对世界 X 的角度，0 就是水平。播放用 `u * scrollLoop`，不要再一律往左滑。

`warning`（四行一条，含 PEROstar）父节点 `s02_mid_h7`：**0→−6 / 5.5s**。同一屏要留相距 6 的三份，遮罩裁切，滑出后才有下一份接上。只留一份时字会滑出屏幕再跳回来。`mid_H5` 是 **0.23→−4.04 / 9s**，旁边那条在 +4.27。往 −X 的 `scrollLoop` 必须带上周期（`abs(dx)>0.05`），不要写成 period=0。街机柜面爱心孔是 `mid_I` 本体图；`Fx` 整棵（`s02_Fx_heart` / `mid_H` / `mid_I_machines_icon`）官机默认关，不要 drive 抽出来。

第二组脸图 `mid_H8`：父节点 `mid_H7_all_trotate` 转 **18.37°**，格子跟着斜，不是水平铺。滚动是这条斜轴上的 **+1.35 / 1.5s**（`s02_mid_h7B`，−2.161→−0.811），不是往世界 −X。`mid_B5` 是 **0→+5 / 8s**，同样往右，左右各留一份。

远楼：`Far_1` x=**−13.5**，循环 **15.72 / 12s**；`Far_2` x=**−6.67**，**12 / 13s**。铁丝网是 `Mid_2` 的 `mid_D1`（order **−53**，宽 2.85），楼在网后（order **−60～−76**）。`Far_1` 第一圈有两簇。左簇在窗内。右簇世界 x **2.59～6.50**（`Building_04`、`Building_03`、`building_01`、`building_02`、`Building_04`）超过 `origin+loop`（约 2.22），下一圈同一批在 `Far_1_B`（约 18.3 起）。两份都在窗外，网后就空一截。窗右缘放到本圈最左一块 + loop（约 **8.25**），下一圈交给播放端 `k` 拷贝，不要把 `Far_1_B` 再写进 layout。`Far_2` 后半都是 +12 的孪生，不用补。距遮罩中心超过宽×0.58 就丢，只对付 `warning` / `mid_H*` 铺满铁网，不要拿来删这些楼。

爱心公告栏是 `Mid_F/mid_F2`。第一间心在 x≈**7.92**，第二间在 **60.34**（`mid_F2_up2`）。框、立柱、柜台格都是 `bg_white` 缩放出来的，不是另一张图；棚沿下还有 4 条 `mid_F2_up3`。`trim_items` 按面积砍到 480 时这些细条最小，会被裁掉，画面上就只剩大底板、一只心和 1 条 up3。补的时候只加 `mid_F2` 子树里 layout 还没有的片。楼窗上的 `bg_white` 不要动。下一圈店（x≈100、152）交给 Floor 循环，不要写进 layout。

下栏必须按包内资源还原，禁止手搓整条横梁。`mid_F2_down1` 官机是 **Sliced**（`m_DrawMode=1`，border 左右各 9px，`m_Size`=(1.74, 0.07)，Transform scale=2），播放世界 **3.48×0.14 / y≈−0.93 / order −46**，不是 Simple 下的 0.48×0.28。格栏仍是 `mid_down/GameObject (7)` 下的 9 组双竖条：宽 **0.08** 与 **0.03**、高 **0.451**、中心 y≈**−0.6345**、order **−47**。不要在 y≈−0.89 再加合成长条。

爱心棚顶同样走官机 Sliced，不要手搓梯形、也不要把 `up1` 当普通四边形横拉（圆拱会被拉成尖顶，端帽会折叠）。`mid_F2_up1` border 左右各 **64px**，`m_Size`=(**10.38, 1.57**) 再乘约 0.50 的 Transform：前层世界 **5.208×0.79 / y=1.674 / order −44**，后层 **5.313×0.83 / y=1.706 / order −45**（在 `mid_up_bg` 的 1.02×1.05 上）。4 条 `mid_F2_up3` border 左右各 **6px**，`m_Size` 宽 **5.21 / 5.08 / 4.94 / 4.80**、高 0.05，y=**1.437 / 1.613 / 1.796 / 1.968**，order −43。抽取读 `m_DrawMode`+`m_Size`+`m_Border`，layout 写 `sliceL/sliceR`（UV）和 `capL/capR`（世界端帽宽）；播放端用三列九宫格网格，端帽不拉伸。

#### scene_03 井墙（对照 `gamescene_03`）

官机根下 `Far` / `Bg` / `Default`（里面才是 `Near`+`Mid`）。`Default`/`Near` **没有 Animator**，必须拆成独立滚动层，否则井、水、地冻在同一位移上。

| 层 | Animator | 循环 | 物件 |
|----|----------|------|------|
| `Far` | `s03_long_shot` | **11 / 4s** | `s03_far`，order **−60** |
| `Bg` | 无 | 0 | `s03_bg`，order **−80** |
| `Floor_default` | 跟 hexie 的 `s03_floor` | **2.5 / 0.5s** | `s03_floor`，order **−31** |
| `Water` | `s03_closs_shot` | **2.5 / 0.45s** | `s03_water` / `_b` / `_c` / `_d` 四条，order **−30** |
| `Light` | **固定** | clip `s03_bpm_light` 无曲线 | `s03_light` α≈**0.241**，w≈19.9。不要按层名正弦闪 |
| `Mid` | `s03_medium_shot` | **57.5 / 15s** | 井 `s03_well_1–4` order **−40**，骷髅 **−38** |

`Light` 精灵就在层根上（没有子节点 `s03_light` GO）。clip 名带 bpm，**genericBindings / StreamedClip 都空**，官机亮度钉 SR alpha。抽取见空曲线就 `blinkPeriod=0`，不要用层 period 1s 去闪。窗龛粉光是井墙贴图自带的，不是另抽的灯。

包内常激活的是 `Floor_hexie` / `Well4_hexie`。**官机美术走旁边关掉的 `Floor_default` / `Well4_default`**（`s03_floor`、`s03_well_4`、`s03_skeleton_1–5`）。`drive_uncensored` 用激活的 hexie 兄弟去开 `_default`，不要抽和谐替图。联动 Touhou / Wacca / Arknights / Leaf 保持关。

短循环地砖/水面按镜头宽补拷贝（`loop<8` 且条带 `span<14`）。井墙 loop=57.5、条带本身就长，**不要**再按镜头扩 `k`，否则整街叠在一起。

Well 成对，官机用 **`SpriteRenderer.flipX`**，缩放仍是 +1，**绕 pivot（左下 0,0）镜像**，不是绕贴图中心。抽取要把四边形中心翻到 pivot 另一侧，播放再用负缩放翻贴图。只翻贴图、中心不动时，B 侧的碗会翻到火苗对面：

- `Well1_A` 翻，`Well1_B` 不翻（同 local x，靠 flip 合成拱）
- `Well3_B` 翻（local x=+2.5）。碗在贴图左侧约 0.31；翻到 pivot 左侧后，火把 local x=2.04 才落进碗
- `Well4_B` 翻（local x=+5）
- 部分 `Well2` 翻

`Well4_B` 绕左下角翻转后，两龛中心相距 **2.5**（贴图宽），龛与龛相接。骷髅保持官机位置，正好落在各自龛心（A 与未翻的井同 x，B 在 +2.5）。不要再把第二具收到 B 井：旧的夹缝是中心没翻转时算出来的，再收一次两只会叠在同一龛里。Y 仍用 default 的脚位，不要改成 hexie 的 1.64。

火把是 Spine **`s03_fire`**（scale **0.005**），跟 Mid 滚，不是静图。Well1 上 scale **1**，Well3 柱头 scale **0.4**，clip **`S03_Nor1`**。图集只有三张：`Aa` 火、`Bb` 碗、`fx`。setup 槽 `A`/`B`/`fx` 就挂这三张；动画去拧 `Ac1–Ac14`、`Bc1–Bc3` 骨。`extract_md_spine` 给短图集补 `Ac*`/`Bc*`/`fx*` 同 uv 别名；`BindAtlas` 也对不上时按叶名落到 `Aa`/`Bb`/`fx`。`StripLike` 不要把 `Aa`/`Bb`/`Ac*`/`Bc*` 当扁条丢掉。

`PoseLoop`（场景火/幽灵，以及钉死 `TimeScale=0` 的采样）必须 **`DefaultMix=0`**，并且 **先 `SetToSetupPose` 再 `Apply`**。默认 mix 0.2s 且时间不走时，会卡在混合开头，火苗停在 setup，和碗错开。

场景幽灵在 **`FxGhost_special`**（三只 `s03_ghost`，clip `1start`），**不要**按层 loop 做 k 拷贝。sorting **queue 2500 / order −25**，低于飞近物件。飞近齿轮 **queue 3200 / order 12**，幽灵键 **3050 / 2**，禁止和场景幽灵共用同一材质队列（后生成的网格会盖住齿轮）。`s03_ghost_cy*` 保持关。

#### scene_04 雨街（对照 `gamescene_04`）

四层。**不要**把中景 69.6 收成 6–28（会变成 23，后半条街消失）。

| 层 | 循环 | 说明 |
|----|------|------|
| `Bg` | 0 | 钉镜头 |
| `Far` | **2.5 / 3.167s** | 远景一块，必须按镜头宽 k 拷 |
| `Mid` | **69.6 / 20s** | 整条楼 `s04_building_*`、`roof_*`。窗灯 `s04_light_a/b/c` 走 `blinkPeriod` |
| `Near` | **14.4 / 2.833s** | 近地砖 |

粒子挂根 `gi=-1`：地面溅雨（rot≈−58°，rate 10）+ 空中雨丝（rot −90°，rate 30）。shape Box。不要当 Circle。

#### scene_05 城堡（对照 `gamescene_05`）

树在 `Mid/Tree`，Animator **`s05_tree`**，循环 **31.85 / 7.87s**。父级 `Mid` 没有控制器，抽取必须把 `Tree` 拆成独立滚动层，否则 11 棵树冻在镜头里叠成外围乱景。水面 Animator **`s05_floor_c`**，精灵 **`s05_water`**（不是 christmas），块宽 **2.5**、循环 **5.0 / 0.75s**（≈6.67u/s，比地板 15.30/3.02s 快），Y 世界 **−5**、pivot (0,0) 后中心 **−3.85**、h=2.3、order **−30**（地板 −31 后面）。抽取只留 loop 内 2 块，播放必须按镜头宽补拷贝（`k` 不能只 ±1，否则右半屏没水）。**不要**给城堡套 scene_02 的 `phase=52.6`。不要用 fit_loop 扩成 12.5。远山 `s05_long_shot_1` **15.30 / 5.58s**；远树 `s05_long_shot_2` **12.50 / 10s**；天 `s05_long_shot_3` **6.66 / 7s**。视差由各层 period 决定，不要把树并进 loop=0 的 Mid。根上粒子 `s05_scene_sfx`。

| 层 | 循环 | 物件 |
|----|------|------|
| `Tree` | 31.85 / 7.87s | `s05_tree_1–5` |
| `Floor` | 15.30 / 3.02s | `s05_floor` |
| `Water` | **5.0 / 0.75s** | `s05_water` ×2 |
| `Mountain` | 15.30 / 5.58s | `s05_mountain_*` |
| `FarTree` | 12.50 / 10s | 远树 |
| `Bg` | 6.66 / 7s | 天 |

没有独立 `Light` 层。树干窗洞的粉/橙光画在 `s05_tree_*` 里，**固定亮度**，不要按灯名补 `blinkPeriod`。根上只有粒子 `s05_scene_sfx`。粉森林城堡是 scene_05；Hierarchy 出现 `Floor_default` / `s03_light` / `FxGhost_special` 的是 **scene_03 井墙**，不要混成 05。

#### scene_06 河边夜市（对照 `gamescene_06`）

鱼在独立层，Animator 记的是游动子物体（约 18→−2），**圈长用层 20 / 4.233s**，不要用鱼的世界跨度当 loop。`s06_fish` 绑的是 `fish_1` / `fish_2`（path≠0）：第一条 t=0 从 x=18 起游，第二条 **t=1.233s** 才起游（错开约 **5.83** 单位）。抽取后两条世界 x 约 **9.88** 与 **15.71**，y=**−3.15 / −3.40**，order **−37 / −35**（夹在水波层之间）。不要让两条叠在同一 x：贴图是鲤鱼，叠在一起会在水波里看起来像收音机。灯笼 `lantern` 挂中景 y≈1.0，不要掉进水面。无粒子。

| 层 | 循环 | 说明 |
|----|------|------|
| `Floor` | 4.0 / 0.8s | 地砖，短循环要 k 拷 |
| `water_gradient` | 0 | 钉镜头 |
| `Water` | 3.6 / 0.633s | `water` ×12：6 行 × 每行 2 块，块距 **1.8**，奇偶行错开 **0.9**。不要把邻居当成下一圈剪掉 |
| `fish` | **20 / 4.233s** | `fish` 两条，x 错开 5.83，不要同 x |
| `Mid` | **67 / 20s** | 灯笼、花砖 `middle_a_*`、`lantern`、`little_flower` |
| `Light` | period 1s | `gradient` α≈0.528，`s06_bpm_light` 无曲线，**固定亮度** |
| `far_floor` | 0 | |
| `Far` | 30 / 12s | `far_a_up/down` |
| `Bg_cat` | 19.5 / 15s | 猫剪影 |
| `Bg_moutain` | 19.5 / 21s | 远山（拼写保持 layout） |
| `Bg` | 0 | 天 |

#### scene_07 过山车（对照 `gamescene_07`）

| 层 | 循环 | belt | 物件 |
|----|------|------|------|
| `road` | 20.04 / 4.0833s | 0 | 彩虹条 `Road1`，中心 y=**−1.9**、h=1.44 |
| `AudioClips` | 22.4 / 7s | 0 | 呼吸灯 `AudioClip`，31×571，w=0.31、h=5.71，中心 y=**−1.9**，order **−70** |
| `cloud` | 25 / 50.0167s | 0 | |
| `sky` | 0 | 0 | |
| `mid` | **56.8 / 80s** | **1** | 过山车。速度 <2.5，不写 belt 会被改成 5u/s。不要改全局 2.5 |
| `farlight_all` | 40 / 32.0167s | 0 | `farlight_n`，order **−70**，α=**0.392**，pivot (0.5,0.5)，layout 的 x,y 是中心 |
| `far` | 18.46 / 89.9333s | 0 | |
| `stars` | 20 / 19s | 0 | |

呼吸灯是水平镜面，轴在方块中线 UV **0.5**（UV 0 在下方）。`MdSprite` 镜像：`|uv.y−0.5|` 小于 `_ClipUp` 或大于 `_ClipDown` 就丢掉。`_ClipUp` 是内半径 **0.0403**（彩虹实心条半宽），`_ClipDown` 是外半径。上下共用一个外半径，节奏一致。一格约 **0.0334** UV。安静 **3** 整格，最响约 **8** 整格，外沿对齐格顶，不要露出半格。官机 GCClip 下限 0.268、插值 0.32。播放端用频谱近似，再吸附到格顶。

摩天轮包名 **`sky wheel`**（有空格）。官机文本资源没有 `.skel` 后缀，正文是 Spine 3.8.99 JSON（宽 957.49、高 1058.5），必须存 `sky wheel.json`。scale **0.005**。两架都挂 `mid`：x=**7.746** 与 **23.042**，y=**−1.3718**，sx=sy=**0.9455**，clip **`animation`**。官机 MeshRenderer sortingOrder **−75**，在过山车轨道 **−80** 和耳机车厢 **−78** 前面、音墙 **−70** 后面。播放队列要和背景精灵一样用 **2450**。队列 2550 会盖住音墙。layout 写 0 会被当成 −35。`neonlight_all` 预制默认关，没有 Animator 去开，不要加。

蓝色耳机是 `s07_roller_coaster_icon`，order **−78**，两只都挂 `mid`。父节点 `s07_roller_coaster_icon_all` 的 clip **`s07_roller_coaster_icon`** 长 **80s**，两只车各一条本地位移，不是同一个位移带着走。`roller_coaster_icon (1)` 走 `moveT/X/Y`（x 0.17→37.4，y −0.06→4.29）。`(2)` 走 `moveT2/X2/Y2`（x 19.66→56.88）。播放端 `moveId==2` 读第二套，不要两只都套第一套。

远光 clip **`s07_farlight`**，周期 **30s**，五束绕各自父节点的 Z 来回扫（约每 5s 换一个角），不是平移。转轴在灯座（父节点），不在贴图中心。`orbit=1` 时播放端把中心绕 `pivotX/pivotY` 转 `spinZ−z0`。父节点 Transform 欧拉角（约 40°、40°、78°、0°、−35°）会被曲线**整段替换**，不是叠加上去：第 0 帧是 **10 / 83 / −2.5 / 80 / −35**。用 Transform 角烘焙再叠差值，t=25 时灯心会被甩到 y≈−4，绿光条进判定线和 UI。抽取时旋转父节点要用曲线第 0 帧。第一圈动画开局中心：**(0.437, 1.497)**、**(2.639, 1.402)**、**(11.749, 1.107)**、**(15.711, 1.318)**、**(27.586, 1.709)**。灯座 **(−1.60, −1.98)** 等。第二圈由层循环 +40 复制，不要再写一套灯。塔灯 (2.568, 2.685)、(28.438, 2.685) order **−84**，月亮 (9.030, 2.770)，不要再挪。scene_02 爱心店 Sliced 棚顶/下栏、scene_07 远光曲线第 0 帧，均已对照官机修好，不要再改。

#### scene_08 和风夜樱（对照 `gamescene_08`）

根在 **(−22.3, −3.66)**。联动 `*_Touhou`、粒子 `par_star` 保持关。不要全场景重抽 02/07。

| 层 | Animator | 循环 | 说明 |
|----|----------|------|------|
| `Ice_all` | 父节点 `scene_08` 播 **`08_ice`**，绑定 Ice_all | **62.1 / 11.3333s，循环** | 速度 **5.48**。361 帧里冰块一直往左，没有在 11 秒停住，所以 `mode` 是 `scroll`。4 块 `ice_1`（含 0.65 倍那块）。不要把 08_ice 传给 `water` / `Near_all` |
| `Near_all` | `08_near` | 62.1 / 12s | 地板 `floor_1`/`floor_2`。层原点用 Transform，不要改成曲线第 0 帧（房屋还在 Transform 上，地板单独前移会和房子错开） |
| `Mid_A_0` | `08_road` | **62.96 / 17.10s** | 灯笼实测速度 **3.682**，两套灯笼相距 **62.96**。不再用 Animator 圈长 51（那会把灯笼留在 2.83）。多出来的一丛 `sakura_shrub_3` 删掉 |
| `Mid_A_all` | `08_mid_A_1` | **84 / 23.81s** | 速度仍是 **63.5/18**。圈加到 **84**，月亮 70.96、第二间 69/73 能完整过镜再回卷。不要用 63.5：月亮刚进镜就会 Repeat 跳走。这一层不要 k 拷贝。开局 house **5.587 / 9.341**，第二间 **69.087 / 72.841**，月亮 **70.96**。鸟居 **19.13**。y=**0.388**。 |
| `Mid_B_1` | `08_mid_B_1` | **62.1 / 18s** | 小塔、竹子的官机间距是 **62.1**，速度 **3.45**。不再用 Animator 行程 66 |
| `Mid_B_all` | `08_mid_B` | 62.1 / 18s | 钟楼上半在 0.15s 的世界中心 **−31.33**（y=**0.44**），另一份隔一个循环。`Mid_B_2_Touhou` 关 |
| `Far_all` | `08_far` | 62.1 / 32s | 山、云。月亮 Spine **`s08_moon`** 挂 **Mid_A_all**，x=**70.96**（第二间 69.09/72.84 中间）。clip **`offset`**，y=**2.74**，scale **0.7**。倒影 `moon1–5` 同层，不要 k 拷贝。不要放 32.71。 |
| `water` | 无 | 0 | 静水，Transform scale.x=**200**，不要跟冰块横移 |
| `Mid_color` / `Bg` | 无 | 0 | 渐变条 `gradual_change`；天 `sky` |

父节点 Animator 只驱动绑定的那一层：读绑定 path 的哈希。**位移幅度 >2 的是层皮带**，已经写在 `group.loop`，不要再把子节点开局帧套上去（`Mid_B_2` Transform 13.8、开局 61.3，套了钟楼会整段出镜）。根上的 08_ice 不要传给 `water` / `Near_all`。

层 path 0 和子路径皮带都不要改成开局帧。场景 08 的片位置对齐官机进对局 0.15s 的抓取（`layout_runtime.json` 的包围盒中心），再按各层速度回推到开局。中景循环宽按 361 帧里的实测：神社 **63.5**、竹子 **62.1**、灯笼 **62.96**。开场以 `规则解析/scene_08_循环参考.json` 的左端为 0，片坐标按官机 `layout_frames.json` **t≈0.15s** 包围盒中心再平移到这套坐标（大树对齐，偏移约 **+11.78**）。Tight 网格精灵必须按 mesh 抠图，不能只切 textureRect（否则树/神社会带图集黑块）。这一圈两座神社：开局 **5.59 / 9.34**，第二座 layout **−32.67 / −28.91**（官机 −44.45/−40.69，Scene 编号 369–372）。不要折到 30.83。鸟居只留 **19.13**。`cattle` 官机 **30.660→42.44**（另一份 **−31.440**，圈长 62.1）。不要在 4.576 再补鸟居，也不要把第二座神社摆到 42/45。神社上半 y=**0.388**，不要用 0.13。`Mid_color` 渐变 `belt=1` 钉镜头，中心 **−3.87、宽 30**（官机 SpriteRenderer 502.2 是切片拉长，编辑器里会画出一条空带，不要照抄）。`Mid_A_0` 的 `light 1` 不要和 `Mid_B_1` 的 3.94 叠在一起：A 盏放鸟居左 **17.61**，与 24.07 成对（间距 6.46）；B 盏仍在神社左 **3.94**。+63.5 / +127 拷贝跟着挪。Bg 天空 **1.11 / 0.14**（从 12.89/11.92 左移 11.78）。地砖 `floor_1`/`floor_2` 已对调。中景物件跨不满一整圈（灯笼层只约 34、神社层约 47），只铺一份后段会只剩远山和地板。scene_08 只向右拷地板/冰/远山。`Mid_A_0` / `Mid_A_all` / `Mid_B_*` 不按 loop 拷。第二间是第一段 +63.5 再加月亮；第二间 cattle 在 **105.94**（42.44+63.5）。开局 `house_m 1` 左侧补牛圈尾 **−62.1**（树/竹/地砖/远山）。中间/绕回 `house_m 1` 补 `cattle_shrub_2/4`（57.51、62.04 与 121.01、125.54）。不拷 cattle 本体、不动原屋/鸟居。**不要用全图包围盒当各层 loop**（水/误挪的鸟居会把圈长拉到 120+，皮带错、开局空天）。鸟居上半在 **19.13**，冰块在 **−0.2 / 37.23 / 51.16 / 53.64**，水在 **23.14 宽 66**，渐变在 **6.27 宽 30**；这些片若再加一层 62 就会飞到镜头右边。`Mid_color` 名字带 Mid 但 **`belt=1`、loop=0**，不要发明 5u/s 皮带。粒子 Box 用 `m_Scale`（火花约 0.7×0.4，星空 y=240），不要拿 Sphere 的 radius 4.5 当盒子高，否则灯笼旁会出现两团品红。播完这一圈宽度后位移回到开头。灯笼层在歌曲约 **12.4 秒**会整层向右跳一圈（约 66），播放端按 62.96 循环，这一跳之后灯笼会差大约 3。

灯笼火花 clip **`08_spark`（1.333s 转/缩）**，播放端 `blinkPeriod=1.3333`。灯笼旁 `par_spark`（贴图 `spark`）跟灯。sakura 上 `par_petal`。远景夜空 Box 宽 240：`star_1` rate 60、`star_2` rate 700、`background_blue` rate 480，`maxP` 封 80。月亮 Spine **`s08_moon`** MeshRenderer order **−51**（不要写成 0 被当成 −35），queue 2450。灯笼层速度是 **3.682**，不跟中景的 62.1/18。不要给 Far 发明 2.5 皮带（`Far_all` 不是 ConveyorName）。

#### scene_09 墓园（对照 `gamescene_09`）

`Far_all` 自己没有位移。远景要拆开，各自循环 **48.14**：

| 层 | 循环 | 物件 |
|----|------|------|
| `Far_A` | 48.14 / 28s | 远墓碑、枯树、小屋 |
| `cloud` | 48.14 / 28.5s | 云 |
| `volcano` | 48.14 / 35s | 远山。`09_far_volcano` 只有时长，间距取两套山的 **48.14** |
| `Far_B` | 48.14 / 45s | 山影 `mountain`，层原点 x≈**−68.3** |
| `Far_C` | 48.14 / 60s | 月亮 `planet_01` |

近景坟、栅栏、雕像、墓门仍在 `Nearest` / `Mid_floor_*` / `Mid_B_all`。不要并回一个不滚的 `Far_all`。这些层名字不带 floor/near/mid/road，速度低于 2.5 也不能改成 5。月亮 `planet_glow` 同位置叠两张（9.23 和 7.85），去重必须比宽高，不能只比 xy。clip 不要把墓碑/叉/点当灯丢掉。

#### scene_10 初音舞台（对照 `gamescene_10` / `scene_10_miku`）

默认 `scene_10` 根无子节点，背景在 **`scene_10_miku`** 的 `ScrollableGear`。舞台没有 Animator，圈长在 `m_ScrollLength`，速度是普通键车道（13.85 / 44 帧）乘 `m_SpeedFactor`，相位 `m_Anchor * 圈长`。荧光棒在 Near 子节点，要拆开。圈长右缘外 0.9 的镜音连要留下。`SpeakerBoxMiddle1` 在 Mid1 左缘外（x≈−13.07），按 73.2 折回后约 60.13，裁窗左缘要扩约 6，不能丢。KAITO 和头上音符不浮动；镜音双子（含星）bob 0.12 / 1s（面积>6 和射灯 mask 也不能清掉）。粉棒 `s10_light stick_p_*` 开局 alpha=0，是叠在绿棒上的闪层，不要当缺图补。`par_star` 仍跳过。`0037` 初音、`0040`/`0041` 音符用官机 `kagamine` 1.333s 抬 0.258。射灯 `SpotLightAnim` / `spotlight_back` 都是 **4s** 扫 10°–20°（不是 2s 小摆）。`orbit` 转轴在灯头 `SpotLight1..6`，光束沿局部 +Y。根 `SpotLight` 的 `scale.x=-1` 只镜像灯位。PlayClient 里负角是 /、正角是 \\。扇形要朝 \\：开局负角（Light1/2/3/6）取正，开局正角（Light4/5）保持。每盏 SpotLight 独占一条 spin。灯头仍绕 pivot 沿 +Y。底座 `Haft` / `s10_spotlight*_3` 不跟着转，中心落在光束底端的轴线上，不要沿世界 Y 下移（斜灯会偏到光束旁边）。灯头 `s10_spotlight*_2` 跟光束同角，中心沿轴线吃进底端约半个灯头高。镜音两侧的短灯不是拱上的 SpotLight3/4：`Kagamine` 节点只有 Rin/Len，短灯是 `Mid1/SpotLight_E`（父级 scale.x=+1，世界角 **+20.78**）和镜像 `Mid1/SpotLight_F`（父级 scale.x=−1，世界角 **−20.78**）。`anim_bind` 只信 path=0，不要把 kagamine / s10_kaito 的子位移套到根上（镜音会叠两次、KAITO 被音符抬偏）。官机没有对局粒子。位置以写回 layout 为准，不要再手挪 KAITO/Rin/Len。

| 层 | 循环 | 说明 |
|----|------|------|
| `s10_shadow_front` | 0 | 前景阴影 |
| `LightStick2` | 15.4 / 2.330s | 近荧光棒 |
| `LightStick` | 15.4 / 2.039s | 近荧光棒 |
| `Floor` | 12.8 / 2.085s | 舞台地 |
| `Mid1` | **73.2 / 19.379s** | factor 0.40。音箱、立牌。不要把圈切在镜音连 65.92 |
| `Mid2` | **54.9 / 19.379s** | factor 0.30。双子、星 `s10_rin_star` bob |
| `Mid2_lightStick` | 54.9 / 19.379s | 舞台棒 |
| `Mid3` | 54.9 / 19.379s | |
| `Far` | 44.8 / 23.721s | 呼吸灯 `AudioClip`×120，`idx` 分频段 0–30，不要共用 band 0 |
| `Bg` | 0 | 天 |

射灯 `s10_spotlight*` `orbit`/`spin`，不要闪透明度。呼吸灯规则同 scene_07。小怪默认皮在 `Miku` 皮肤（身体/翅膀/连打 hurt），default 与 `Miku` 合成一套；`RinLen` 不用。连打轮换 `hurt_1/2/3`，不要播 `hurt_4`。

#### scene_11 愚人包（**1K 玩法专用**）

1K 玩法的场景地图**只有 11 号**。官谱默认 `MusicInfo.scene=scene_11`，`1O–1W` **没有**切到 11 的通道。

layout **0 组 0 件**。几乎无景，跳过抽取与播放皮带。不要发明循环。

**判定跟当前场景，不跟物件号。** `SceneTimeline.RulesFor(SceneAt)`：`scene_11` = 1K 单轨；`scene_01–10/12` = DFJK 双轨。`1O` 切走后立刻双轨，即使剩下的键仍是 `40/41/42`。反过来，scene_11 上普通 `01` 也按单轨打。物件外观仍按 ibms：`40–42` 永远地面 `1110/1111/1112_road`。

| 特化 | 约定 |
|------|------|
| 可玩键 | 只有地面愚人彩蛋 **`40` / `41` / `42`**（type 1）。对应 F8 **`1110_road` / `1111_road` / `1112_road`**。没有 air 包，pathway 一律当 0 |
| 判定 | **当前场景**：scene_11 时 D/F/J/K 打同一排地面，不画空中判定圈/空中车道；切到别的 scene 立刻双圈双轨 |
| 飞近 | 普通键 44/38/33，`in_nor_*`，Y = `RoadY` |
| 问答 `40`/`41` | type **50/51** 是通道 15 事件（scene_01 museyao），**不是** 1K 彩蛋 |

案例：`peropero_aniki_ranbu` map2（官机无 `1O–1W`）。Unity 使用 `JudgeRules` + `HitLane`，并将 `prefix===11` 强制归入 road。

#### scene_12 中式园林（对照 `gamescene_12`）

根 `(-6.67, -3.75)`。舞台没有 Animator，圈长在 `Scrollable.m_ScrollLength`，速度是普通键车道速度（13.85 / 44 帧 ≈ **9.443**）乘 `m_SpeedFactor`，开局相位 `m_Anchor * 圈长`。不要把 `Near` 拆成不滚的子层——它自己就有 Scrollable。`m_SpeedFactor` 可以小于 0.01（远山 0.008、太阳 0.001），不要当无效丢掉。联动 `Arknights*` / `s12_Arknights_*` 保持关。`par_star` 仍跳过。

粒子（`layout.particles`，顶层数组，不要嵌进 groups）：

- `mid_far` 上 **`s12_fx_fireworks`**：8 组大/小爆点 + 子节点 ring，共 16 个发射器。时长 2s、循环、错开 delay 0～1.2s，rate=0、burst 4–8，形状 Circle，贴图 **`fx_ring_line`**，order **−54**，世界空间模拟，跟着 mid_far 滚。父节点 emission 关着，不要抽。
- 场景根上 **`s12_light`**：近/远两条 `SingleSidedEdge` 光点（父节点旋转 160°，子节点再 −30.5°），贴图 **`fx_ring_5`**（官机 `fx_ring_4_blend` 在依赖包，用同包 `fx_ring_5`），order **−20 / −35**，Noise 开，不跟层滚（`gi=-1`）。发射口在镜头上方，粒子飘进画面。
- 播放端用 `Particles/Additive` 按这些参数建 `ParticleSystem`。不要用 Spine 烟花 `s12_Arknights_fireworks`。

动画：灯笼 `s12_middle_lantern_light` clip **`s12_lantern_light1` 2.17s** 闪的是 **SpriteRenderer 颜色**（typeID 212），不是位移。开局帧不要把这条曲线写进 x，光斑要对齐灯笼中心（子物体本地约 0, 0.099）。`biuld_4` 只有鱼 bob 0.12；长灯笼 `s12_middle_lantern2` 钉死，不要继承父节点浮动。猫、茶盏、鸟仍走 Spine clip。

| 层 | 圈长 / 因子 / 相位 | 循环 | 物件 |
|----|-------------------|------|------|
| `Near` | 50 / 0.60 / 0.1 | **50 / 8.8247s**，phase **5** | 地砖花 `s12_floor_*`、近井。官机 25 块 `s12_floor_1` 有两块在层原点 **左侧**（x≈−10.75/−8.71），圈长 50 铺满后头尾相差约 1，不是下一圈副本。clip 窗要向左扩到 `max(x)−loop`，密铺地砖不要用 2.0 双子容差，否则接缝缺瓦 |
| `Mid_building` | 100 / 0.45 / 0 | **100 / 23.53s** | 楼、茶盏 Spine `s12_middle_a_teacup`（order **−45**）、猫 `s12_middle_cat` clip `animation`（**−42**） |
| `mid_far` | 87.5 / 0.30 / 0 | **87.5 / 30.89s** | 树石灯笼、鸟 `s12_bird_*`（**−51 / −60**）、另一只猫 clip `animation2`（**−50**）。名字带 mid，速度 **2.83>2.5**，不要改成 5 |
| `far_building` | 102.09 / 0.25 / 0 | **102.09 / 43.24s** | 远楼 `s12_far_a/b/c` |
| `Far` | 40.68 / 0.15 / 0 | **40.68 / 28.72s** | 近假山 `s12_middle_rockery_1` |
| `Far_mountain` | 105 / 0.008 / 0 | **105 / 1390s** | 远山。名字不含 floor/near/mid/road，慢速保留 |
| `s12_far_pagoda` | 20 / 0.01 / 0 | **20 / 211.8s** | 塔 |
| `s12_far_sun` | 20 / 0.001 / 0 | **20 / 2118s** | 日 |
| `s12_far_cloud_1` | 30 / 0.05 / 0 | **30 / 63.54s** | 云 |
| `s12_far_cloud_2` | 20 / 0.01 / 0 | **20 / 211.8s** | 云 |
| `s12_far_cloud_3` | 40 / 0.02 / 0 | **40 / 211.8s** | 云 |
| `s12_gradation` / `s12_sky` | 无 | 0 | 渐变、天 |

clip 去重后不要用「w<0.8 都是子灯」去丢片：`s12_middle_d_tile`、鱼、`s12_middle_leaf_5` 会被删掉，墙面会缺瓦。

背景 Spine 读 **MeshRenderer.sortingOrder**，不要写成 0（播放端会当成 −35）。鸟 `s12_bird_*` 和 **`s12_middle_cat`** 与场景静图同一 **renderQueue 2450**，只靠 order 叠进树叶（mid_far 猫 **−50**，鸟 **−51 / −60**）。不要用装饰骨默认的 2550，否则猫会盖在紫叶前面。楼前那只 clip `animation` order **−42** 即使用 2450 也在中景树 −43/−44 前面。茶盏仍 2550。长按条子官包有 Sprite `1202_road_body`（880×118）。`HitPointInCatch` 仍用 scene_01 那张。

### 1.4 Unity 骨骼外观（已通：大型 0A/0B + Boss x01 + 摆锤/突击者）

谱面层仍只写 ibms；外形由 `AnimBinding` + 本机 F8 决定，**不**进 MDM。

| ibms | 条目 | scene_03 例 | 说明 |
|------|------|-------------|------|
| 01–03 | x10 | 0310 | 小怪 |
| 04–06 | x11 | 0311 | 中型1 |
| 07–09 | x12 | 0312 | 中型2 |
| **0A** | **x13** | **0313** | 大型1 |
| **0B** | **x14** | **0314** | 大型2（骷髅骑士：马盔 + 骑手头） |
| **0D / 0J** | **x15** | **0115** | 摆锤 `in_26/21/18`；0J 用对侧轨包，**不**翻转 ScaleY |
| **0C / 0I** | **x16** | **0116** | 突击者 `in_up_39/30/24`；根 x=0，Y 判定帧对轨一次；判定前不钉 HitX。0I 用对侧轨包，**不**翻转 ScaleY。缺包才方块，方块回退走 `Flight` 底边→起跳 |
| **22** | **0002_hp** | common | 红心；空中 `in_air`、地面 `in_road`；锚槽 `hp` |
| **23** | **0003_score** | common | 音符条；同上，锚槽 `score`。编辑器 `10` 不是此键 |
| **13** | **x06** | 0106 / 0506 | Boss 弹幕1 `in_boss_1_*`（飞近） |
| **14 / 15** | **x07 / x08** | 0107 / 0108 | Boss 弹幕2 `in_boss_2_*`（飞近） |
| **0H** | **x03** | **0103** | 齿轮 `in_nor_*`；到 TimeSec / 判定圈 `HitX=-3.85` 的当帧采样角色轨道并锁定结果，越过后不再按角色位置二次判定。若藏键、切模式或卡帧导致当帧未处理，超过 1/30s 后强制 PASS，禁止补判碰撞。异轨 PASS 无出场、继续飞；同轨碰撞切 `destroy`（约 0.533s）并继续按车道速度向左离场。地面锯盘底边在地板顶（`GearRoadLine=1.67`）下面再 **0.20**（`GearBite`），只咬地面齿轮。投影仍对 `ShadowTrackY`，`GearRoadY` 不改。空中齿轮仍在空中轨。判定圈留在原轨 |
| **18** | **x09** | 0109 | Boss 齿轮 `boss_1_nor_*`（飞近，HUD 算齿轮）；帧 21/18/15，同速更近生成；同轨撞击同样切 `destroy`（约 0.5–0.533s）并继续左移 |
| **0E** | **x05** | **0105 / 0705 / 0805 / 0905 / 1205** | 双押 `in_nor_*`；**走本轨**，不是中线。scene_01 的 `laser_ball`/`laser_circle` 是连线两端能量球，不是柱；`BeamSlot`/`HideLaserSlots` 不能因名字带 `laser` 清掉地面那份，否则下方球缺失。scene_07 是方块/圆 `AA`/`BB`/`squareU`/`roundU`。`0705_air` 的 `dust_cut` EndSlot 指向自己，裁剪会把几何剪光，这种裁剪不要开。scene_08 图集 `0805/image/`。scene_09 脸对 **head** 不要对 `feidie`（飞碟在头下，对飞碟光条变短）。连线 `laser_pink` 是 43×2 细条，挂 `laser`→`laser_body`，按轨距拉伸。scene_03 连线 `0305/images/laser` 59×5，只在 **air** 包，挂在头骨下的 `laser`（附件 scaleY 已是 35、朝下）；头一转先把 `laser` 世界旋转掰回 0，平移用父骨 `WorldToLocal`，对轨用头贴图中心不要对头骨。空中包连线 **朝地面**（dir −1），scene_12 地面包 **朝空中**（dir +1）；0805 的 `laser` 就在 `body` 上，不要用 laser 相对 body 的 Y 猜方向。scene_12 官机是**粉柱**（`laser_pink` 58×268），闪电是叠在柱上的 `fx` 纹理（12×266），不是单独那条折线。粉柱+闪电在 **`1205_road`**；空中 `1205_air` 也有粉柱但没有闪电，同刻要关掉空中那份连线，只留地面一份。粉柱挂在 `body`（setup 还有 0.67）下面；`laser` 骨在柱顶。按粉柱包围盒缩放到两花心间距 2.125 再平移对齐，不要对 `laser.Y` 和 `ScaleY` 连乘，也不要单独把 `laser2`（同尺寸 `laser_pink_2`）再拉成第二根。粉柱和柱上 `fx` 画在莲花后面。对轨用 `FlowerHeart`，不要用先扫到的 `petal_H` |
| **0G** | **x04** | **0104 / 0804** | 普通连打 `in_down_27/23/20`；钉中线；`16/17` **不**画此条。scene_08 条目是 `0804`（无 `_air/_road`），图集必须用共用 `s08_atlas` 的 UV；独立抽出来的 `0804.png` 几乎是空条，只会看见 `R1_Fx1` 白光 |
| **x01 本体** | **x01** | **0101 / 0501** | 通道 15 演出；`11/12/16/17` **不**再挂飞近，见 §2.1 |

飞近物件实现按职责拆分：`PlayDirector.Actors` 管实例/资源/渲染层，`ActorSpine` 管 Spine 飞近、命中离场与 0E 连线，`ActorPlacement` 管通用物件/钟摆/静图定位，`HoldRendering` 管长条、头尾星与透明度；Boss 本体仍在 `PlayDirector.Boss`（`MdSpine.LoadBoss`）。角色方面，轨道与滞空状态在 `PlayDirector.Girl`，Hold/双押/副轨虚影在 `GirlHold`，手动输入和命中动画映射在 `GirlActions`。这些文件都是同一个 partial `PlayDirector`，跨文件可直接共享状态，但新增逻辑应落到对应职责文件，避免重新堆回 `Actors.cs` 或 `Girl.cs`。

`MdSpine` 约定（scene_03 0B 已对照官机）：

- `SkeletonBinary.Scale` / `scale.json` = **0.005**（官机 `SkeletonDataAsset.scale`）。0A/0B 比小怪大是数据带来的，**不要**再用 `SpriteBox` 把它们拉到和小怪一样高。击中后 `out_p`/`out_g` 沿用飞近时的缩放：飞散的头、手、火苗会把包围盒撑过 2.8，再按包围盒缩小会变成一堆小碎块。打击段也不要把 Miku/origin 皮肤填回被动画清掉的槽，否则碎块旁边还站着完整的一只。
- 图集 UV / PMA（已对照 0101 飞碟 + `char_*` bunny）：官机 spine-unity 在 `SkeletonBinary` **之前** `Atlas.FlipV()`（只做 `v=1-v`、`v2=1-v2`）。`x01`（四位数字且 `[2]=='0' && [3]=='1'`）和 `char_*` 走这条：`Pack.VFlipped=true`，绘制时 **不要**再 `1-v`，`SpinePMA` `_PmaTex=1`（图集已是 PMA，frag 不再 `rgb*=a`）。音符 **不** FlipV，绘制时 `1-v`，`_PmaTex=0` 再预乘。`FileTex` 关 `alphaIsTransparency`；shader `ZWrite Off` / `ZTest Always`，混合 One / OneMinusSrcAlpha。`Ensure()` 要在静态 `Shader` 被域重载掏空后重新 `Find`。改完 Unity 脚本必须完全停 Play 再进，否则 `MdSpine.Cache` 仍是旧包。
- 槽位默认附件名经常是短名 `head`，皮肤/图集是 `0314/images_air/head`。按 **本包前缀**（`{条目}/images_air/` 或 `images_road/`）解析；`0804` 连打图集路径是 `{条目}/images/`（没有 air/road），不要猜成 `0804/images_air/`。**禁止**在共用 `s03_atlas` 上把 `head` 绑到别的 xNN（否则轨道上会多飞一颗 0314 脸）。
- 官中脸是 **`head_hexie` + `eyes*_hexie` 网格**（爱心眼），走骨骼网格和 hexie 裁剪。不要用图集 Region 按头盔包框铺一层（会盖住马头或尺寸不对）。
- 默认空着的 `head2` / `eyes3` 不要强行挂附件。
- 不要改 atlas POT 顶 pad 的 UV（会整页错位）。不要按像素 1:1 或 `Skeleton.ScaleX`（常为 1）画补丁头（会撑满屏幕）。
- 物件投影与其余贴图：F8 槽里的 `shadow`、`glow`/`highlight`、`fx`/`line`/`laser`/`trail`、`smoke`/`scrap`/`dust`、`explode`/`water` 等 **都画**。`BindAtlas` 仍只绑本包前缀，禁止共用图集串到别的 xNN。这些槽 **不计入** `BodyOffset`（`IsFxSlot`；`body_shadow` 也算投影）。摆放另读 `ShadowCenter`，对到 `ShadowTrackY`，不要把椭圆算进本体中心。扁条用 `keepStrip`，不要 `StripLike` 误杀。场景火 `Aa`/`Bb`/`Ac*`/`Bc*` 不是扁条。齿轮没有 `out_p`：异轨 PASS 过线飞过，同轨撞击改播 x03/x09 的 `destroy`。不要另裁静图投影。
- 钉死时间的采样（`Pose` / `PoseLoop`，`TimeScale=0`）：`AnimationStateData.DefaultMix=0`，每帧 **`SetToSetupPose` 再 `Apply`**。默认 0.2s mix 会停在开头，场景火苗停在 setup、和碗错位。
- 图层：场景 `s03_ghost` queue **2500** / order **−25**；飞近齿轮 **3200 / 12**，幽灵键 **3050 / 2**。页网格用材质实例写 `renderQueue`，不要改共享材质。齿轮贴图不能被场景幽灵或幽灵键盖住。
- 摆锤优先无和谐 `wooden`，再 `wooden_hexie`。Unity **已接 x15 `in_*`**：根 x=0，Y 用判定帧 **锤面中心**（wooden 贴图，不是吊点骨）对轨一次，**不要**再叠 `TryPose` 或自画绳。判定前即使已判 Perfect 也不钉 HitX。击中同样根 x=0 播 `out_p`。锤面在 **origin** 皮肤，入场和 `out_p` 都要 `FillOrigin`（`out_p` 只动骨骼；不补槽就只剩绳子）。缺 F8 才回退静图 + LineRenderer。`.skel` 不进 MDM；StreamingAssets `MdLooks` gitignore。

仍走静图的：长按条（头尾星星 + body Simple + `note_up/down` overlay，不绑 `0102_air`）、判定圈 `HitPointIn/Out`（§1.3d，已导入）、无对应 F8 条目的键。摆锤/突击者/齿轮/幽灵/双押/普通连打有 F8 走骨骼。近战 `11/12` 与 Boss 连打 `16/17` **不要**再补方块。红心/音符见上 §1.3c，Boss 本体见 §2.1。齿轮 **不要** `out_p`；同轨碰撞只在判定点切入 `destroy`，根节点仍继续向左，PASS 也继续飞。

**来源与工具**：MelonLoader mod **MDSceneGrabber**（独立的 `Muse dash mod支持/MDSceneGrabber` 工程）。热键、F10 不限时、`AfterDumpCommand`、`tools/consolidate.py`、`素材整合/scene_alias.json`（`scene_05`→`城堡地图`）以该目录 **README.md** 为准，这里不复述。分析脚本 `tools/analyze_telemetry.py`，报告在 `out\telemetry_reports\`。另对照官包 notedata（des / prefab_name）。

仍未录到（数值按规则推定）：`0I` 地面档 1/3、`0J` 地面/空中档 1。`0I` 空中档 1 官谱没有，不用录。

---

## 二、事件（type 0 或 ≥9 → 通道 15）

### 2.1 Boss 脚本

| 官方 ibms | type | 官 des | 编辑器 | MDM | 案例 |
|-----------|------|--------|--------|-----|------|
| 1A | 0 | 入场 | Boss 入场 | 1A | mopemope / milk / umpopoff |
| 1B | 0 | 退场 | Boss 退场 | 1B | milk / umpopoff / megalovania_map4 |
| 1C | 0 | 远程1 开始 | Boss 准备1 | 1C | mopemope / milk / umpopoff / megalovania_map4 |
| 1D | 0 | 远程1 结束 | Boss 结束1 | 1D | zhuguang_zhuzhou（接在 1C 后，不走 1G） |
| 1E | 0 | 远程2 开始 | Boss 准备2 | 1E | megalovania_map4（11）；mopemope 111 条仍待复核 |
| 1F | 0 | 远程2 结束 | Boss 结束2 | 1F | mopemope / milk / umpopoff / megalovania_map4 |
| 1G | 0 | 远程1→2 | 换阶段 1→2 | 1G | mopemope / milk / umpopoff / megalovania_map4 |
| 1H | 0 | 远程2→1 | 换阶段 2→1 | 1H | umpopoff（112.515s） |
| 1L | 14 | BOSS 消失 | 隐藏 Boss | 1L | megalovania_map4；change_the_game map4（48.6s，紧接 1A/1M） |
| 1M | 15 | BOSS 复现 | 显示 Boss | 1M | megalovania_map4；change_the_game map4（50.4s） |

Unity **已生成** 场景包 **`x01`**（`0101` / `0501`…，`MdSpine.LoadBoss`）。高度用待机第 0 帧的 `shadow` 对到 `ShadowTrackY`（和飞近物件同一条地面投影）。没有投影槽才退回两轨中线 `BodyY`。水平在判定线到画面右缘的 **3:1**（`BodyAlong=0.75`，约 x=4.04；按实际相机宽高比收右缘，0.875 会裁掉 x01 右侧）。本体 **不算击打**。scene_11 没有 `1101`，日志 `Boss 本体没有骨骼` 只针对该景，不要和 0101 贴图问题混。

图层（不要和角色同一通道）：本体 **Default** queue **3300** / order **80** / z=**−3.0**（在背景负 order、速度线 −40 / 2400 前面）。F6 测试角色 **Character** 层 queue **3600** / order **10** / z=**−3.2**。2J 闪光 **Overlay**（order≥100）。`SpinePMA` `ZTest Always`，2D 不要靠深度挡环面细节。

皮肤：`WearBossSkin` = Default+**origin**（0301 裙子/大腿只在 origin），**不穿 hexie**。`WearPlaySkin` 的 origin **只**给 `GirlTest`。远攻空槽 `FillBossParts` 只补 `ufo_*` / 灯 / 脚 / 身体，**不要**把 `Weapon_*` / `Weapon_box` 按 setup 填回去（待机座舱本就空着；`far_atk_2` 的卡带是片段自己挂的）。`FillOriginSlots` 给角色待机，不给 Boss。

0101 飞碟：`BossSkel` = 有 `standby` 且（`far_atk_1_standby` / `far_atk_1` / `close_atk_1_48`）。**不要**对 Boss 开 `ClipStart`（大轮廓 clip 会把座舱剪成空环）。`keepStrip` 对 Boss 常开。`ufo_end_1` 是黄紫外壳（粉线画在这张 Region 上），`ufo_end_2` 是座舱台，灯是 `ufo_light_*`。`ufo_end` / `ufo_light` / `ufo_top` 走前页（order+4）。**禁止**把 Boss 走 `BeamUnderFace`：`ufo_light` 名字带 `light` 会被当成双押光柱画到黄环后面，环上的灯和粉线会消失。`BeamSlot` 对 `ufo`/`weapon` 直接排除。

通道 15：`1A`→`in` 再 `standby`；`1C`→`far_atk_1_start`/`standby`，`1D`→`far_atk_1_end`（0.7s）再待机；`1E/1F` 同理 `far_atk_2_*`；`1G/1H`→`atk_1_to_2` / `atk_2_to_1`；`1L` 隐藏；`1M` 只解除 1L（**1B / 17 退场后 1M 不回场**，要等退场动画结束之后的 `1A`）；`1B`→`out`（播完隐藏，不要回收 `standby`）。夹在 `1A` 与 `1M` 之间不到 1s 的 `1B` 不算退场。有 `1A/1B` 的谱：`1A` 之前与 `1L`/`1B` 之后本体不在场；`13`/`11`/`1E` **不能**把 x01 召回来（官机这段只飞弹幕/收集物）。

弹幕 `13` 飞近时本体播 `far_atk_1_air`/`far_atk_1_road`（1s），`14`/`15` 播 `far_atk_2`；投完接 `far_atk_*_end` 0.7s，不要投完直接切待机。近战 `11/12`：判定前 `close_atk_1_48` / `close_atk_2_24`（48/24 帧）。命中后的演出保持到挥砍片段结束（`Close1Dur` 2.6s / `Close2Dur` 1.8s，命中在 48/30、24/30，其后约 1s）。**打中播完整 `hurt` 击退**（0101 约 1.0s，0601 约 0.9s；0101 在 0.7s 已站稳，0601 要到 0.9s）。后面 1.5s 内有真 `1B`（chrome_vox map1–3：132.22s 的 `11` 接 133.10s 的 `1B`）也先 `hurt`，挥砍窗口结束再 `out`，不要用斩击把击退整段换掉。漏接把这段换成 `close_atk` 续播到回场结束。0601 的 `close_atk_1_48` 在约 2.30–2.60s 骨 `X` 才从右侧回到原位。连打 `16`：判定前 `multi_atk_48`（约 1.6s）→ 期间 `multi_atk_hurt` → 结束后从片段开头播完整 `multi_atk_out` 回场（约 1s；0601 约 0.63s 才从右侧出现、0.94s 回原位）。有长度的 `17`（length>0.02）：**没有** `multi_atk_48` 前置，也不从 `multi_atk_48_end` 第 0 帧接。判定瞬间切 `multi_atk_hurt`（0101 骨 X 已在连打位，画面是瞬移），打完接 `multi_atk_hurt_end`（约 1s，`HurtEndSec`=31/30，从连打位往右出屏）。`multi_atk_48_end` 第 0 帧在右侧蓄力位，接上去会像退场后又出场。点长 `17`（length≤0.02）从判定起直接 `multi_atk_hurt_end`。退场播完保持隐藏，直到退场结束之后的 `1A`。这些本体动画 **不算击打**。

本体 Transform 只按 **待机 `standby` 第 0 帧** 对位：X 用 `BodyX`，Y 用该帧 `shadow` 对到 `ShadowTrackY`（没有投影槽才用 `BodyY`）。`in` / `close_atk` / `multi_atk` 的走位走骨骼位移。不要用当前 clip 第 0 帧当锚，也不要每帧 `dest−当前 BodyOffset`。`11/12` 与 `16/17` **不要**再画飞近（x04 / 方块）。`11/12` 与 `16` 的判定点是本体冲到判定线（`close_atk` 约 1.63s、`multi_atk_48` 约 1.6s）。有长度的 `17` 不走这段冲刺，判定瞬间已经在连打位。chrome_vox map4：132.22s 的 `11`、37.98s 的长条 `17`（瞬移连打再往右退场）、141.29s 的点长 `17`。判定仍按近战/连打。没接住连打：`multi_atk_48_end` 穿过判定线；`16` 再从片段开头播完整 `multi_atk_out` 回场（0101 约 0.27s 从右侧出现，0601 约 0.63s 才出现）。接住的长条 `17` 不播 `multi_atk_48_end`。`0G` 才画 x04。优先级 **近战 `close_atk`/`hurt` > 弹幕 `far_atk` > 连打 `multi_atk`**。可打的只有飞近：**弹幕 `13/14/15`**、**近战 `11/12`**、Boss 连打 `16/17`。`0G` 不驱动本体。不要在 `multi_atk` 期间把旁边的 `0G` 藏掉。`18` 仍是齿轮飞近。没有脚本时，有 11–18 / 3x 的区间前后 2.5s / 1.5s 循环 `standby`。同一 tick 低难度 `11`、高难度改 `16` 见 §十五 change_the_game。

### 2.2 切场景 / 显隐 / Auto

| 官方 ibms | type | 官 des | 编辑器 | MDM | 案例 |
|-----------|------|--------|--------|-----|------|
| 1J | 12 | 谱面消失 | 隐藏音符 | 1J | super_battleworn_insomniac（map1–3 各 1 次；map4 狂闪 180） |
| 1K | 13 | 谱面复现 | 显示音符 | 1K | 同上，与 1J 成对 |
| 1O | 9 | 场景切换_1 | 切场景 空间站 | 1O | mopemope |
| 1P | 9 | 场景切换_2 | 切场景 复古城 | 1P | mopemope |
| 1Q | 9 | 场景切换_3 | 切场景 城堡 | 1Q | mopemope |
| 1R | 9 | 场景切换_4 | 切场景 雨夜 | 1R | mopemope |
| 1S | 9 | 场景切换_5 | 切场景 糖果 | 1S | mopemope |
| 1T | 9 | 场景切换_6 | 切场景 东方 | 1T | mopemope |
| 1U | 9 | 场景切换_7 | 切场景 GC |  | 官包无此谱案例（GC 联动未实装） |
| 1V | 9 | 场景切换_8 | 切场景 车万 | 1V | flandre map5（29.567s，场景连切序列内） |
| 1W | 9 | 场景切换_9 | 切场景 DJMAX |  | 官包无此谱案例（DJMAX 联动未实装） |
| 1X | 9 | 场景切换_10 | （调色板无） | 1X | **scene_10**（与 1W 同景）。Unity `SceneTimeline` |
| 1Y | 9 | 场景切换_12 | （调色板无） | 1Y | **scene_12**。satsui_azashita map4（24 次连闪）；cubibibibism map3/4（各 1） |
| 25 | 16 | 场景消失 | 隐藏背景 | 25 | megalovania_map4（Unity `BgTimeline` 藏 `MdSceneBg`） |
| 26 | 17 | 场景复现 | 显示背景 | 26 | megalovania_map4 |
| 2Y | 10 | 自动打开启 | Auto 开 |  | 官包无此谱案例,可能和选中梦游少女相关 （只有 2Z 关） |
| 2Z | 11 | 自动打关闭 | Auto 关 | 2Z | museyao map2（70.286 / 98.571，**没有 2Y**） |

### 2.3 画面 / 滤镜

| 官方 ibms | type | 官 des | 编辑器 Names | MDM | 案例 |
|-----------|------|--------|--------------|-----|------|
| 27 | 18 | 画面向上滚动 | 画面上滚 | 27 | super_battleworn_insomniac（与 28 交错抖动，29 关掉；Unity 滚整景 Y） |
| 28 | 19 | 画面向下滚动 | 画面下滚 | 28 | 同上 |
| 29 | 20 | 画面滚动关闭 | 画面滚动关 | 29 | 同上 |
| 2A | 21 | 随机波纹 | 判定线波纹 开 | 2A | zhuguang_zhuzhou（开场 1.723s，与 2C/2E 叠） |
| 2B | 22 | 随机波纹关闭 | 判定线波纹 关 | 2B | zhuguang_zhuzhou（5.777s） |
| 2C | 23 | RGB 分离 | 色散 开 | 2C | zhuguang_zhuzhou（1.824s） |
| 2D | 24 | RGB 分离关闭 | 色散 关 | 2D | zhuguang_zhuzhou（5.878s） |
| 2E | 25 | 暗边 | 暗角 开 | 2E | zhuguang_zhuzhou；peropero 104–115s 8 对（1 拍开 / 3 拍关）。Unity `FxTimeline` + `MdScreenFx`，淡入约 0.08s |
| 2F | 26 | 暗边关闭 | 暗角 关 | 2F | 同上 |
| 2G | 27 | 花屏 | TV噪点 开 | 2G | megalovania_map4（18 对开/关） |
| 2H | 28 | 花屏关闭 | TV噪点 关 | 2H | megalovania_map4 |
| 2I | 29 | 闪光开始 | 闪光 始 | 2I | scarlet（109.3s 起 `2I→2J→2K` 三连，3 组） |
| 2J | 30 | 闪光高峰 | 闪光 中 | 2J | super_battleworn_insomniac map4（无 2I，直接高峰） |
| 2K | 31 | 闪光结束 | 闪光 末 | 2K | 同上，与 2J 成对 |
| 2L | 32 | Note 停滞 | （Names 无） |  | 官包无此谱案例 |
| 2M | 32 | note 停滞结束 | （Names 无） |  | 官包无此谱案例 |
| 2N | 32 | 背景停滞 | 背景静止 开 | 2N | cubibibibism map1–4（71）；satsui_azashita map4（197 连闪）；Unity 冻横向循环与 27/28 |
| 2O | 32 | 背景停滞结束 | 背景静止 关 | 2O | 同上，与 2N 成对 |
| 2P | 33 | 像素化开始 | 马赛克 开 | 2P | rush_hour map1–4（各 1 对，69.6s 起） |
| 2Q | 34 | 像素化结束 | 马赛克 关 | 2Q | 同上，与 2P 成对 |
| 2R | 35 | 黑白滤镜开始 | 棕褐 开 | 2R | zhuguang_zhuzhou map4 121.723s（叠 2E + 1B） |
| 2S | 36 | 黑白滤镜结束 | 棕褐 关 | 2S | zhuguang_zhuzhou 134.493s |
| 2T | 37 | 速度线_黑 | 集中线 黑 | 2T | peropero_aniki_ranbu map2（115.714s，配 2V 结束，无 2U 白） |
| 2U | 38 | 速度线_白 | 集中线 白 | 2U | scarlet（113.069s，无 2T） |
| 2V | 39 | 速度线结束 | 集中线 关 | 2V | scarlet 116.881s |
| 2W | 48 | 怀旧滤镜开始 | （Names 无 2W） | 2W | satsui_azashita map1–4（3 对）；cubibibibism map1–4（1 对） |
| 2X | 49 | 怀旧滤镜结束 | （另有 type2 带提示齿轮共用 2X，需按 type 拆） | 2X | 同上，与 2W 成对（按 type 49，勿混 type2） |
| CM–CT | 40–47 | 闪光颜色 白黑红绿蓝青洋红黄 | （调色板无） | 同 ibms | **CN（黑）** worlds_end_loneliness；**CO（红）** flandre map5；**CM（白）** cubibibibism map2–4 + coooonsultant map5；**CS（洋红）** cubibibibism map2–4；`CP CQ CR CT`（绿蓝青黄）官包无此谱案例 |
| 40 / 41 | 50 / 51 | 问答开始 / 结束 | （另有愚人彩蛋 type1 共用号） | 40 / 41 | museyao map2（type **50/51** 问答，5 对 + Boss 段 1 对） |
| M3 / M4 / M8 / M9 | 52–54 | 切鱼演示/实操/评价 | （调色板无） | M3/M4/M8/M9 | serve_sushi_serve map2（`M3` 班乔演示 11、`M4` 布若实操 11、`M8` 切鱼评价 8、`M9` 顾客评价 3） |

Unity **已接**（`FxTimeline`，与切景/藏景独立）：`2E/2F` 暗角、`2T/2U/2V` 黑/白集中线、`2I/2J/2K` 闪光（`CM–CT` 染色）、`2R/2S` 棕褐叠、`2G/2H` 噪点、`2A` 判定圈闪、`1J/1K` 藏键。切景 `1O–1Y` 换背景与物件包，**不拆飞近物件**。未接真后处理：`2C` 色散、`2P` 马赛克、`2W` 怀旧、`2L` 音符停滞。

### 2.4 自制流速（官包 **没有** 这些 ibms）

编辑器事件轨专用。官谱转化 **不要写**。umpopoff 已证明不加 `0Q` 键位与官机一致，变快来自物件 `speed`。

| ibms | 编辑器 | 说明 | 案例 |
|------|--------|------|------|
| 0O | 流速1 双 | | |
| 0P | 流速2 双 | | |
| 0Q | 流速3 双 | | |
| 0R / 0S / 0T | 流速1–3 下 | | |
| 0U / 0V / 0W | 流速1–3 上 | | |

编辑器另有 `0X` / `0Y` / `0Z` = 「变速 速度1–3」批量改档标记（逐音符速度的快捷方式，落成 `0S` 台阶）。同为编辑器专用、官谱转化**不要写**。

---

## 三、不要写入词典的对应

| 官方 | 不转成 |
|------|--------|
| `NoteConfigData.speed` 1/2/3 | `0O/0P/0Q` |
| `MusicInfo.bpm` 区间（如 123~177） | 通道 08 |
| `StageInfo.bpm` | 只写 `#BPM` 标称网格 |
| 长按中间 / 尾 `length=0` 同 uid | 独立 0F |
| type 0 且 ibms 空 | 丢掉 |
| `dialogEvents` 对白 | 无 MDM 通道（编辑器没有对话框轨） |

---

## 四、谱面贡献

1、ut_megalovania_map4

`music_ut_megalovania_assets_all.bundle`，曲名 MEGALOVANIA / Toby Fox，`scene_03`，网格 BPM 240，`difficulty4=¿`，谱师 DETERMINATION，delay 0。

raw 1989 → tap 1193 / hold 79 / event 88。导出 MDM 后 map4 **`0H` 348 颗往返齐全**（旧 `notes_to_taps_holds` 只放行条内 `0H` 会丢掉其中 47 颗齿轮；现条内可玩键全留）。HUD 把 ibms `18` 算进「齿轮」而不是 Boss（`categoryOf` / `isGear`），连击规则不变。

本张补进词典的新案例：可玩 `02 03 0B 0E 22`；事件 **`1L 1M 25 26 2G 2H`**，并给 `1E` 一条干净的远程2开始样本（11 次，137s 与 `1C` 交错）。

仍空着、本张没有的事件：`1D`、`1J 1K`、`1U–1Y`、`2Y 2Z`；画面除 `2G/2H` 外的 `27–2F`、`2I–2X`；以及 `CM–CT`、问答、切鱼。可玩仍空：`05 06 08 09 0I 0J`、`11 16 17 20`、东方弹 `30+`。

### 对话框（有配置，MDM 空着）

Unity TypeTree **读不到** `dialogEvents`，但 map4 的 `SerializedBytes` 尾部有完整字典：`ChineseS / ChineseT / English / Japanese / Korean` → `List<GameDialogArgs>`。

结构与 `dump.cs` 一致：`time`、`dialogType`（0 角色 / **1 Boss** / 2 UI）、`text`、颜色、字号、`dialogState`（Show/Hide）。同一句按打字机存递增前缀。

截图「即使你反复地游玩」就是其中一句（英文 `even if you play it over and over again,`），Boss 气泡，**不是** note ibms。编辑器没有对话框轨，转化不写，词典 MDM 列空着。

**约 130 combo 前后（实机画面：Boss 句上叠 MISS、底下「战斗」）**  
`dialogType`：1=Boss 气泡，2=UI（菜单/MISS）。`dialogState`：0/1 显示，2 隐藏。  
「MISS」官包写成 **`M I S S`**（字母空格），「战斗」写成 **`战 斗`**。

| 秒 | 约 combo | type | 文本 |
|----|----------|------|------|
| 42.980 | ~106 | Boss 显 | 即使你反复地游玩 |
| **44.980** | **~109** | **Boss 显** | **我也没有更多的话可以说了。** |
| **46.980** | **~124** | **UI 显** | **战 斗**（底栏） |
| **47.480** | **~127** | Boss 隐 + 战斗隐 + **UI 显 `M I S S`** | 叠在上一句上，即截图 |
| 47.980 | ~128 | MISS 隐；Boss 显 | 想必这些攻击已经简单到你可以预测了吧。 |
| 48.050–48.500 | ~128 | UI 打字 | `*  你没有吃掉传说英雄。` |
| 49.500 | | 检查句隐 | |
| 54.600 | **130** | （无对白） | 下句要到 80.980「看，不藏着点…」 |

全资源检索（全 bundle 字节 + `music_*` 解压 SerializedBytes）：

| 检索句 | 结果 |
|--------|------|
| 同样是精彩绝伦呢 | **无**。官包是 **「同样精彩绝伦啊」**（繁中「同樣精彩絕倫啊」），只在 `ut_megalovania_map4` |
| 战斗 | 无连续两字「战斗」。同图 UT 菜单是 **「战 斗」/「戰 鬥」**，英文 `FIGHT`×12、日文 `たたかう`×12，仅 map4 |
| MISS | **无** 独立 `MISS`/`Miss` 气泡。`how_to_make_otoge_kyoku` 各 map 有 1 次小写 `miss`（音游教程台词，不是判定 MISS） |
| \*你没有吃掉传说英雄。 | 标点/空格不同。官包是 **`*  你没有吃掉传说英雄`**（星号后两个空格，句末无「。」）；英文 `* You didnt't eat the Legendary Hero.`（官方拼写 didnt't） |

上述特殊对白目前都只坐实在 **MEGALOVANIA Hidden（map4）** 的 `dialogEvents` 里（打字机前缀会重复计数）。`calamity_fortune` 各 map 也有 `dialogEvents` 块，简中句子尚未扫出与这四句相同的内容。`how_to_make_otoge_kyoku` 是教程旁白（「怎么做音游曲」等），不是 Boss 气泡。

MDM 仍空着：没有对话框通道。

### 「同样精彩绝伦啊。」配置（`ut_megalovania_map4` / ChineseS）

三条 `GameDialogArgs`（同一句：显示 0、显示 1、隐藏 2），不是打字机前缀。

| 字段 | 值 |
|------|----|
| `time` | **31.980s** 显示；**33.980s** 隐藏（持续 2.000s） |
| 约 combo | ~85（32.1s 空中摆锤） |
| `dialogType` | **1** Boss |
| `dialogIndex` | 0 |
| `index` | 0 |
| `dialogState` | 31.980：0 与 1 各一条；33.980：**2 Hide** |
| `text` | `同样精彩绝伦啊。`（8 字，带句号） |
| 繁中 | `同樣精彩絕倫啊。` |
| 英文 | `that's somethin' special.` |
| 日文 | `けっさくだ` |
| `textColor` | RGBA **(1, 0, 0, 1)** 红 |
| `bgColor` | RGBA **(0, 0, 0, 1)** 黑 |
| `speed` | 1.0 |
| `fontSize` | 30 |
| `alignment` | 1 |
| 前后句 | 28.980「不过你在这里吃苦头的表情...」→ 本句 → 34.980「通常情况下，当里谱的难度是 ¿ 的时候…」 |
| 叠加 UI | 本句时段 **没有** `战 斗` / `M I S S` |

MDM 不写。

---

2、zhuguang_zhuzhou（逐光筑昼）

`music_zhuguang_zhuzhou_assets_all.bundle` + `song_zhuguang_zhuzhou_assets_all_*.bundle`。

MusicInfo `uid=96-0`，曲名 **逐光筑昼**，作者 鸣潮先约电台 / YUE.STEVEN / Mars Atlas，网格 BPM **148**，`scene_01`，谱师 SugarSu，★ **3 / 5 / 8 / 10**（无 map5），delay **0**，封面 `zhuguang_zhuzhou_cover`，试听 `zhuguang_zhuzhou_demo`。

无 `dialogEvents`（SerializedBytes 无 `ChineseS`）。无 `0O–0W`，无东方弹 `30+`，无 `0I 0J 11 20`。

本张补进词典：可玩 **`05 06 08 09 16 17`**；事件 **`1D 2A 2B 2C 2D 2E 2F 2R 2S`**。Boss 远程用 **1C→1D→1E→1F**，没有 `1G/1H`。`16/17` 只在 map1–3；Hidden map4 用 Boss 齿轮 `18` 代替连打。

map4 开场滤镜：1.723 `2A` + 1.824 `2C` + 1.926 `2E`，约 4s 后 `2B/2D/2F` 关掉。`2E/2F` 另外在 24s、92s、121s 再成对。121.7s `2R` 黑白与 Boss 退场叠在一起，134.5s `2S` 结束。

仍空着：`0I 0J`、`11`、`20`、`1J 1K`、`1U–1Y`、`2Y 2Z`、画面 `27–29` `2I–2Q` `2T–2X`、`CM–CT`、问答、切鱼。

---

3、super_battleworn_insomniac（超·东方不眠夜）

`music_super_battleworn_insomniac_assets_all.bundle`。曲库 `uid=41-0`，fizzd，**90～180** 是展示串，**不要写通道 08**。网格用 StageInfo **≈65.753**，`scene_06`，谱师 Howard_Y，★ **4 / 7 / 9 / ?**，delay **0**。无对话框。无 `0I 0J 11 20`。

本张补进词典：事件 **`1J 1K 27 28 29 2J 2K`**。

map1–3 同一套（约 49.15s 起）：`25` 藏景 → `27/28` 交错上下滚（约 24 对）→ `29` 关滚动 → `2E` 暗角 → Boss `1A/1C` → `25`+`1D` → **`1J` 藏键 + `1L` 藏 Boss + `1E` 远程2 + `1M`+`1K` 同时复现** → 连切 `1O 1P 1Q 1R 1T`（夹 `2A/2B`）→ 三次 `1F` 叠切场景 → `1B` 退场。

Hidden map4：几乎全是事件（raw 918，hold 头 0）。`1J/1K` 各 180 次闪谱面，`1T` 151 次切东方景，另有 `1S`、`2C/2D`、`2G/2H`。闪光只有 **`2J` 高峰 + `2K` 结束**，**没有 `2I`**。StageInfo 上 map4 的 `difficulty` 写成 2，星级仍以 MusicInfo `difficulty4=?` 为准。

仍空着：`0I 0J`、`11`、`20`、`1U–1Y`、`2Y 2Z`、`2I`、`2L–2Q`、`2T–2X`、`CM–CT`、问答、切鱼、东方弹。

---

4、museyao（喵斯摇）

`music_museyao_assets_all.bundle`。曲库 `uid=84-0`，曲名 **喵斯摇 (feat. 春哥，渊神)**，DJ怪哥/DJ鹏哥，BPM **140**，`scene_12`，谱师 DJ翔哥。只有 **map2**，MusicInfo `difficulty2=?`（1/3 为 0）。delay **0**。无对话框。无 `0I 0J 11 20`。

本张补进词典：事件 **`2Z`**、**`40` / `41`**（type 50/51 问答，**不是**愚人 type1 彩蛋）。

问答 5 对：16.286–22.714、30.000–36.429、43.714–50.143、57.429–63.857，Boss 里再一对 78.000–84.429（`40` 与 `1F` 同帧）。

Boss 是 **boss_10**：`1A` → **先 `1E` 远程2** → `2Z` Auto 关 → `1F` → `1C` 远程1 → `1D` → 再一次 `2Z` → `1B`。全程 **没有 `2Y` Auto 开**。

仍空着：`0I 0J`、`11`、`20`、`1U–1Y`、**`2Y`**、`2I`、`2L–2Q`、`2T–2X`、`CM–CT`、切鱼、东方弹。

---

5、scarlet_keisatu_no_ghetto_patro_24_zi（スカーレット警察のゲットーパトロール 24 時）

`music_scarlet_keisatu_no_ghetto_patro_24_zi_assets_all.bundle`。uid `80-5`，七条レタスグループ，BPM **160**，`scene_08`，谱师 HXJ・ザ・パトローラー，★ **5 / 7 / 10** + map5 **N**（无 map4），delay **0**。无对话框。开头有一条 **ibms 空、type 0**，丢掉。

本张补进词典：可玩 **`20`（P点）**、东方弹 **`31–36 38–3C 3E 3G–3I`**；事件 **`2I`**、**`2U` / `2V`**（白集中线；**没有 `2T` 黑线**）。map1–3 也有完整 `2I→2J→2K`。

Touhou 里谱 map5：几乎全是弹幕（raw 1480，hold 头 0），`20`×129，`23` 音符条 393。未出现：`30` 光玉平移、`37` 星弹交错、`3D/3F` 鳞/刀交错。

仍空着：`0I 0J`、`11`、`30`、`37`、`3D`、`3F`、`1U–1Y`、`2Y`、`2L–2Q`、**`2T`**、`2W 2X`、`CM–CT`、切鱼。

---

6、worlds_end_loneliness（World's end loneliness）

`music_worlds_end_loneliness_assets_all.bundle` + `song_worlds_end_loneliness_assets_all_*.bundle`。uid `100-4`，曲名 **World's end loneliness**，作者 打打だいず，网格 BPM **180**，`scene_01`，★ **6 / 8 / 10 / 11**（无 map5），delay **0**，无 levelDesigner。无对话框、无 `0I 0J 11 20`、无东方弹。

Boss 是 **boss_01**，全程 **没有 `1B` 退场、没有 `1G/1H` 换阶段**，两次 `1A` 入场分两段：

| 谱 | 顺序 |
|----|------|
| map1 / map2 | `1A` → `1C` → `1D` → `1A` → `1E` → `1F` |
| map3 / map4 | 同骨架（map3/4 第二次入场处 `1A` 与 `1M` 同帧；`17` 连击后退场，无 `16`、无 `1B`） |

本张补进词典：事件 **`CN`（闪光颜色黑，type 41）**，是 `CM–CT` 闪光颜色族第一个实锤谱案例。notedata 逐条确认 `CM/CN/CO/CP/CQ/CR/CS/CT = 白/黑/红/绿/蓝/青/洋红/黄`（type 40–47，uid `001009`，pathway 0）。map3/4 在闪光 `2I` 之间插 `CN` 设黑：`2I`(59.275) → `CN`(59.311) → `2I`(59.384) → `2J` 高峰 → `2K` 结束。

map3/4 特有演出：57.6s 起 `25/26` 背景闪 6 对 → 59.3s 上述闪光 → 63.5s `25+2C` 藏景叠色散 → 64.7s `26+2D` 复现；106.8s 起 `25/26` 连闪 16 对 + `1L/1M` Boss 闪烁 15 对（同 megalovania_map4 的 Boss 隐身戏）。map1/2 无 `25/26`、无滤镜。

仍空着：`0I 0J`、`11`、`30`、`37`、`3D`、`3F`、`1U–1Y`、`2Y`、`2L–2Q`、`2T`、`2W 2X`、`CM` `CO–CT`（闪光颜色除黑外）、切鱼。

---

7、burn

`music_burn_assets_all.bundle` + `song_burn_assets_all_*.bundle`。uid `35-5`，曲名 **Burn**，作者 NceS，网格 BPM **148**，`scene_01`，★ **4 / 7 / 9**（无 map4/5），delay **0**，谱师 您（三张同）。无对话框、无 `0I 0J 20 30+`、无滤镜事件。

Boss 是 **boss_01**，三张谱的 Boss 段用法不同：

| 谱 | 顺序 |
|----|------|
| map1 | `1A` → `1C/1D` 远程1 抖动约 28 对 → `1E`×21 远程2 → `1F`；无 `1B`，末尾 `17` 连击后退场 |
| map2 / map3 | `1A` → `1C` → `1C/1D` 抖动 → `1E`×18 远程2 → `1F` → `1B` 退场；夹 `11` 近战1×8、`12` 近战2×20、`16` 连打×4 |

本张补进词典：可玩 **`11`（Boss 近战1，type 5）**，`boss_close_atk_1`，map2/3 各 8 次，填掉词典里最后一个 Boss 近战空位。`12`（近战2）、`16`（连打1）、`17`（连击后退场）均为已有号，此处再各添一条干净样本。

仍空着：`0I 0J`、`30`、`37`、`3D`、`3F`、`1U–1Y`、`2Y`、`2L–2Q`、`2T`、`2W 2X`、`CM` `CO–CT`（闪光颜色除黑外）、切鱼。

---

8、saishuu_kichiku_imouto_flandre_s（最終鬼畜妹フランドール・S）

Touhou 弹幕（`30–3I`）是 `scene_08` 专属，谱面在 Touhou 曲的 **map5 里谱**（scarlet / flandre / bad_apple）。本批把 §九 里 4 个「未出现」的弹补全：

| ibms | type | 官 des（notedata） | 编辑器 | 案例 |
|------|------|--------------------|--------|------|
| 30 | 2 | 光玉 平移（`080307_road_nor` / 空中平移） | 中弹 | flandre map5 ×11；bad_apple map5 ×38 |
| 37 | 2 | 星弹 交错（`080349_road_up_down`） | 小弹 换轨 | flandre map5 ×36 |
| 3D | 2 | Boss鳞弹 **交叉**（`080919_road_down`，非「交错」） | Boss弹1 换轨 | flandre map5 ×94 |
| 3F | 2 | Boss刀弹 **交叉**（`080943_road_down`，非「交错」） | Boss弹2 换轨 | flandre map5 ×100 |

注意 `3D/3F` 官 des 是 **「交叉」（road_down 单向下切）**，与 `33/37/3B` 的 **「交错」（road_up_down 换轨）** 不是一回事，词典 §一 已按此改正。

`music_saishuu_kichiku_imouto_flandre_s_assets_all.bundle`。uid `80-0`，作者 ビートまりお，BPM **200**，`scene_08`，★ **6 / 8 / 10 / 11** + map5 **L**，谱师 ForeverFXS（map1–3）/ 最終鬼畜エターナル・F（map4）/ 悪魔の妹（map5）。map5 raw 1907，`20`×58、`23`×298，**全套 `30–3I` 东方弹 20 种全齐**。

map5 额外补进词典：

- 事件 **`1V`（场景切换_8，type 9）**：29.567s，夹在开场 29.117–29.567 的连切序列 `1O→1P→1Q→1R→1T→1Q→1V` 里。
- 事件 **`CO`（闪光颜色红，type 42）**：4 次（36.317 / 45.917 / 84.317 / 103.517），每次紧跟 `2I→2J→2K` 闪光三连，配 `25/26` 藏景。

Boss 骨架（boss_01）三段：`1L` 藏 Boss → `1A` → `1C` → `1G`（切远程2）→ … → `1B`，中间 `1L/1M` 反复消失/复现，125.4s `1J`+`1L`+`25` 同帧收尾。

9、bad_apple（Bad Apple!! feat. Nomico）

`music_bad_apple_assets_all.bundle`。uid `42-0`，作者 Alstroemeria Records，BPM **138**，`scene_08`，★ **1 / 3 / 6 / 8** + map5 **E**。map5 raw 442，`30`×38 光玉平移，其余弹（`31 32 34 35 36 38 39 3A 3B 3C 3E`）均已有号。**没有** `37/3D/3F`，也无新事件。

仍空着：`0I 0J`、`1U` `1W–1Y`、`2Y`、`2L–2Q`、`2T`、`2W 2X`、`CM` `CP–CT`（闪光颜色除黑红外）、切鱼。

---

10、serve_sushi_serve（Serve, Sushi, Serve!）—— DAVE 联动「切鱼」专题

`music_serve_sushi_serve_assets_all.bundle`。uid `93-0`，曲名 **Serve, Sushi, Serve!**，作者 D'anthoni Wooten，`scene_13`（`gamescene_dave`），MusicInfo BPM **128**，delay **0**。只有 **map2**（difficulty2=`?`，difficulty1/3=0），raw 171。StageInfo `bpm=0`——切鱼小游戏不走标准网格，转 MDM 时 **不写 `#BPM` 标称**（只按 tick 落键）。

这是词典里最后一块空白 **切鱼专题 `M0–ML`** 的全部来源。全套 22 码，prefab 均为 `dave_empty`（uid `20xxxx`）：

| ibms | type | 官 des | 角色 | 通道 | 案例 |
|------|------|--------|------|------|------|
| M0 | 1 | 切鱼_轻击 | 布若 | 可玩 | ×54 |
| M1 | 3 | 切鱼_重击 | 布若 | 可玩（长按头） | ×14 |
| M2 | 1 | 鱼_切配完成_演示 | — | 可玩 | ×18 |
| M5 | 1 | 起手动作_演示 | — | 可玩 | ×22 |
| M6 | 1 | 切案板_轻击 | 班乔 | 可玩 | ×4 |
| M7 | 3 | 切案板_重击 | 班乔 | 可玩（长按头） | ×2 |
| MA–ML | 1 | 鱼_菜式 1–3 各 3 段 + 完成 | — | 可玩 | 各 ×2 |
| M3 | 52 | 切鱼_班乔演示 | 班乔 | 事件 15 | ×11 |
| M4 | 52 | 切鱼_布若实操 | 布若 | 事件 15 | ×11 |
| M8 | 53 | 切鱼评价 | — | 事件 15 | ×8 |
| M9 | 54 | 顾客评价 | — | 事件 15 | ×3 |

两个角色：**班乔**（Bancho，DAVE 主厨，走「演示」）与 **布若**（走「实操」）。`M3/M4` 成对切换演示/实操，`M8/M9` 分别是切鱼与顾客的评分节点。可玩键 `M0–ML` 原样抄 ibms；事件 `M3/M4/M8/M9` 也原样抄 ibms 进通道 15。

仍空着：`0I 0J`、`1U` `1W–1Y`、`2Y`、`2L–2Q`、`2T`、`2W 2X`、`CM` `CP–CT`（闪光颜色除黑红外）。

---

11、peropero_aniki_ranbu（P E R O P E R O兄☆貴☆乱☆舞）

`music_peropero_aniki_ranbu_assets_all.bundle`。uid `72-0`，曲名 **P E R O P E R O兄☆貴☆乱☆舞（feat.音游部, howsoon）**，作者 Mrguai，`scene_11`，MusicInfo BPM **175**（StageInfo 168），只有 **map2**（difficulty2=`?`，difficulty1/3=0），delay **0**，raw **177**。对照官机：可玩 **159**（`40`×65 / `41`×53 / `42`×41，uid `111xxx`，type 1，**全 pathway=0、speed=3**）+ 事件 18（`2E/2F` 各 8、`2T/2V` 各 1）。**没有** `1O–1W`。

本张补进词典：可玩 **`40`/`41`/`42`（愚人彩蛋 1/2/3，type 1）**——与 问答 `40/41`（type 50/51）共用号、按 type 拆；事件 **`2T`（速度线_黑，type 37）**。

| ibms | type | 官 des | 次数 |
|------|------|--------|------|
| 40 | 1 | 愚人彩蛋_地面_1 | 65 |
| 41 | 1 | 愚人彩蛋_地面_2 | 53 |
| 42 | 1 | 愚人彩蛋_地面_3 | 41 |
| 2E / 2F | 25 / 26 | 暗边 / 关闭（104–115s 频闪 8 对） | 各 8 |
| 2T | 37 | 速度线_黑 | 1（115.714s） |
| 2V | 39 | 速度线结束 | 1（125.714s） |

`2T` 黑线只出现一次、10s 后 `2V` 关掉；与 scarlet 的 `2U` 白线（**没有 2T**）互补，坐实黑/白两套集中线。`40/41/42` 愚人彩蛋是 `scene_11`（uid `111xxx`）专属，问答 `40/41` 是 `scene_01`（uid `001xxx`）。

仍空着：`0I 0J`、`1U` `1W–1Y`、`2Y`、`2L–2Q`、`2W 2X`、`CM` `CP–CT`（闪光颜色除黑红外）。

---

12、cubibibibism（Cubibibibism / きゅびびびびずむ）

`music_cubibibibism_assets_all.bundle`。uid `43-63`，作者 超绝最可爱天使酱，BPM **185**，`scene_02`，★ **6 / 8 / 10 / 11**，密度 map1–4 ≈ 4.35 / 8.43 / 9.72 / 14.68 个/s。

本张补进词典：可玩 **`0J`（摆锤 倒，×14）**；事件 **`2N/2O`（背景停滞/结束，各 71）**、**`2W/2X`（怀旧滤镜/结束，各 4）**、**`1Y`（场景切换_12，×2）**、**`CM`（白，×4）/ `CS`（洋红，×3）**。

- `2N/2O` 背景停滞：23–41s 频闪（map2–4 尤其密）。
- `2W/2X` 怀旧滤镜：43.5s 开 → 48.6s 关，四张各 1 对。
- `1Y` 场景切换_12：46.5s（map3/4）。
- `CM` 白 / `CS` 洋红：85.7s 洋红、103.6s 白（map4 多一次 119.8s 白）。

仍空着：`1U 1W 1X 2Y 2L 2M CP CQ CR CT`（官包无此谱案例）。

---

13、satsui_azashita（サ終あざした）

`music_satsui_azashita_assets_all.bundle`。uid `91-4`，作者 あばらや，BPM **152**，`scene_02`，★ **3 / 5 / 8 / 10**，密度 map1–4 ≈ 2.64 / 4.55 / 7.48 / 13.85 个/s。

本张补进词典：事件 **`2W/2X`（怀旧滤镜，各 12，四张各 3 对）**、**`1Y`（场景切换_12，map4 连闪 24 次）**、**`2N/2O`（背景停滞/结束，各 197）**。

- `2W/2X` 3 对：14.1→26.3、39.4→45.3、102.5→108.4（四张同）。
- map4 结尾 157.6–170.2s：`2N/2O` 连闪约 90 对，其中 163.9–166.9s 夹 24 次 `1Y` 切场景。

仍空着：`1U 1W 1X 2Y 2L 2M CP CQ CR CT`（官包无此谱案例）。

---

14、rush_hour（Rush-Hour）

`music_rush_hour_assets_all.bundle`。uid `61-3`，作者 litmus*，BPM **224**，`scene_01`，密度 map1–4 ≈ 5.07 / 7.09 / 8.69 / 10.34 个/s。★ 难度是愚人彩蛋（`IG / Jh / a2 / Eh`，谱师字段是 base64「Happy April Fool's Day, Bababa」）。

本张补进词典：可玩 **`0I`（突击者 倒）/ `0J`（摆锤 倒）**（各 55）；事件 **`2P/2Q`（像素化/结束）**（四张各 1 对：69.6s 开 → 79.3s 关）。

仍空着：`1U 1W 1X 2Y 2L 2M CP CQ CR CT`（以上官包无此谱案例，其余全部填满）。

---

15、change_the_game（Change the Game feat. 松永依織）

`music_change_the_game_assets_all.bundle`。uid 曲库名 `change_the_game`，REDALiCE & USAO，网格 BPM **200**，`scene_01`，谱师 Horizon.T（map4 字段 `Horizon.T in 2025.5.4`），★ **6 / 8 / 10 / 11**（无 map5），delay **0**。无对话框、无 `0I 0J 12 20`、无东方弹、无滤镜。

本张把 **同一时刻低难度近战 / 高难度 Boss 连打** 钉死，并确认 `0G` 叠弹幕是官写法：

| 谱 | 总键 | 11 | 16 @130.5s | 17 | 0G | 13/14/15 |
|----|------|----|------------|----|----|----------|
| map1 ★6 | 652 | 1（近战，130.5s） | 1 | 0 | 3 | 24/20/24 |
| map2 ★8 | 854 | 1（近战，130.5s） | 1 | 1 | 87 | 14/22/16 |
| map3 ★10 | 1249 | **0** | 1（连打，替 11） | 1 | 51 | 30/26/29 |
| map4 ★11 | 1582 | **0** | 1（连打，替 11） | 1 @70.8s | 60 | 106/30/45 |

map4 脚本（自制 BMS 时间戳一致）：`1L`(48.6) → `1A`(48.9) → `1M`(50.4) → `1C`(51.6) → `1D`(67.8) → `1A`(88.8) → `1E`(90.081) → `1F`(108.6) → `1C`(109.35) → `1D`(128.6) → `1B`(131.4)。`17` des=`boss_01_连击后退场_48`（点长，通道 14）；前一拍仍有空中 `0G` 下落 0.15s。`16` des=`boss_01_连击_48`。map4 有 **38** 处 `0G` 与 `13/14/15` 同拍。

解析：`type==8` **必须抄 ibms**（`0G`/`16`/`17`），禁止一律写成 `0G`。点长 `16/17`（length≈0.006）走 13/14，不要当成 53/54 长条。`chart.json` 只写飞近覆盖（本张可能只有一颗 `16` 的 `approachOverride`），**全谱在 mapN.bms**。

自制 map4 与官 Boss 键/脚本对齐；长条 `0F` 若对不上，是 BMS 头尾计数，与近战无关。要挥砍需把 130.5s 的 `16` 改成 `11`（对标 map1/2），不要用中线 `0G` 冒充近战。

仍空着：本张没有的 `12`、`0I 0J`、`20`、`1G/1H`、`1U–1Y`、`2Y 2Z`、滤镜、问答、切鱼、东方弹。

