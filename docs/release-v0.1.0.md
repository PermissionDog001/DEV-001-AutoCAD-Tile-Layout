# V0.1.0 发布检查记录

## 当前状态

V0.1.0 本地交付包及全部 M5 检查已完成，尚未对外发布。Release 构建、自动测试、包内容、SHA-256 和一次 AutoCAD 2021 冒烟均已通过。未经用户明确授权，不提交、推送、创建 PR、创建远程 Release、发布或部署。

## 版本与目标环境

- 产品版本：`0.1.0`
- 程序集版本：`0.1.0.0`
- 平台：Windows x64
- 宿主：Autodesk AutoCAD 2021
- 目标框架：.NET Framework 4.8
- 正式命令：`TILE600`
- 输出图层：`TILE_LAYOUT_600`

## 本地交付物

- 解压目录：`dist/TileLayout-0.1.0/`
- 压缩包：`dist/TileLayout-0.1.0.zip`
- 哈希清单：`dist/TileLayout-0.1.0-sha256.txt`
- 压缩包最终 SHA-256：`322077112229CA8E0EDFB0CEE0B1F3F192A24EAC2CC23D11DCEA413CC3431141`

压缩包内固定只包含：

1. `TileLayout.AutoCAD.dll`
2. `TileLayout.Core.dll`
3. `使用说明.md`

不得包含 Autodesk DLL、探针 DLL、测试 DLL、第三方测试组件、PDB、DWG、缓存、日志或其他无关文件。

## 自动检查结果

- NuGet 锁定模式恢复：通过，依赖均为最新状态。
- 完整解决方案 Release 重建：通过，使用 `build/solution-m5/Release` 备用输出，未覆盖 AutoCAD 可能锁定的标准目录。
- Release 自动测试：27/27 通过。
- 正式插件依赖：发布包只带 `TileLayout.Core.dll`；AutoCAD Managed DLL 由宿主提供，未复制进包。
- 原始 `inputs/test.dwg`：最终仍为只读、31,890 字节，SHA-256 为 `646A3A7A22CF40E5EC0B9CF8621A17AFAB09BB27928772C05D9CB3F4202DDA75`，与 M4 基线一致。
- 产品源码：M5 未发现可复现缺陷，未修改。

## 唯一一次 AutoCAD 2021 冒烟

使用只读脱敏夹具的临时打开状态，不保存：

1. `NETLOAD` 加载解压目录中的 `TileLayout.AutoCAD.dll`。
2. 执行 `TILE600`，选择已知的四条房间边界 `LINE`。
3. 确认命令报告 5600 × 8600 mm、完整列/行 9/14、东/北余量 200/200 mm，并生成 23 条。
4. 成功后不要插入其他命令，立即执行一次 `U`；确认 23 条分格线整体消失，墙线仍在。
5. 关闭测试图并选择“不保存”。

不重复 M4 的错误矩阵。

2026-07-20 用户实机回报：Release 交付包中的正式 DLL 加载成功；`TILE600` 正确生成 23 条；一次撤销成功。M5 发布包冒烟通过。

## 已知限制

V0.1.0 只支持模型空间、毫米单位、同一高程、WCS 轴对齐的单个四线矩形。固定使用 600 × 600 mm、灰缝 0 mm，从西南角起铺；东、北侧直接截断。不支持旋转房间、自定义 UCS、洞口、柱、地漏、异形空间、多房间、墙砖、优化排版或独立 EXE。程序集未代码签名，可能触发 Windows 或 AutoCAD 安全提示。

## 发布决定

M5 已完成，本地交付包不等于已经对外发布。真正提交、推送、创建远程 Release、发布或部署仍需用户另行明确授权。
