# MusePSona

Muse Dash 自制谱解析与游玩端。本仓库只包含游玩端、谱面解析/转换工具及必要的共享解析核心；谱面编辑器将维护在独立仓库中。

## 目录

- `PlayClient/`：Unity 游玩端、运行时、命令行工具和自动化测试。
- `PlayClient/MuseDashChart.Core/`：游玩端所需的共享谱面解析核心，仓库可独立构建，不依赖编辑器源码。
- `scripts/`：官方谱面、MDM 与资源解析脚本。
- `规则解析/`：事件、轨道、角色动作与项目结构说明。

## 构建与测试

需要 .NET 8；Unity 工程使用 Unity 2022.3.62f3c1。

```powershell
dotnet test .\PlayClient\MusePSona.Play.sln -c Release
.\PlayClient\sync_unity_plugins.ps1
```

随后用 Unity Hub 打开 `PlayClient/Unity`。运行时素材、MDM、官机解包资源及游戏本体文件需由使用者在本地提供，不属于本仓库。

## 编辑器

编辑器不在本仓库。仓库内仍保留少量与编辑器导出格式兼容的路径探测及文档说明，但不会编译或上传编辑器程序。
