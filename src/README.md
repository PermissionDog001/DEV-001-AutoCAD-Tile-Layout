# src

产品源码目录。`TileLayout.Core` 是 M2 已完成的宿主无关核心；M3 正式项目为 `TileLayout.AutoCAD/TileLayout.AutoCAD.csproj`。`TileLayout.AutoCAD/TileLayout.AutoCAD.Probe.csproj` 仍是 M1 最小技术探针，不代表完整 V0.1 功能。

当前结构：

- `TileLayout.Core`：已建立，与 AutoCAD 无关的三维线模型、矩形验证和固定 600 mm 网格算法；
- `TileLayout.AutoCAD.csproj`：正式 AutoCAD 2021 适配，提供 `TILE600`，负责模型空间/毫米检查、四线选择、核心快照转换、事务、`TILE_LAYOUT_600` 图层和分格线写回；
- `TileLayout.AutoCAD.Probe.csproj`：保留 M1 `TILE600PROBE` 回归探针，与正式插件分开构建和加载。

全部项目使用 .NET Framework 4.8。核心不得引用 Autodesk 程序集；正式 AutoCAD 项目负责把宿主对象转换为核心模型并调用核心算法。两个 AutoCAD 项目固定使用 x64，Autodesk 引用均为 `Copy Local=false`。
