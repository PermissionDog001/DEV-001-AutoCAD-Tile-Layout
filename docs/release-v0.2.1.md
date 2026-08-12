# TileLayout V0.2.1 发布准备记录

发布日期：2026-08-12

版本号：`v0.2.1`

程序集版本：`0.2.1.0`

适用环境：Windows x64、AutoCAD 2021、.NET Framework 4.8

状态：本地发布候选包已准备，待用户另行授权后再创建远程 GitHub Release；本记录不代表已完成远程发布。

## 这次更新带来了什么

在保留既有命令、预览零写入和确认后单事务写回边界的基础上，本版本收口以下能力：

- **项目规则比例界面**：建议下限比例默认 `0.5`，按对应方向砖宽/砖高换算；项目最低允许规则支持毫米下限、比例下限和按图面确认。
- **灰缝候选恢复**：局部砖体因灰缝无法形成有效实体时，作为当前候选淘汰并继续搜索，不再误报为输入不可信。
- **自动尺寸标注**：引导式 UI 默认提供建筑样式自动标注，使用房间内中心安全带、代表行/列和特殊砖最长必要尺寸规则。
- **自动起铺点标志**：在远离门口墙面的贴墙整砖/半砖首排或首列中，标志放在四砖交界灰缝中心；向内箭头和实际铺贴大方向箭头随方案计算，不按屏幕方向固定。

既有 `TILE600`、`TILELAYOUT`、`TILEORTHO` 和 `TILEDOORRECT` 命令保持原有行为。引导式正式结果仍分别使用 `TILE_LAYOUT_ORTHO_CONFIRMED`、`TILE_LAYOUT_ORTHO_DIM` 和 `TILE_LAYOUT_ORTHO_START` 等专用图层；AutoCAD Managed DLL 不打包进交付包。

## 安装和使用

1. 解压 `TileLayout-0.2.1.zip`，得到两个插件程序集和中文使用说明。
2. 在 AutoCAD 2021 中执行 `NETLOAD`，选择 `TileLayout.AutoCAD.dll`。
3. 普通用户执行 `TILEUI`，按窗口顺序选择房间、设置、门洞（如有）、方案和最终确认。
4. 在预览中核对分格、尺寸标注和起铺点：起铺点应位于远墙首排/首列的四砖灰缝中心，贴墙侧为整砖或半砖；箭头一个指向房间内，另一个与实际铺贴大方向一致。
5. 只有点击最终确认后才会写入正式图层；如需撤销，执行一次 AutoCAD `U` 或 `UNDO`。

## 发布包

压缩包：`dist/TileLayout-0.2.1.zip`

压缩包大小：`191,853` bytes

压缩包 SHA-256：`FBED79DEF05F9551A591E38DD981C9976B67E118C85F1BA85A9B9E7D890B0F11`

包内只允许包含：

- `TileLayout.AutoCAD.dll`；
- `TileLayout.Core.dll`；
- `使用说明.md`。

不得包含 Autodesk Managed DLL、PDB、测试组件、DWG、缓存或日志。历史 `v0.1.0`、`v0.2.0` 交付物保持不变。

| 文件 | 大小 | SHA-256 |
|---|---:|---|
| `TileLayout.AutoCAD.dll` | 233,472 bytes | `05D12057298DC48DF489304CC2685C1CB248E02D40B42E1778A8B00AF7335449` |
| `TileLayout.Core.dll` | 247,808 bytes | `944C371944C0C967625A1659E099715E02A1B72C20ADD4B67017492CCF62B487` |
| `使用说明.md` | 1,679 bytes | `1CE749E471344FE8C30B492DC1FA4D568D2C9B32223C4D3DC4537E34CC327DB5` |

完整校验清单见 [TileLayout-0.2.1-sha256.txt](../dist/TileLayout-0.2.1-sha256.txt)。

## 验证记录

- Core Debug：`346/346`，使用 `tools/Invoke-CoreReflectionTests.ps1` 手动反射运行 MSTest 方法和 `DataRow`。
- Core Release：`346/346`，使用同一运行器。
- Core、测试项目和 AutoCAD 适配项目构建通过；AutoCAD Release 在 AutoCAD 锁定标准输出目录时使用隔离 `Release-verify` 输出，未强制关闭宿主。
- 自动起铺点回归覆盖远墙、门洞对侧、四砖灰缝中心、非零灰缝和实际沿墙方向箭头。
- 用户已确认 2026-08-12 的 AutoCAD 2021 实机验证达到预期效果。未据此虚构未提供的 DWG 文件名、实体数量、前后哈希或完整操作清单。
- 当前仅完成本地发布准备和 Git 基线提交；未推送、未创建标签、未创建远程 GitHub Release。

## 已知限制

- 仅支持 AutoCAD 2021、Windows x64 和 .NET Framework 4.8；AutoCAD Managed 程序集由宿主提供。
- 旋转房间、自定义 UCS、圆弧/带 bulge 多段线、洞、多外环、柱、地漏、多房间通缝和材料损耗优化仍不支持。
- 标准 `dotnet test` 在当前 .NET Framework 测试宿主上可能无法稳定发现或执行测试；发布检查使用项目内反射运行器，并记录明确通过数。
- 远程 Release、标签和推送需要后续明确授权。
