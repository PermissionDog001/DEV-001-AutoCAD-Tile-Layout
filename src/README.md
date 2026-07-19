# src

产品源码目录。当前 `TileLayout.AutoCAD/TileLayout.AutoCAD.Probe.csproj` 是 M1 最小技术探针，不代表完整 V0.1 功能。

技术探针通过后计划建立：

- `TileLayout.Core`：与 AutoCAD 无关的核心几何和网格算法；
- `TileLayout.AutoCAD`：AutoCAD 2021 命令、选择、事务、图层和写回适配。

探针固定使用 .NET Framework 4.8 和 x64，仅验证 `NETLOAD`、四条 `LINE`、矩形尺寸、测试图层写入和一次撤销。探针通过前不建设完整排版算法、复杂 UI 或安装器。
