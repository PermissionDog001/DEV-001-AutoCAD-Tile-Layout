# M2 核心算法与自动测试

## 目的

M2 建立一个不启动 AutoCAD 即可编译和测试的确定性核心，为 M3 的宿主适配提供冻结契约。核心不选择对象、不访问图层、不开启 AutoCAD 事务，也不保存 DWG。

## 公共契约

- `Point3D`：与 Autodesk 类型无关的 WCS 三维点。
- `LineSegment3D`：只包含起点和终点的只读线段快照。
- `RectangleValidator.Validate(...)`：接收且只接收四条线，返回 `RectangleValidationResult`。
- `AxisAlignedRectangle`：通过验证后的西、东、南、北和统一高程。
- `TileGridCalculator.Calculate(...)`：接收矩形并返回 `TileLayoutResult`。
- `TileLayoutResult`：包含完整列数、完整行数、东侧余量、北侧余量和只读分格线列表。

M3 应先把 AutoCAD `LINE` 的端点复制为核心模型，完成核心验证和计算后，再把结果线写回 AutoCAD。核心程序集不得反向引用 `AcCoreMgd.dll`、`AcDbMgd.dll` 或 `AcMgd.dll`。

## 公差规则

所有坐标比较统一使用 `GeometryTolerance.Coordinate = 1e-6 mm`：

- 差值小于等于公差时视为相等；
- 差值超过公差时视为不同；
- 宽或高必须严格大于公差；
- 尺寸位于 600 mm 整数倍的公差范围内时，余量归零；
- 分格线与东侧或北侧边界相距不超过公差时，不生成该内部线。

## 矩形验证

验证顺序固定为：线数量、有限坐标、统一高程、非退化线、WCS 轴对齐、正宽高、完整四边、无重复或缺失边。失败结果包含稳定错误枚举和可读消息，不返回部分矩形。

线的输入顺序和起终点方向不影响结果。V0.1 只支持单一高程上的 WCS X/Y 轴对齐矩形。

## 600 mm 网格

- 固定砖宽和砖高均为 600 mm，灰缝为 0 mm。
- 起点为房间西南角，不居中、不平移、不优化窄砖。
- 先按 X 递增生成竖向内部分格线，再按 Y 递增生成横向内部分格线，输出顺序确定。
- 竖线坐标为 `west + 600 × n` 且严格位于东边界内。
- 横线坐标为 `south + 600 × n` 且严格位于北边界内。
- 房间任一方向小于 600 mm 时仍返回有效结果，该方向完整砖数为 0，且不生成该方向的内部分格线。

核心只返回内部分格线，不重复生成作为输入存在的房间边界。

## 测试依赖

测试项目精确锁定以下微软维护的 NuGet 包：

- `Microsoft.NET.Test.Sdk 18.8.1`
- `MSTest.TestAdapter 4.3.2`
- `MSTest.TestFramework 4.3.2`

这些依赖使用 MIT 许可，只用于测试，不进入核心或插件发布物。包源固定为 NuGet 官方 HTTPS 源，精确传递依赖由 `packages.lock.json` 锁定，缓存位于被 Git 忽略的 `build\packages`；本机首次恢复缓存约 110 MiB。

## 自动覆盖

自动测试覆盖：整除尺寸、仅东侧余量、仅北侧余量、双向余量、宽或高小于 600 mm、500 × 500 mm、600 × 600 mm、输入线数量错误、四线重复边、非有限坐标、退化线、非正宽高、非闭合、非轴对齐、不同高程、坐标公差边界、600 mm 整数倍余量公差边界、偏移原点、高程保留和分格线顺序。

## 2026-07-19 验证记录

- NuGet 锁定模式依赖恢复：通过。
- `TileLayout.Core` 与 `TileLayout.Core.Tests` Debug/Release 编译：通过。
- 核心自动测试：Debug/Release 均为 24 项全部通过。
- M1 探针标准 Debug/Release 输出目录编译：通过，未复制 Autodesk Managed DLL。
- 完整解决方案标准 Debug/Release 输出目录重建：通过。
- 首次重建时正在运行的 AutoCAD 曾锁定 `build\probe\Debug\TileLayout.AutoCAD.Probe.dll`；用户正常关闭 AutoCAD 后复验通过，未强制结束宿主进程。

## M3 后续状态

2026-07-19，正式 `TILE600` 命令、模型空间和毫米单位检查、四条 `LINE` 选择、核心模型转换、`TILE_LAYOUT_600` 图层、事务写回和错误提示已经在 M3 代码中建立，M2 契约未被复制或改写。自动构建与测试已通过，但一次 `UNDO`、正式 DLL 加载和完整宿主行为仍待用户实机验证。详见 `docs/autocad-integration-m3.md`；M1 探针仍不代表正式插件。
