# AutoCAD瓷砖自动排版插件

DEV-001 是一个面向 Autodesk AutoCAD 2021 的 C# Managed .NET 插件项目。

## 当前状态

项目已完成目录和需求基线初始化，M1 最小 `NETLOAD` 技术探针已于 2026-07-19 通过实机验收：程序集能够加载，命令能够选择四条 `LINE`，矩形尺寸和西南角计算正确，能够在测试图层写入一条线，并可通过一次 `U` 撤销。下一阶段是 M2：建立与 AutoCAD 无关的核心矩形、600 mm 网格算法和自动单元测试。

## V0.1 目标

用户执行暂定命令 `TILE600`，选择组成矩形房间的四条墙线。插件识别房间西南角，以 600 × 600 mm、灰缝 0 mm 的固定规则，从西向东、从南向北生成地砖分格线，并写入 `TILE_LAYOUT_600` 新图层。

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
- 项目执行规则：[AGENTS.md](AGENTS.md)

## 计划中的解决方案结构

```text
src/
├─ TileLayout.Core/
└─ TileLayout.AutoCAD/

tests/
└─ TileLayout.Core.Tests/
```

## 构建与测试

当前只有 M1 最小技术探针，尚未创建完整解决方案、核心算法项目和单元测试项目。

本机配置：复制 `config/AutoCAD.Local.props.example` 为被 Git 忽略的 `config/AutoCAD.Local.props`，并填写 AutoCAD 2021 安装目录。也可以设置环境变量 `AUTOCAD2021_DIR`。

在“Developer PowerShell for VS 2022”中执行：

```powershell
msbuild .\src\TileLayout.AutoCAD\TileLayout.AutoCAD.Probe.csproj /t:Rebuild /p:Configuration=Debug /p:Platform=x64
```

当前探针没有 NuGet 或其他第三方依赖，不需要恢复包。成功输出：

```text
build\probe\Debug\TileLayout.AutoCAD.Probe.dll
```

完整核心测试与插件打包命令将在 M1 实机探针通过后、正式解决方案建立时补充，不在当前阶段伪造。

## 第一技术探针

1. 在 AutoCAD 2021 命令行执行 `NETLOAD`。
2. 加载 `build\probe\Debug\TileLayout.AutoCAD.Probe.dll`。
3. 执行 `TILE600PROBE` 并选择四条 `LINE`。
4. 确认命令行输出端点、矩形尺寸与西南角坐标。
5. 确认插件只在 `TILE_LAYOUT_PROBE` 测试图层新增一条线。
6. 使用一次 `U` 或 `UNDO` 撤销整次操作。

详细实机步骤和记录表见 [docs/technical-probe-m1.md](docs/technical-probe-m1.md)。

远程仓库、提交、推送、发布和部署均未初始化或执行。
