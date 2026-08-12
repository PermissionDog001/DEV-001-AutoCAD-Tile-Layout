# TileLayout v0.2.1 更新说明

发布日期：2026-08-12

版本号：`v0.2.1`

程序集版本：`0.2.1.0`

适用环境：Windows x64、AutoCAD 2021、.NET Framework 4.8

## 本次更新

- **项目铺贴规则设置**：建议下限比例默认 `0.5`，会按 X、Y 方向分别对应砖宽和砖高换算；最低允许尺寸支持毫米或比例两种填写方式，也可以选择按图面确认。
- **灰缝排版稳定性**：对灰缝较窄、无法保留有效砖体的位置改进处理方式，程序会继续寻找可用排版，并给出清晰提示。
- **自动尺寸标注**：引导式流程默认提供建筑样式尺寸标注；大面尺寸链、特殊切砖尺寸和标注位置更清晰，尺寸显示为整数毫米。
- **自动起铺点标志**：标志位于远离门口墙面的首排或首列、贴墙整砖或半砖对应的四砖灰缝中心；一个箭头指向房间内部，另一个箭头指向实际铺贴方向。
- **贴墙边界线**：紧贴抹灰完成面的同一直线边界线贯通显示，不再按每块砖拆分，便于整体选择、删除和调整。
- **原有命令保持不变**：`TILE600`、`TILELAYOUT`、`TILEORTHO` 和 `TILEDOORRECT` 继续按原有方式使用。

## 安装使用

1. 下载本页附件 `TileLayout-0.2.1.zip`。
2. 解压后，在 AutoCAD 2021 中执行 `NETLOAD`，选择 `TileLayout.AutoCAD.dll`。
3. 推荐执行 `TILEUI`，按窗口提示选择房间、设置、门洞和排版方案，确认预览无误后写入图纸。
4. 详细操作见 [用户使用说明](user-guide.md)。

## 发布包

压缩包：`dist/TileLayout-0.2.1.zip`

压缩包大小：`192,534` bytes

压缩包 SHA-256：`E89C0BBC4629290BB3A7EC65A83EB27153574A6F2E18083295A4A9899E09C921`

包内只允许包含：

- `TileLayout.AutoCAD.dll`；
- `TileLayout.Core.dll`；
- `使用说明.md`。

压缩包只包含插件运行文件和使用说明。

| 文件 | 大小 | SHA-256 |
|---|---:|---|
| `TileLayout.AutoCAD.dll` | 233,472 bytes | `09C17CFDD0E66D459CCEA528A35D6BCD9A7A1439C8B5F5D016B66B2637DB2FD8` |
| `TileLayout.Core.dll` | 249,344 bytes | `B59CFCD5E4A205E4E45BA1A3883EC4ED6D129F1D459B9D2F3CC95BB3534AE941` |
| `使用说明.md` | 1,942 bytes | `377359CA6DA175D436AB0535F6B02773E11A8BA63C0467F76A41A0407C7EC5BD` |

完整校验清单见 [TileLayout-0.2.1-sha256.txt](../dist/TileLayout-0.2.1-sha256.txt)。
