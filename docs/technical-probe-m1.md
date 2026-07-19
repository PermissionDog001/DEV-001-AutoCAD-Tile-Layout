# M1 AutoCAD 2021 技术探针

## 目的

只验证 AutoCAD 2021 Managed .NET 技术闭环，不验证完整瓷砖排版：

1. `NETLOAD` 能加载本项目程序集；
2. `TILE600PROBE` 能选择四条模型空间 `LINE`；
3. 能读取端点并验证 WCS 轴对齐闭合矩形；
4. 能报告房间宽、高和西南角；
5. 能在 `TILE_LAYOUT_PROBE` 图层创建一条测试线；
6. 一次 `U` 或 `UNDO` 能撤销本次新增对象；
7. 原四条墙线和当前 DWG 文件不被插件修改或自动保存。

## 已确认环境

- Windows 10 x64；
- AutoCAD 2021 简体中文，版本 24.0.47.0；
- AutoCAD 2021 命令行可以打开 `NETLOAD`；
- Visual Studio Community 2022 17.14；
- MSBuild 17.14；
- .NET Framework 4.8 SDK、Targeting Pack 和开发工具；
- AutoCAD 2021 Managed DLL 版本 24.0.47.0。

## 构建

在“Developer PowerShell for VS 2022”中，从项目根目录执行：

```powershell
msbuild .\src\TileLayout.AutoCAD\TileLayout.AutoCAD.Probe.csproj /t:Rebuild /p:Configuration=Debug /p:Platform=x64
```

输出文件：

```text
build\probe\Debug\TileLayout.AutoCAD.Probe.dll
```

探针项目没有 NuGet 或第三方依赖。AutoCAD 引用设置为 `Copy Local = false`，输出目录不得出现 `AcCoreMgd.dll`、`AcDbMgd.dll` 或 `AcMgd.dll`。

AutoCAD 通过 `NETLOAD` 加载 DLL 后，会一直锁定该文件直到关闭 AutoCAD。此时重新构建到同一输出目录会出现 `MSB3021` 或 `MSB3027`，不代表源码或编译环境故障。需要重新构建时，应正常关闭 AutoCAD，或临时使用另一个构建输出目录；不得强制结束 AutoCAD 进程，以免丢失未保存图纸。

## 准备一次性测试图

不要在正式项目 DWG 中做首次探针。新建空白图纸或使用脱敏副本，并确认：

1. 当前处于模型空间；
2. `INSUNITS` 为毫米；
3. 用 `RECTANG` 从 `0,0` 绘制到 `3600,3000`；
4. 用 `EXPLODE` 分解矩形，使其成为四条独立 `LINE`；
5. 首次测试前可以另存测试副本，但插件本身不会保存图纸。

## 实机操作

1. 在 AutoCAD 命令行输入 `NETLOAD`；
2. 选择 `build\probe\Debug\TileLayout.AutoCAD.Probe.dll`；
3. 命令行应显示“TileLayout 0.0.1 技术探针已加载”；
4. 输入 `TILE600PROBE`；
5. 依次选择四条 `LINE`，按 Enter；
6. 命令行应报告每条线端点以及：宽 `3600 mm`、高 `3000 mm`、西南角 `(0, 0, 0)`；
7. 图层列表应出现 `TILE_LAYOUT_PROBE`，房间内应有一条新增测试线；
8. 输入 `U`，只撤销一次；
9. 确认测试线消失，原四条墙线仍存在且未改变；
10. 不保存测试图，或只保存明确的测试副本。

## 失败行为

以下情况必须停止且不留下部分新增对象：

- 不在模型空间；
- `INSUNITS` 不是毫米；
- 没有选择恰好四条 `LINE`；
- 四条线不共面；
- 四条线不与 WCS X/Y 轴平行；
- 四条线没有形成完整闭合矩形。

## 验收记录

| 检查项 | 结果 | 备注 |
|---|---|---|
| `NETLOAD` 可打开 | 已通过 | 2026-07-19 用户实机确认 |
| 探针程序集编译 | 已通过 | Debug/x64，MSBuild 17.14 |
| 探针程序集加载 | 已通过 | 2026-07-19 用户实机确认 |
| 四条 `LINE` 可选择 | 已通过 | 2026-07-19 用户实机确认 |
| 端点、宽高、西南角正确 | 已通过 | 3600 × 3000 mm 样例结果正确 |
| 测试图层和测试线正确 | 已通过 | `TILE_LAYOUT_PROBE` 写入成功 |
| 一次撤销完整 | 已通过 | 一次 `U` 可撤销新增内容 |
| 原墙线未修改、插件未保存 DWG | 已通过 | 用户按一次性测试图流程确认 |

## M1 结论

2026-07-19，AutoCAD 2021 Managed .NET 主路线的第一技术关口通过。项目可以进入 M2，建立不引用 AutoCAD 程序集的 `TileLayout.Core`，实现轴对齐矩形验证、集中公差定义、600 mm 网格计算与自动单元测试。M1 通过不等同于 V0.1 完整功能完成，探针命令 `TILE600PROBE` 和测试图层 `TILE_LAYOUT_PROBE` 仍只用于技术验证。
