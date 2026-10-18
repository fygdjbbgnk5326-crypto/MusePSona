# MusePSona 自制谱游玩端

独立客户端：读自制谱 `.mdm`，两轨节奏游玩。**不是** Muse Dash 工程。编辑器导出的 MDM **已验证可 Play**（F8 Spine 外形含大型 0A/0B；默认 autoplay）。

| 层 | 目录 | 作用 |
|----|------|------|
| 运行时 | `MusePSona.Play.Runtime` | 解 MDM、飞近、判定、切场景 |
| 自检 | `MusePSona.Play.Cli` | `mdplay 谱.mdm [map] [--auto]` |
| Unity | `Unity/` | 显示 + DFJK；`Plugins/Spine38` 直读 F8（**3.8.99**，勿用 4.x） |

网页 `play/mdm_player` 仍是无判定预览。本端才接按键。

## 现在就能跑（不需要 Unity）

```text
dotnet test MusePSona.Play.sln
dotnet run --project MusePSona.Play.Cli -- "..\导出\<id>\<id>.mdm" 1 --auto
```

## Unity 2022.3.62f3c1

1. Hub **打开** `PlayClient\Unity`（编辑器已在 `E:\Program Files\unity\2022.3.62f3c1\Editor`）。
2. 改运行时后跑 `sync_unity_plugins.ps1`，回到 Unity 等编译。`.meta` 必须是合法 YAML。若缺 `System.Runtime.CompilerServices.Unsafe`，Play 会 FileNotFound。
3. Player Settings → **Scripting Backend = Mono**，Api Compatibility = **.NET Standard 2.1**。
4. 打开场景 `Assets/Scenes/Play.unity`（`PlayDirector`，**默认 autoplay**；`F1` 随时切换自动/手动，`F2` 以手动模式重开，`F3` 以自动模式重开；手动使用 D/F/J/K）。Game 视图必须选 **Display 1**（黑屏写 Display 2 / No cameras rendering 时点 Game 窗口顶部 Display 下拉，或菜单 `MusePSona/Game 视图切到 Display 1`）。底部 map 按钮或菜单 `MusePSona/难度` 可换谱，不必重载场景。
5. 谱面：`Assets/StreamingAssets/Charts/song.mdm`。**进入 Play 时默认自动拷最新 MDM**（`导出\` 与 `%LOCALAPPDATA%\MuseDashChart\export`）。可在菜单 **MusePSona → 进入 Play 时自动导入最新 MDM** 关掉。仍可用 **导入 Unity** / **从导出复制最新 MDM** / **从导出选择 MDM**。菜单脚本需要 `using System;`。
6. Play：飞近轨迹 + F8 Spine 外形（0A/0B 大型完整）。空中/地面判定圈。长按头尾连击；幽灵判定前 0.15 s 渐隐；连打钉在判定线脉动；切场景 `1O–1Y` 换分层背景（不拆飞近；`25/26` 显隐、`2N/2O` 冻结、`27/28/29` 滚屏）。画面 `2E/2F` 暗角、`2T/2U/2V` 集中线、`2I–2K` 闪光、`1J/1K` 藏键。手动模式下空中 **D/F**、地面 **J/K**；即使没有命中物件也会播放对应动作。同轨双键同帧不会用第二次空打覆盖命中动作；Hold 必须持续按住，D↔F / J↔K 换指有 80ms 宽限。Music `10/23` 与红心按角色所在轨接触收集，`24` Hold 星仍需独立按键；齿轮抵达判定圈当帧锁定结果，超过一逻辑帧未处理则强制 PASS、禁止迟到受击，手动 PASS 不补副轨命中特效，同轨碰撞触发原生 `destroy` 后继续向左离场，Hold 内齿轮不打断主动作。判定窗仍是练习值 ±50/80/120 ms。编辑器导出后回到 Unity **停掉再按 Play** 即会自动导入。

改了 Runtime / Core 之后再跑一次 `sync_unity_plugins.ps1`。

## 骨骼（本机 F8）

- 本机 `StreamingAssets/MdLooks/spine`（`python scripts/extract_md_spine.py --all`），**3.8.99 .skel**，物件 **scale 0.005**。clip 名走 `AnimBinding`（0A=x13，0B=x14）。脸走本包 `head_hexie` 网格；图集按条目前缀绑，不要串绑共用 atlas 的 `head`。不要 Spine 4.x。
- `.skel` 不进 MDM。切场景仍用通道 15 `1O–1Y`。约定全文：`规则解析/mdm物件转化词典.md` §1.4 / §2.2–2.3。

## 不要做的

- 把官机 Addressables 打进这个工程当发行包
- 把 `.skel` 打进 `.mdm`
- 用 Spine 4.x 打开 3.8.99 skel
- 用 IL2CPP 当第一期（本机缺模块时会 Failed to locate Unity.exe）
