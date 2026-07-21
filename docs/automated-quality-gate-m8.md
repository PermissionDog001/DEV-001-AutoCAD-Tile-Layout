# M8 正式自动质量门验收记录

> 状态：已完成
> 日期：2026-07-20
> 需求基线：`docs/requirements-v0.2.md`
> 技术方案：`docs/technical-plan-v0.2.md`
> 前置验收：`docs/core-parameterization-m6.md`、`docs/autocad-adapter-m7.md`

## 1. 本里程碑范围

M8 只审查 M6/M7 是否完整满足冻结需求，并执行正式自动恢复、测试、完整解决方案重建和产物保护检查。本里程碑没有操作任何 DWG，没有执行 M9 AutoCAD 宿主实机矩阵，也没有修改 `dist`、V0.1.0 Release、程序集版本、Git 标签或远端状态。

开始时已确认分支为 `main`，并完整保留 V0.2 冻结文档、M6 参数化核心和 M7 AutoCAD 适配的全部未提交改动。M8 未清理、回退或覆盖这些既有工作。

## 2. M6/M7 冻结需求审查

- `TILE600` 继续使用固定 600 × 600 mm 兼容入口和 `TILE_LAYOUT_600`，不受 10,000 条新命令上限约束；既有成功、验证、选择、环境和失败消息路径保持兼容。
- `TILELAYOUT` 依次提示砖宽、砖高，两个默认值每次均为 600 mm；非法值继续提示同一参数，任一阶段取消都会在调用共享边界选择路径之前退出。
- 两个命令共用 `ExecuteLayout`，因此模型空间/毫米检查、四条模型空间 `LINE` 过滤、只读核心快照、矩形验证、事务写回、异常回滚、墙线保护和不自动保存提示没有形成两套实现。
- `TILELAYOUT` 固定写入 `TILE_LAYOUT`；图层存在时 `EnsureLayoutLayer` 只返回既有 ID，不改写颜色、线型、线宽或锁定状态。
- 参数化核心计算发生在 `EnsureLayoutLayer` 之前；超过 10,000 条时由核心在创建结果集合及 AutoCAD 图层之前抛出专用异常，恰好 10,000 条允许。固定 `TILE600` 入口仍不限额。
- 参数化成功、参数错误和超限三类宿主无关消息均由 `TileLayoutCommandText` 提供并有自动测试；V0.1 消息回归继续通过。
- `TileLayout.Core` 项目只引用 `System` 和 `System.Core`，没有 Autodesk 引用；AutoCAD 三个 Managed 引用继续设置 `<Private>False</Private>`。

审查未发现由 M6/M7 引入、可通过当前自动环境复现的产品缺陷，因此 M8 没有修改产品源码或增加回归测试。

## 3. 正式命令与结果

使用 MSBuild `17.14.51.32402` 和 VSTest `17.14.0 x64`：

```powershell
msbuild .\TileLayout.sln /t:Restore /p:RestoreConfigFile="$PWD\NuGet.Config" /p:RestoreLockedMode=true
msbuild .\tests\TileLayout.Core.Tests\TileLayout.Core.Tests.csproj /t:Rebuild /p:Configuration=Debug /p:Platform=AnyCPU
vstest.console.exe .\build\tests\Debug\TileLayout.Core.Tests.dll /Platform:x64
msbuild .\tests\TileLayout.Core.Tests\TileLayout.Core.Tests.csproj /t:Rebuild /p:Configuration=Release /p:Platform=AnyCPU
vstest.console.exe .\build\tests\Release\TileLayout.Core.Tests.dll /Platform:x64
```

AutoCAD 运行期间的完整解决方案重建额外设置了按配置隔离的 `OutDir`，并把 `IntermediateOutputPath` 指向含 `$(MSBuildProjectName)` 的 M8 专用中间目录；实际输出分别为 `build/solution-m8/Debug` 和 `build/solution-m8/Release`。

结果：

- NuGet 锁定模式恢复通过，所有项目均为最新状态。
- Debug 测试项目重建通过，自动测试 51/51 通过。
- Release 测试项目重建通过，自动测试 51/51 通过。

验证时 AutoCAD 2021 仍在运行，进程 PID 为 `23772`。完整解决方案使用新的 `build/solution-m8/Debug`、`build/solution-m8/Release` 以及隔离的 `build/obj-solution-m8` 中间目录完成 Debug/Release `Rebuild`，两套配置均成功生成核心、探针、正式插件和测试程序集。没有覆盖 `build/plugin` 的标准 DLL，没有启动、关闭或操作 AutoCAD。

## 4. 产物与历史保护检查

- `build/solution-m8/Debug` 和 `build/solution-m8/Release` 均未包含 `AcCoreMgd.dll`、`AcDbMgd.dll` 或 `AcMgd.dll`。
- 两套输出中的 `TileLayout.Core.dll` 与 `TileLayout.AutoCAD.dll` 的 AssemblyVersion/FileVersion 均为 `0.1.0.0`；版本按计划留到 M10 统一更新。
- `dist/TileLayout-0.1.0.zip` 仍为 12,531 字节，SHA-256 仍为 `322077112229CA8E0EDFB0CEE0B1F3F192A24EAC2CC23D11DCEA413CC3431141`；ZIP 仍只含两个运行 DLL 和 `使用说明.md`。
- 本地 `v0.1.0` 标签对象仍为 `47cb17ad28f60bb8e81549b7ad8e2b5261198c4d`，解引用提交仍为 `b31bc13867e8beccda676d3ae8f2797965c6ea88`。
- M8 未执行任何提交、推送、标签修改、Release 修改或发布命令。远端只读复核因本机没有可用的 `gh`，且 SSH 远端认证缺少公钥而未完成；这不影响确认 M8 没有改动既有 V0.1.0 Release，但不作为远端元数据重新验收。
- 原始 `inputs/test.dwg` 仍为只读、31,890 字节，SHA-256 为 `646A3A7A22CF40E5EC0B9CF8621A17AFAB09BB27928772C05D9CB3F4202DDA75`。
- `git diff --check` 通过。

## 5. 未验证项与下一步

M8 没有执行或提前宣称 M9/M10 通过。以下项目仍须在新的 M9 任务中由用户使用脱敏 DWG 和 AutoCAD 2021 实机验证：

- 参数默认、非法值重输以及宽/高任一阶段取消；
- 方砖、矩形砖、宽高交换、非零高程和关键坐标；
- `TILE_LAYOUT` 既有属性保持、重复追加和超限图层前拒绝；
- 一次撤销、失败回滚、原墙线保护和关闭不保存后的恢复。

V0.2.0 版本更新、最小包、说明、SHA-256 和发布包冒烟继续留给 M10。M8 已安全收尾，可把 M9 作为独立的新 Codex 任务开始。
