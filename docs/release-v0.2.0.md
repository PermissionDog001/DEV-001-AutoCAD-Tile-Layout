# TileLayout V0.2.0 发布说明

发布日期：2026-08-04

版本号：`v0.2.0`

程序集版本：`0.2.0.0`
适用环境：Windows x64、AutoCAD 2021、.NET Framework 4.8

## 这次更新带来了什么

在保留原有命令和原有安全边界的基础上，本版本增加并完善了三项普通用户可直接使用的能力：

- **灰缝**：默认砖间灰缝为 `1.5 mm`，也可以输入 `0`。砖与砖之间使用完整灰缝，砖与房间边界之间使用半宽灰缝；墙角对齐的是灰缝中心位置。图面会显示灰缝两侧的边界线。灰缝只影响分格线之间的间距，不改变砖的名义尺寸，也不把灰缝占位计入推荐下限和绝对下限。
- **闭合多段线房间**：除原有的一组 `LINE` 外，可以直接选择一个闭合的 `LWPOLYLINE` 或传统二维 `POLYLINE` 作为房间边界。首期支持 WCS 近似水平/竖直的单一外环，包括轴对齐凹多边形；不支持三维多段线、圆弧、洞、多环、自交、重复边和混合边界输入。
- **抹灰完成面**：默认厚度为 `0 mm`。输入正值后，程序先把原始边界向房间内部生成统一厚度的抹灰完成面，再以完成面边界计算门洞、区域和砖排版。完成面无效时会清除旧预览、不生成方案，也不写入对象；厚度不为 `0` 时，完成面轮廓会与排版结果一起写入专用图层，原始 LINE/POLYLINE 始终不修改。

同时，本版本优化了引导式窗口的刷新、候选切换、预览失效、取消/失败处理和重复计算，降低了界面反复刷新造成的等待。预览仍然是零写入的，只有用户明确确认后才写入正式图形；正式写回继续只生成 `DivisionLines + Connections`，并保持一次 `UNDO` 可撤销。

既有 `TILE600`、`TILELAYOUT`、`TILEORTHO` 和 `TILEDOORRECT` 流程保持不变，继续按 `0 mm` 灰缝运行。

## 安装和使用

1. 解压 `TileLayout-0.2.0.zip`，得到两个 DLL 和本说明文件。
2. 在 AutoCAD 2021 中执行 `NETLOAD`，选择 `TileLayout.AutoCAD.dll`。
3. 普通用户推荐执行 `TILEUI`，按窗口顺序选择房间、门洞（如有）、砖和灰缝，查看预览后再确认写入。
4. 详细操作、边界要求、常见提示和取消方式见 [user-guide.md](user-guide.md)。

## 发布包

压缩包：`dist/TileLayout-0.2.0.zip`

压缩包大小：`170,603` bytes

压缩包 SHA-256：`7EF6B392396FDF6B751C442C7B6A1258356C52390491FEE51D4ECFF0A1AAC689`

包内文件：

| 文件 | 大小 | SHA-256 |
|---|---:|---|
| `TileLayout.AutoCAD.dll` | 209,920 bytes | `E703C69503C3EDF677991900C53D52B9C3B426E056AE6BB13B7B34B5D46EEFE0` |
| `TileLayout.Core.dll` | 211,968 bytes | `348CB100C5D95C5624AC5142748E3DE6D5CC938EB0EA02D61AEABB78AFAFA4E6` |
| `使用说明.md` | 4,456 bytes | `14017628E7BD37F09E630567660F8C0926ED46F9B21F2911A12421AEA6DBA50F` |

完整校验清单见 [TileLayout-0.2.0-sha256.txt](../dist/TileLayout-0.2.0-sha256.txt)。旧版 `v0.1.0` 包仍保留，作为历史版本，不被本次发布覆盖。

GitHub Release：<https://github.com/PermissionDog001/DEV-001-AutoCAD-Tile-Layout/releases/tag/v0.2.0>

## 验证记录

- Core Debug：`333/333` 测试通过。
- Core Release：`333/333` 测试通过。
- AutoCAD 适配 Debug/Release：独立构建 `0` 警告、`0` 错误；版本包使用的 Release 程序集版本为 `0.2.0.0`。
- 写回边界检查通过：正式写回只消费 `DivisionLines + Connections`；原始房间边界不被修改；预览、取消、失败和未确认状态不写入正式对象。
- 用户已确认 DOR9 修复后的 AutoCAD 2021 实机测试完成。

历史专项记录中的未复核证据仍按原记录保留，本发布不以文字说明替代独立的 AutoCAD DWG 副本证据。
