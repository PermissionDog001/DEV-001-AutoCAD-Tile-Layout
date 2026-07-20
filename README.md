# AutoCAD瓷砖自动排版插件

DEV-001 是一个面向 Autodesk AutoCAD 2021 的 C# Managed .NET 插件项目。

## 当前状态

M1 至 M5 均已完成。M4-01 至 M4-16 全部通过；M5 Release 重建和 27/27 自动测试通过，V0.1.0 最小包、说明和 SHA-256 已核验，AutoCAD 2021 发布包冒烟也已确认正式 DLL 加载成功、生成 23 条并可一次撤销。包中只有两个运行 DLL 和使用说明，未包含 Autodesk DLL、DWG、PDB、缓存或测试组件。未发现需要修改产品源码的可复现缺陷；本地交付包尚未对外发布。

## V0.1 目标

用户执行正式命令 `TILE600`，选择组成矩形房间的四条墙线。插件识别房间西南角，以 600 × 600 mm、灰缝 0 mm 的固定规则，从西向东、从南向北生成地砖分格线，并写入 `TILE_LAYOUT_600` 专用图层。

## 支持范围

- Windows x64
- Autodesk AutoCAD 2021
- C# / .NET Framework 4.8
- 模型空间
- WCS 世界坐标
- 与 WCS X/Y 轴平行的四线矩形房间
- 图纸单位：毫米

## 暂不支持

- 旋转矩形和自定义 UCS
- 门洞、柱、地漏、墙垛、异形边界
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

2026-07-20，完整解决方案 Release 在 `build/solution-m5/Release` 备用目录重建通过，Release 自动测试 27/27 通过；两个程序集的 AssemblyVersion/FileVersion 均为 `0.1.0.0`。用户随后从交付包 `NETLOAD` 正式 DLL，在脱敏夹具执行一次 `TILE600`，确认加载成功、生成 23 条并可一次撤销。M5 已完成；本地交付包不等于已经对外发布。详见 [docs/release-v0.1.0.md](docs/release-v0.1.0.md)。
