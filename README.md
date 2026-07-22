# AutoCAD瓷砖自动排版插件

DEV-001 是一个面向 Autodesk AutoCAD 2021 的 C# Managed .NET 插件项目。

## 当前状态

M1 至 M5 均已完成。M4-01 至 M4-16 全部通过；M5 Release 重建和 27/27 自动测试通过，V0.1.0 最小包、说明和 SHA-256 已核验，AutoCAD 2021 发布包冒烟也已确认正式 DLL 加载成功、生成 23 条并可一次撤销。包中只有两个运行 DLL 和使用说明，未包含 Autodesk DLL、DWG、PDB、缓存或测试组件。未发现需要修改产品源码的可复现缺陷；V0.1.0 已发布到私有 GitHub Release，尚未公开发布。

V0.2 范围已于 2026-07-20 冻结：新增 `TILELAYOUT`，支持用户按次输入方砖或矩形砖的宽、高；继续使用四条 WCS 轴对齐 `LINE`、西南角起铺、灰缝 0 和直接截断规则。M6 参数化核心、M7 AutoCAD 适配、M8 自动质量门和 M9 AutoCAD 2021 脱敏 DWG 实机验收均已完成；Debug/Release 自动测试各 51/51 通过，方砖/矩形砖、宽高方向、取消/错误、图层属性、重复追加、超限拒绝、一次撤销、原墙线保护和不自动保存均已形成证据，未发现可复现产品缺陷。M10 发布检查尚未执行，版本号和发布物未变。

在 M9 工作树上启动的“工程起铺控制”阶段已完成：`TILELAYOUT` 在砖宽、砖高之后允许选择西南、东南、西北或东北起铺角，默认西南；切砖余量确定地落在所选角的对边。`TILE600` 继续固定西南角且无新增提示。Debug 56/56 自动测试、正式插件隔离编译和 AutoCAD 2021 脱敏副本最小实机验证均已通过。该阶段未启动 M10，未变更版本或发布物。

在 Git 基线 `6aa51d9` 上启动的“正交简单房间边界与裁切”已完成需求/接口冻结、核心算法、独立 `TILEORTHO` AutoCAD 适配、自动质量门、聚焦代码审查和 AutoCAD 2021 最小实机。新命令接受 4 条及以上同高程、WCS X/Y 轴对齐 `LINE` 组成的单一简单闭环，支持共线分段矩形和 L/U 形凹房间；网格锚点定义为区域 WCS 包围盒四角，候选线裁切为一个或多个室内片段。审查修复了大 WCS 坐标偏移下绝对坐标面积计算的消减问题；Debug/Release 81/81 自动测试和完整解决方案隔离重建通过。实机共线分段矩形、L 形凹角裁切、断口拒绝、一次撤销、原线保护和关闭不保存均通过。M10、版本和发布物保持不变。

## V0.1 目标

用户执行正式命令 `TILE600`，选择组成矩形房间的四条墙线。插件识别房间西南角，以 600 × 600 mm、灰缝 0 mm 的固定规则，从西向东、从南向北生成地砖分格线，并写入 `TILE_LAYOUT_600` 专用图层。

## 支持范围

- Windows x64
- Autodesk AutoCAD 2021
- C# / .NET Framework 4.8
- 模型空间
- WCS 世界坐标
- 与 WCS X/Y 轴平行的四线矩形房间
- 独立 `TILEORTHO`：4 条及以上 WCS 正交 `LINE` 组成的单一、无洞、无自交简单房间
- 图纸单位：毫米

## 暂不支持

- 旋转矩形和自定义 UCS
- 任意斜边、旋转网格、门洞、柱、地漏和多外环
- 灰缝、居中、对称、窄砖优化和材料损耗优化
- 多房间通缝、墙砖和独立 EXE

## 安全原则

- 不修改或删除原四条墙线。
- 所有排版结果写入专用新图层。
- 一次操作可以通过一次 AutoCAD `UNDO` 撤销。
- 插件不自动保存或覆盖 DWG。

## 开发入口

- 项目范围与里程碑：[PROJECT.md](PROJECT.md)
- V0.1需求基线：[docs/requirements-v0.1.md](docs/requirements-v0.1.md)
- V0.2 冻结需求：[docs/requirements-v0.2.md](docs/requirements-v0.2.md)
- V0.2 技术方案与开发顺序：[docs/technical-plan-v0.2.md](docs/technical-plan-v0.2.md)
- M6 参数化核心与安全计数验收：[docs/core-parameterization-m6.md](docs/core-parameterization-m6.md)
- M7 `TILELAYOUT` AutoCAD 适配验收：[docs/autocad-adapter-m7.md](docs/autocad-adapter-m7.md)
- M8 正式自动质量门验收：[docs/automated-quality-gate-m8.md](docs/automated-quality-gate-m8.md)
- M9 AutoCAD 2021 脱敏 DWG 实机验收：[docs/dwg-acceptance-m9.md](docs/dwg-acceptance-m9.md)
- 起铺控制功能基线与验收：[docs/start-control.md](docs/start-control.md)
- 非矩形房间能力边界与依赖评估：[docs/non-rectangular-room-assessment.md](docs/non-rectangular-room-assessment.md)
- 正交简单房间需求、接口、自动验收和实机清单：[docs/orthogonal-simple-room.md](docs/orthogonal-simple-room.md)
- 技术路线决策：[docs/adr/0001-autocad-2021-managed-net-plugin.md](docs/adr/0001-autocad-2021-managed-net-plugin.md)
- M2 核心契约与验证记录：[docs/core-algorithm-m2.md](docs/core-algorithm-m2.md)
- M3 正式集成与实机验收：[docs/autocad-integration-m3.md](docs/autocad-integration-m3.md)
- M4 脱敏 DWG 流程与验收矩阵：[docs/dwg-acceptance-m4.md](docs/dwg-acceptance-m4.md)
- V0.1.0 发布检查记录：[docs/release-v0.1.0.md](docs/release-v0.1.0.md)
- 项目执行规则：[AGENTS.md](AGENTS.md)

## 解决方案结构

```text
src/
├─ TileLayout.Core/
└─ TileLayout.AutoCAD/

tests/
└─ TileLayout.Core.Tests/
```

## 构建与测试

需要 Visual Studio 2022、MSBuild、VSTest 和 .NET Framework 4.8 Developer Pack。以下命令在项目根目录的“Developer PowerShell for VS 2022”中执行。

首次恢复测试依赖：

```powershell
msbuild .\TileLayout.sln /t:Restore /p:RestoreConfigFile="$PWD\NuGet.Config" /p:RestoreLockedMode=true
```

测试依赖仅包括微软维护的 `Microsoft.NET.Test.Sdk 18.8.1`、`MSTest.TestAdapter 4.3.2` 和 `MSTest.TestFramework 4.3.2`，均使用 MIT 许可，只供测试项目使用。精确依赖树记录在 `tests\TileLayout.Core.Tests\packages.lock.json`，NuGet 包缓存写入被 Git 忽略的 `build\packages`。

编译核心和测试项目：

```powershell
msbuild .\tests\TileLayout.Core.Tests\TileLayout.Core.Tests.csproj /t:Rebuild /p:Configuration=Debug /p:Platform=AnyCPU
```

运行核心和宿主无关适配自动测试，不需要启动 AutoCAD：

```powershell
vstest.console.exe .\build\tests\Debug\TileLayout.Core.Tests.dll /Platform:x64
```

编译完整解决方案前应先正常关闭 AutoCAD，避免已由 `NETLOAD` 加载的 DLL 被宿主锁定：

```powershell
msbuild .\TileLayout.sln /t:Rebuild /p:Configuration=Debug /p:Platform=x64
```

正式插件和 M1 探针也可分别单独编译。本机配置方式仍是复制 `config/AutoCAD.Local.props.example` 为被 Git 忽略的 `config/AutoCAD.Local.props`，并填写 AutoCAD 2021 安装目录；也可以设置环境变量 `AUTOCAD2021_DIR`。

```powershell
msbuild .\src\TileLayout.AutoCAD\TileLayout.AutoCAD.csproj /t:Rebuild /p:Configuration=Debug /p:Platform=x64
```

```powershell
msbuild .\src\TileLayout.AutoCAD\TileLayout.AutoCAD.Probe.csproj /t:Rebuild /p:Configuration=Debug /p:Platform=x64
```

主要输出：

```text
build\core\Debug\TileLayout.Core.dll
build\tests\Debug\TileLayout.Core.Tests.dll
build\plugin\Debug\TileLayout.AutoCAD.dll
build\plugin\Debug\TileLayout.Core.dll
build\probe\Debug\TileLayout.AutoCAD.Probe.dll
```

`build\plugin` 是开发与实机验证输出，不是正式交付目录。V0.1.0 本地交付候选包位于 `dist`；`TileLayout.AutoCAD.Probe.dll` 仍只用于 M1 回归，不得把探针当作正式插件。

## M3 正式插件实机验收

1. 使用新建空白图或脱敏测试副本，确认当前处于模型空间且 `INSUNITS` 为毫米。
2. 在 AutoCAD 2021 执行 `NETLOAD`，加载 `build\plugin\Debug\TileLayout.AutoCAD.dll`。
3. 执行 `TILE600`，选择恰好四条模型空间 `LINE`。
4. 按 M3 验收文档依次检查 3600 × 3000、4250 × 3100、500 × 500、无效四线、原墙线保护和一次撤销。
5. 不在正式 DWG 中首次试验；插件不会自动保存，是否保存测试副本只由用户决定。

完整步骤、预期值和已通过记录见 [docs/autocad-integration-m3.md](docs/autocad-integration-m3.md)。M3 已于 2026-07-20 完成实机验收。

## 第一技术探针

1. 在 AutoCAD 2021 命令行执行 `NETLOAD`。
2. 加载 `build\probe\Debug\TileLayout.AutoCAD.Probe.dll`。
3. 执行 `TILE600PROBE` 并选择四条 `LINE`。
4. 确认命令行输出端点、矩形尺寸与西南角坐标。
5. 确认插件只在 `TILE_LAYOUT_PROBE` 测试图层新增一条线。
6. 使用一次 `U` 或 `UNDO` 撤销整次操作。

详细实机步骤和记录表见 [docs/technical-probe-m1.md](docs/technical-probe-m1.md)。

本次 M3 未执行提交、推送、PR、发布或部署。Codex 未直接打开或保存 DWG/DOCX；用户仅在测试图中完成实机验收，并确认插件未自动保存图纸。

## M4 脱敏 DWG 验收

M4 必须使用用户确认已经脱敏的真实图纸抽取副本，不在原始 DWG 上操作。原始文件只读保留在 `inputs`，未脱敏中间副本进入被 Git 忽略的 `work/m4-redaction`，最终脱敏夹具和非敏感说明进入 `tests/fixtures`。详细脱敏清单、初学者实机步骤、16 项验收矩阵、证据字段和缺陷修复门见 [docs/dwg-acceptance-m4.md](docs/dwg-acceptance-m4.md)。

最终只读夹具 `tests/fixtures/m4-real-room-sanitized.dwg` 的大小和 SHA-256 已记录，M4-01 至 M4-16 均已通过。最终锁定模式恢复、Debug/Release 各 27/27 自动测试及 `build/solution-m4-final` 备用目录下的完整解决方案 Debug/Release 重建均通过且未复制 Autodesk DLL。AutoCAD 全程由用户操作并保持运行，未被 Codex 启动或关闭。详细证据见 [docs/dwg-acceptance-m4.md](docs/dwg-acceptance-m4.md) 和 `tests/fixtures/m4-real-room-sanitized.md`。

## M5 V0.1.0 本地交付候选包

- 解压目录：`dist/TileLayout-0.1.0/`
- 压缩包：`dist/TileLayout-0.1.0.zip`
- SHA-256：`322077112229CA8E0EDFB0CEE0B1F3F192A24EAC2CC23D11DCEA413CC3431141`
- 校验清单：`dist/TileLayout-0.1.0-sha256.txt`
- 包内文件：`TileLayout.AutoCAD.dll`、`TileLayout.Core.dll`、`使用说明.md`

2026-07-20，完整解决方案 Release 在 `build/solution-m5/Release` 备用目录重建通过，Release 自动测试 27/27 通过；两个程序集的 AssemblyVersion/FileVersion 均为 `0.1.0.0`。用户随后从交付包 `NETLOAD` 正式 DLL，在脱敏夹具执行一次 `TILE600`，确认加载成功、生成 23 条并可一次撤销。V0.1.0 已发布到私有仓库的 [GitHub Release](https://github.com/PermissionDog001/DEV-001-AutoCAD-Tile-Layout/releases/tag/v0.1.0)，包含 ZIP 和 SHA-256 清单；尚未公开发布。详见 [docs/release-v0.1.0.md](docs/release-v0.1.0.md)。

## M7 TILELAYOUT AutoCAD 适配

正式插件现已注册 `TILELAYOUT`。命令依次提示砖宽和砖高，每次默认均为 600 mm；宽沿 WCS X、高沿 WCS Y，非法尺寸可重新输入，取消任一参数会在边界选择前退出。取得合法参数后，新命令复用 `TILE600` 的模型空间、毫米单位、四条 `LINE` 只读快照、矩形验证、事务写回和回滚路径，并固定写入 `TILE_LAYOUT`。

M7 只完成代码接入和宿主无关自动验证，当时没有操作 DWG 或宣称通过 AutoCAD 实机行为。参数提示、取消、既有图层属性、超限图层前拒绝、一次撤销和失败回滚的宿主证据随后已由 M9 补充。详细记录见 [docs/autocad-adapter-m7.md](docs/autocad-adapter-m7.md)。

## M8 正式自动质量门

M8 已逐项复核 `TILE600` 兼容、新命令参数/取消顺序、共享选择与事务路径、`TILE_LAYOUT` 图层复用、10,000 条图层前拒绝以及三类宿主无关消息。锁定模式恢复和 Debug/Release 各 51/51 自动测试通过；AutoCAD 2021 运行期间，完整解决方案在新的 `build/solution-m8` 备用目录完成双配置重建，未覆盖标准插件 DLL，输出未包含 Autodesk Managed DLL。

核心和正式插件版本仍为 `0.1.0.0`，`dist` 中 V0.1.0 ZIP 哈希、既有本地标签及原始只读 DWG 哈希均保持基线。M8 没有操作 DWG、修改发布物或执行远端写入；当时留给 M9 的 AutoCAD 宿主行为随后已完成验收。详细记录见 [docs/automated-quality-gate-m8.md](docs/automated-quality-gate-m8.md)。

## M9 AutoCAD 2021 脱敏 DWG 实机验收

M9 已在 5600 × 8600 mm 脱敏工作副本中完成。`TILE600` 与 `TILELAYOUT 600×600` 均生成 23 条且分别写入各自图层；600 × 1200、1200 × 600、800 × 800 和 700 × 1200 的列行、余量、线数及代表坐标均符合预期。默认值、非法值重输、宽/高/选择取消、无效四线、11,199 条超限、既有锁定图层属性、重复追加、一次撤销、墙线保护和关闭不保存均通过。

关闭不保存后，原始输入、只读夹具与 M9 工作副本仍为 31,890 字节，SHA-256 均为 `646A3A7A22CF40E5EC0B9CF8621A17AFAB09BB27928772C05D9CB3F4202DDA75`。未实机生成恰好 10,000 条，也未人为注入实体写入/提交异常；这些边界和原因已明确记录。详见 [docs/dwg-acceptance-m9.md](docs/dwg-acceptance-m9.md)。M10 尚未开始。

## 工程起铺控制

`TILELAYOUT` 当前交互冻结为“砖宽 → 砖高 → 起铺角 → 四条边界 `LINE`”。起铺角使用 WCS 方位，接受 `SW`、`SE`、`NW`、`NE`，默认 `SW`；选择哪一角，就从该角向房间内部沿两个方向起排，非整除切砖落在对边。成功消息报告起铺角、两个起排方向和实际余量边。

核心四角坐标、顺序、余量落边、默认兼容、整除边界及既有资源上限已由 56/56 Debug 自动测试覆盖；正式 AutoCAD 项目也已隔离编译通过。AutoCAD 2021 最小实机进一步确认东北角生成 23 条、首条竖/横线坐标、角选择取消、`TILE600` 兼容、撤销和关闭不保存，未重复 M9 的全量矩阵。完整定义、样例和证据见 [docs/start-control.md](docs/start-control.md)。

## 正交简单房间

新增独立命令 `TILEORTHO`，交互为“砖宽 → 砖高 → WCS 包围盒网格锚点 `SW/SE/NW/NE` → 选择 4 条及以上 `LINE`”。锚点只定义网格相位，允许位于凹房间外；它不等同于必定位于房内的第一块砖角。输出固定写入 `TILE_LAYOUT_ORTHO`，单次最多 10,000 条最终室内片段。

核心会以选择顺序无关的规则归并公差内端点、拒绝吸附歧义/断口/重叠/T 形/自交/分离环，并合并相邻共线碎段。裁切采用半开顶点规则和奇偶区间，支持一条候选网格线产生多个室内片段，同时扣除与房间边界重合的部分。`TILE600` 和 `TILELAYOUT` 保持既有四线矩形行为。

自动实现、聚焦代码审查和 Debug/Release 81/81 测试已通过；审查新增了大 WCS 坐标偏移的面积稳定性回归。正式插件隔离输出为 `build/orthogonal-room/Debug/TileLayout.AutoCAD.dll`。AutoCAD 2021 最小实机也已确认共线分段矩形 9 条、L 形 4 条、断口拒绝、一次撤销、原线保护和不保存；完整步骤及证据见 [docs/orthogonal-simple-room.md](docs/orthogonal-simple-room.md)。任意斜边、洞口/柱、旋转网格和多房间通缝继续暂缓。
