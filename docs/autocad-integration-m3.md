# M3 AutoCAD 2021 正式集成与实机验收

## 当前状态

M3 已于 2026-07-20 完成。正式 AutoCAD 适配代码、离线适配测试和 Debug/Release 构建检查均已通过；用户已在 AutoCAD 2021 中确认正式 DLL 加载、`TILE600`、三个尺寸样例、错误输入回滚、模型空间/单位限制、原墙线保护、一次撤销和不自动保存全部符合预期。M3 宿主集成可标记为实机通过。

正式插件与 M1 探针严格分开：

- 正式插件：`TileLayout.AutoCAD.dll`，命令 `TILE600`，图层 `TILE_LAYOUT_600`；
- M1 探针：`TileLayout.AutoCAD.Probe.dll`，命令 `TILE600PROBE`，图层 `TILE_LAYOUT_PROBE`。

实机验收时必须 `NETLOAD` 正式插件，不要把探针结果当成 M3 结果。

## 实现边界

正式命令只处理当前图纸模型空间、毫米单位和用户选择的恰好四条 `LINE`。适配流程为：

1. 检查模型空间和 `INSUNITS=Millimeters`；
2. 只读打开四条模型空间 `LINE`；
3. 把端点复制为核心 `Point3D` 和 `LineSegment3D` 快照；
4. 调用 `RectangleValidator`，失败时在写入前停止；
5. 调用 `TileGridCalculator`，取得统一高程上的内部分格线；
6. 在单个 AutoCAD 命令和事务内创建或复用 `TILE_LAYOUT_600`，写入全部分格线；
7. 事务成功后报告房间宽高、完整列/行数、东侧/北侧余量和分格线数。

命令不会写开原四条墙线，不调用保存命令，也不修改现有同名图层的属性。验证失败发生在创建图层之前；创建或写入异常时事务不提交，不应留下部分新增对象。成功时图层和全部分格线属于同一次命令操作，应能用一次 `U` 或 `UNDO` 撤销。

## 构建与自动验证

在项目根目录的“Developer PowerShell for VS 2022”中执行：

```powershell
msbuild .\TileLayout.sln /t:Restore /p:RestoreConfigFile="$PWD\NuGet.Config" /p:RestoreLockedMode=true
msbuild .\TileLayout.sln /t:Rebuild /p:Configuration=Debug /p:Platform=x64
vstest.console.exe .\build\tests\Debug\TileLayout.Core.Tests.dll /Platform:x64
msbuild .\TileLayout.sln /t:Rebuild /p:Configuration=Release /p:Platform=x64
vstest.console.exe .\build\tests\Release\TileLayout.Core.Tests.dll /Platform:x64
```

正式插件输出：

```text
build\plugin\Debug\TileLayout.AutoCAD.dll
build\plugin\Debug\TileLayout.Core.dll
build\plugin\Release\TileLayout.AutoCAD.dll
build\plugin\Release\TileLayout.Core.dll
```

AutoCAD 通过 `NETLOAD` 加载 DLL 后可能一直锁定该 DLL，直到用户正常关闭 AutoCAD。需要在宿主运行期间验证新构建时，可把输出改到项目 `build` 下的安全备用目录，例如：

```powershell
msbuild .\src\TileLayout.AutoCAD\TileLayout.AutoCAD.csproj /t:Rebuild /p:Configuration=Debug /p:Platform=x64 /p:OutputPath="$PWD\build\plugin-verify\Debug\"
```

不要强制结束 AutoCAD；应加载备用目录中的新 DLL，或保存测试图后正常关闭宿主再重建标准目录。

2026-07-19 自动验证结果：

- NuGet 锁定模式恢复：通过；
- Debug/Release 完整解决方案重建：通过；
- 原 M2 24 项核心测试：Debug/Release 全部继续通过；
- 新增 3 项宿主无关适配文本测试：Debug/Release 全部通过；
- 每个配置合计 27 项测试，均为 27/27 通过；
- 正式插件 Debug/Release 输出均未复制 `AcCoreMgd.dll`、`AcDbMgd.dll` 或 `AcMgd.dll`；
- `TileLayout.Core` 源码和项目文件没有 Autodesk 引用；
- 本次自动验证期间 AutoCAD 未运行，没有发生 DLL 锁定，也未启动或关闭宿主；
- 未读取、修改或保存任何 DWG/DOCX。

## 实机验收准备

使用新建空白图或明确的脱敏测试副本，不要在正式项目 DWG 上进行首次验证。建议先准备以下互相独立的测试矩形，均使用 `RECTANG` 后 `EXPLODE` 得到四条 `LINE`：

| 样例 | 预期完整列/行 | 东/北余量 | 预期内部分格线 |
|---|---:|---:|---:|
| 3600 × 3000 | 6 / 5 | 0 / 0 mm | 9 条 |
| 4250 × 3100 | 7 / 5 | 50 / 100 mm | 12 条 |
| 500 × 500 | 0 / 0 | 500 / 500 mm | 0 条 |

开始前确认：

1. AutoCAD 2021 当前位于模型空间；
2. `INSUNITS` 设置为毫米；
3. 测试边界是四条独立 `LINE`，不是未分解的多段线；
4. 测试图已经另存为副本，或者准备在结束时放弃保存；
5. 记录四条墙线数量、图层和关键属性，便于前后对比。

## 正式插件加载

1. 在 AutoCAD 2021 命令行执行 `NETLOAD`；
2. 选择 `build\plugin\Debug\TileLayout.AutoCAD.dll`；
3. 确认命令行显示“TileLayout 0.1.0 正式插件已加载”；
4. 如果 AutoCAD 报安全加载或可信路径问题，只对这个项目的明确输出目录进行受控配置，不要降低全局安全设置；
5. 输入 `TILE600`，确认命令存在并开始选线。

## 功能验收步骤

### 1. 3600 × 3000 整除样例

1. 执行 `TILE600`，选择该矩形的四条 `LINE`，按 Enter；
2. 确认命令行报告宽 3600、高 3000、完整列 6、完整行 5、东/北余量均为 0；
3. 确认出现或复用 `TILE_LAYOUT_600`；
4. 确认生成 5 条竖向和 4 条横向内部分格线，共 9 条；
5. 确认分格线与墙线处于同一高程。

### 2. 4250 × 3100 双余量样例

1. 执行 `TILE600` 并选择四条边界线；
2. 确认报告完整列 7、完整行 5、东侧余量 50 mm、北侧余量 100 mm；
3. 确认从西南角起按 600 mm 递增，东、北侧直接截断；
4. 确认共生成 12 条内部分格线，没有居中、平移或窄砖优化。

### 3. 500 × 500 小房间

1. 执行 `TILE600` 并选择四条边界线；
2. 确认命令成功且不崩溃；
3. 确认报告完整列/行均为 0，东/北余量均为 500 mm；
4. 确认不生成内部分格线。若专用图层原本不存在，允许本次成功操作只创建空图层。

### 4. 无效输入与回滚

分别执行独立测试：

- 少于或多于四条 `LINE`：应提示必须且只能选择四条；
- 四条线不闭合：应提示矩形验证失败；
- 四条线中有斜线：应提示必须与 WCS X/Y 轴平行；
- 在布局/图纸空间执行：应要求切换到模型空间；
- `INSUNITS` 非毫米：应停止执行并说明单位问题。

每种失败后检查 `TILE_LAYOUT_600` 中没有新增线；如果该图层原本不存在，验证失败后也不应留下该图层。原四条输入线必须仍然存在且属性未变。

### 5. 原图保护、一次撤销和不保存

1. 在一个成功样例执行前记录原墙线数量、对象属性和专用图层是否存在；
2. 执行一次 `TILE600`；
3. 只输入一次 `U`，或执行一次 `UNDO` 撤销上一操作；
4. 确认本次新增的全部分格线同时消失；若专用图层是本次新建，也应恢复到命令前状态；
5. 确认四条墙线仍在原图层，几何和属性未改变；
6. 确认插件没有执行 `QSAVE`/`SAVE`。测试结束时关闭副本并选择“不保存”，或只由用户手动保存明确的测试副本。

## 实机验收记录

| 检查项 | 当前结果 | 用户实机备注 |
|---|---|---|
| 正式 DLL 可由 `NETLOAD` 加载 | 已通过 | 2026-07-20，用户已运行正式命令完成三组样例 |
| `TILE600` 命令可执行 | 已通过 | 2026-07-20，用户实机确认 |
| 3600 × 3000 整除样例 | 已通过 | 2026-07-20，分格线生成正确 |
| 4250 × 3100 双余量样例 | 已通过 | 2026-07-20，分格线生成正确 |
| 500 × 500 小房间 | 已通过 | 2026-07-20，报告 0 列/0 行、双向余量 500 mm、0 条内部分格线 |
| 数量错误、非闭合、非轴对齐均拒绝且无残留 | 已通过 | 2026-07-20，用户确认均给出正确错误提示且无错误生成 |
| 模型空间与毫米单位限制 | 已通过 | 2026-07-20，用户实机确认 |
| 原四条墙线未修改 | 已通过 | 2026-07-20，用户实机确认 |
| 一次 `U`/`UNDO` 完整撤销 | 已通过 | 2026-07-20，用户确认一次撤销完整 |
| 插件未保存图纸 | 已通过 | 2026-07-20，用户实机确认 |

结论：M3 全部自动检查和宿主实机检查通过。该结论不等同于 M4 脱敏 DWG 扩展验收或 M5 发布包验收。
