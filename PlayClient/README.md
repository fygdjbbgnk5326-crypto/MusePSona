# MusePSona 自制谱游玩端

独立客户端：读自制谱 `.mdm`，两轨节奏游玩。**不是** Muse Dash 工程。编辑器导出的 MDM **已验证可 Play**（F8 Spine 外形含大型 0A/0B；默认 autoplay）。

| 层 | 目录 | 作用 |
|----|------|------|
| 运行时 | `MusePSona.Play.Runtime` | 解 MDM、飞近、判定、切场景 |
| 自检 | `MusePSona.Play.Cli` | `mdplay 谱.mdm [map] [--auto]` |
| Unity | `Unity/` | 显示 + 官机键位预设；`Plugins/Spine38` 直读 F8（**3.8.99**，勿用 4.x） |

## 现在就能跑（不需要 Unity）

```text
dotnet test MusePSona.Play.sln
dotnet run --project MusePSona.Play.Cli -- "..\导出\<id>\<id>.mdm" 1 --auto
```

## Unity 2022.3.62f3c1

1. Hub **打开** `PlayClient\Unity`（编辑器已在 `E:\Program Files\unity\2022.3.62f3c1\Editor`）。
2. 改运行时后跑 `sync_unity_plugins.ps1`，回到 Unity 等编译。`.meta` 必须是合法 YAML。若缺 `System.Runtime.CompilerServices.Unsafe`，Play 会 FileNotFound。
3. Player Settings → **Scripting Backend = Mono**，Api Compatibility = **.NET Standard 2.1**。
4. 打开场景 `Assets/Scenes/Play.unity`（`PlayDirector`，**默认 autoplay**；`F1` 随时切换自动/手动，`F2` 以手动模式重开，`F3` 以自动模式重开；手动默认使用 D/F/J/K，右上 `键位` 可切官机预设）。Game 视图必须选 **Display 1**（黑屏写 Display 2 / No cameras rendering 时点 Game 窗口顶部 Display 下拉，或菜单 `MusePSona/Game 视图切到 Display 1`）。底部 map 按钮或菜单 `MusePSona/难度` 可换谱，不必重载场景。

### 音频偏移测试

- `F12` 打开覆盖式校准副页：当前谱面暂停，独立循环测试音与移动标记用于听感对齐，保存后原位置继续播放。
- `F9` / `F10`：每次减少 / 增加 `0.001 s`；`Shift+F9/F10` 快调 `0.010 s`。`F11` 归零。
- 范围为 `-1.000～+1.000 s`，固定显示三位小数，数值自动保存；正值会让谱面相对音频稍后到达判定线。
- 该值只校准设备音频延迟，不会改写谱面自身的 `delay`。

### 战斗 HUD

- 左上角按官机布局显示实时分数；保持全 Perfect 时显示 AP 图标，出现 Great 或 Miss 后立即隐藏。
- 右上角 `EARLY/LATE 小P` 按钮控制高级判定反馈并自动保存。开启时 ±25ms 的亮 P 不显示偏差，25–50ms 的小 P 与 Great 显示方向和毫秒；被动拾取物不显示偏差。
- `键位` 按钮循环官机 `InputStandlone` 预设并自动保存：Default（空 D/F、地 J/K）、Mode B（空 D/K、地 F/J）、Mode C（空 Z/←/↑、地 X/→/↓）、Mode D（空 A/W/K、地 D/S/J）；Default/B/C 还提供上下轨反转方案。按下、松开、Hold 与换指共用同一映射。
- 曲终全连提示会按联动主题切换官机字图：`scene_09` 自动使用 DJMAX Max Combo 徽章；谱面 `search_tags` 含 `touhou`/`东方`、`arknights`/`明日方舟` 或 `djmax` 时分别启用东方、明日方舟或 DJMAX 样式。联动资源缺失时回退普通 FULL COMBO，不使用系统字体。

5. 谱面：`Assets/StreamingAssets/Charts/song.mdm`。**进入 Play 时默认自动拷最新 MDM**（`导出\` 与 `%LOCALAPPDATA%\MuseDashChart\export`）。可在菜单 **MusePSona → 进入 Play 时自动导入最新 MDM** 关掉。仍可用 **导入 Unity** / **从导出复制最新 MDM** / **从导出选择 MDM**。菜单脚本需要 `using System;`。
6. Play：飞近轨迹 + F8 Spine 外形（0A/0B 大型完整）。空中/地面判定圈。长按头尾连击；幽灵判定前 0.15 s 渐隐；连打钉在判定线脉动；切场景 `1O–1Y` 换分层背景（不拆飞近；`25/26` 显隐、`2N/2O` 冻结、`27/28/29` 滚屏）。画面 `2E/2F` 暗角、`2T/2U/2V` 集中线、`2I–2K` 闪光、`1J/1K` 藏键。手动模式下空中 **D/F**、地面 **J/K**；即使没有命中物件也会播放对应动作。同轨双键同帧不会用第二次空打覆盖命中动作；Hold 必须持续按住，D↔F / J↔K 换指有 80ms 宽限。Music `10/23` 与红心按角色所在轨接触收集，`24` Hold 星仍需独立按键；齿轮抵达判定圈当帧锁定结果，超过一逻辑帧未处理则强制 PASS、禁止迟到受击，手动 PASS 不补副轨命中特效，同轨碰撞触发原生 `destroy` 后继续向左离场，Hold 内齿轮不打断主动作。官机攻击判定为 Perfect ±50ms、Great ±80ms，超过 Late 80ms 立即 Miss；Music/红心到点后有 160ms 接触窗口。手动命中的 `EARLY / LATE + 毫秒` 会附在 Perfect/Great 判定图标下并共同淡出，Miss 与 Just 暂不显示；结算以整张谱的判定单元为分母，Perfect=100%、Great=50%、Miss=0%，满分固定 100000。Hold 头尾各算一次，连打按 80ms 目标击数计算，齿轮 `JUMPOVER/PASS` 不进入准确率。编辑器导出后回到 Unity **停掉再按 Play** 即会自动导入。

改了 Runtime / Core 之后再跑一次 `sync_unity_plugins.ps1`。

## 骨骼（本机 F8）

- 本机 `StreamingAssets/MdLooks/spine`（`python scripts/extract_md_spine.py --all`），**3.8.99 .skel**，物件 **scale 0.005**。clip 名走 `AnimBinding`（0A=x13，0B=x14）。脸走本包 `head_hexie` 网格；图集按条目前缀绑，不要串绑共用 atlas 的 `head`。不要 Spine 4.x。
- `.skel` 不进 MDM。切场景仍用通道 15 `1O–1Y`。约定全文：`规则解析/mdm物件转化词典.md` §1.4 / §2.2–2.3。

## 不要做的

- 把官机 Addressables 打进这个工程当发行包
- 把 `.skel` 打进 `.mdm`
- 用 Spine 4.x 打开 3.8.99 skel
- 用 IL2CPP 当第一期（本机缺模块时会 Failed to locate Unity.exe）
