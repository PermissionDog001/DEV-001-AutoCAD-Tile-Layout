# AutoCAD 瓷砖自动排版插件

这是一个运行在 Autodesk AutoCAD 2021 中的 C# Managed .NET 插件。它根据房间边界生成地砖排版方案，先提供图面预览，只有用户明确确认后才把结果写入图纸。

正式版本：`v0.2.1`

适用环境：Windows x64、AutoCAD 2021、.NET Framework 4.8

图纸单位：毫米，模型空间，WCS 坐标

## 目前支持的功能

- **引导式排版**：推荐使用 `TILEUI`；`TILEORTHOUI` 是兼容入口。
- **房间边界**：可以选择多条 `LINE`，也可以选择一个闭合的直线型 `LWPOLYLINE`、二维 `POLYLINE` 或 `3D POLYLINE`；若多段线首尾只有不超过 `3 mm` 的绘图误差，也会在只读计算副本中自动连接。支持 WCS 近似水平/竖直的凹形房间。
- **灰缝**：默认 `1.5 mm`，允许输入 `0`。砖与砖之间使用完整灰缝，砖与房间边界之间使用半宽灰缝；墙角对齐灰缝中心。图面显示灰缝两侧边界线，灰缝不改变砖的名义尺寸。
- **抹灰完成面**：默认 `0 mm`。输入正值后，程序先从原始边界向房间内部生成统一厚度的完成面，再根据完成面计算门洞、区域和排版。厚度不为 `0` 时，完成面轮廓也会写入专用图层。
- **门洞**：在引导式流程中选择同一段外墙上的门洞两侧边缘点，程序自动处理门洞附近的排版关系。
- **方案预览**：可切换方案、查看窄边砖和墙角对缝结果；修改设置、取消或失败时不会留下旧预览。
- **自动尺寸标注**：第 1 页默认勾选“自动添加尺寸标注（建筑样式，默认勾选）”，大面选择房间内连续通长的代表性第一行和连续通宽的代表性第一列，链内每块砖逐块标注；房间内模式优先把通用尺寸链放在最接近房间中心且不贴边界的砖边，避免斜短线遮挡阳角、墙角和对缝关系。特殊切砖、异形砖和特殊位置只保留每个方向最长的必要尺寸并去重。房间凹边、凸边、转角台阶尺寸默认关闭，可单独开启。标注位置默认房间内，也可切换为房间外；数值四舍五入到整数且不带 `mm` 后缀。正式写回使用插件专用标注样式 `TILE_LAYOUT_ANNOTATION`，不受当前图纸 DIMSTYLE 影响。
- **自动起铺点标志**：引导式 UI 会在远离门口的墙一侧首排/首列中，选择贴墙边砖为整砖或半砖的位置，把标志放在四块砖交界的灰缝中心；箭头分别指向房间内和实际铺贴大方向。正式标志写入 `TILE_LAYOUT_ORTHO_START` 图层，旧命令不受影响。
- **贴墙边界线**：紧贴抹灰完成面的同一直线边界线贯通显示，不再按每块砖拆分，便于整体选择、删除和调整。
- **图面颜色**：第 1 页可选择瓷砖分割线、砖尺寸标注、凹凸/特殊标注和抹灰边界颜色，提供 AutoCAD 常用 ACI 1～7 颜色，默认分别为 3、2、6、4。
- **正式写回**：确认后写入专用图层，并保留 AutoCAD 一次 `UNDO` 撤销边界。

## 命令说明

| 命令 | 用途 |
|---|---|
| `TILEUI` | 推荐入口，按窗口提示完成房间、设置、门洞、方案预览和确认写回。 |
| `TILEORTHOUI` | 与 `TILEUI` 相同的兼容入口。 |
| `TILE600` | 600 × 600 mm 矩形房间流程，固定使用 0 mm 灰缝。 |
| `TILELAYOUT` | 可输入砖宽、砖高和起铺角的矩形房间流程，固定使用 0 mm 灰缝。 |
| `TILEORTHO` | 多条 WCS 近正交 `LINE` 的简单房间流程，固定使用 0 mm 灰缝。 |
| `TILEDOORRECT` | 矩形房间的两点门洞流程，固定使用 0 mm 灰缝。 |

## 安装和使用

1. 从 [GitHub Release v0.2.1](https://github.com/PermissionDog001/DEV-001-AutoCAD-Tile-Layout/releases/tag/v0.2.1) 下载 `TileLayout-0.2.1.zip`。
2. 解压后，在 AutoCAD 2021 中执行 `NETLOAD`，选择 `TileLayout.AutoCAD.dll`。
3. 进入模型空间，执行 `TILEUI`。
4. 按窗口顺序选择房间边界、输入砖尺寸和灰缝，必要时输入抹灰厚度并选择门洞。
5. 选择排版方案，查看临时预览和图面核对结果。
6. 在第 1 页确认自动标注、标注位置、房间台阶标注开关和颜色；默认标注位置为房间内。大面使用连续通长/通宽的第一行和第一列，链内每块砖逐块标注；特殊切砖/异形砖只补充每个方向最长且去重后的必要尺寸，预览确认后才会写入标注图层。
7. 核对自动起铺点标志：标志应位于远离门口的墙一侧、贴墙整砖或半砖对应的四砖灰缝中心；一个箭头指向房间内，另一个箭头与实际铺贴大方向一致。
8. 只有点击最后的确认写回按钮后，结果才会写入图纸。

详细的操作步骤和常见提示见 [docs/user-guide.md](docs/user-guide.md)。

## 边界要求

选择房间时请注意：

- 一间房只能使用一组 `LINE`，或一个闭合的直线型多段线，不能混合选择；多段线首尾误差不超过 `3 mm` 时按闭合处理；
- 多段线必须是单一外环、共面、WCS 近似水平/竖直、无 bulge、无圆弧、无自交、无重复边、无洞；首尾间隙超过 `3 mm` 时仍需先修正；
- 仅支持直线型 `3D POLYLINE`；非直线型 3D 多段线、旋转边界、自定义 UCS、多外环和多房间混合输入仍不支持；
- 使用 `PL` 绘制房间时，建议结束命令前选择“闭合(C)”；如果图面中只留下不超过 `3 mm` 的首尾小间隙，插件会在计算副本中连接，不会修改原多段线；选择时点击多段线本身；
- 门洞两点必须位于同一段真实外墙边界上，不能跨墙角或选择房间内部共享边。

抹灰完成面如果发生偏移失败、自交、退化、面积不足或房间消失，程序会清除旧预览，不生成方案，也不写入任何对象。

## 暂不支持

- 旋转房间、任意斜边、旋转网格和自定义 UCS；
- 圆弧或带 bulge 的多段线、非直线型 `3D POLYLINE`、洞、多外环和自交边界；
- 柱、地漏、多房间通缝、墙砖和材料损耗优化；
- 自动修改、炸开、移动或删除原始 `LINE`/多段线。

## 原图保护

- 原始房间边界只读使用，不会被修改、移动、炸开或删除；
- 预览、取消、失败和未确认状态不写入正式对象；
- 正式结果写入专用图层，抹灰完成面厚度不为 `0` 时一并写入完成面轮廓；
- 自动尺寸标注写入独立图层 `TILE_LAYOUT_ORTHO_DIM`；取消勾选或未确认时不会创建该层或写入标注对象；
- 自动起铺点标志写入独立图层 `TILE_LAYOUT_ORTHO_START`；没有合适的整砖/半砖墙边位置时不生成标志；预览、取消或未确认时不会写入正式对象；
- 分割线、尺寸标注和抹灰边界按 UI 选择的 ACI 实体颜色写入；原始墙线不改色；
- 插件不自动保存或覆盖 DWG；
- 如需撤销本次写入，可执行一次 AutoCAD `U` 或 `UNDO`。

## 构建、测试与加载

在项目根目录执行：

```powershell
dotnet restore TileLayout.sln --locked-mode
dotnet msbuild src\TileLayout.Core\TileLayout.Core.csproj /t:Build /p:Configuration=Release /v:minimal
dotnet msbuild tests\TileLayout.Core.Tests\TileLayout.Core.Tests.csproj /t:Build /p:Configuration=Release /v:minimal
powershell -ExecutionPolicy Bypass -File .\tools\Invoke-CoreReflectionTests.ps1 -Configuration Release
```

AutoCAD 插件需要本机 `config\AutoCAD.Local.props` 提供 AutoCAD 2021 Managed 程序集位置；该文件不进入 Git。AutoCAD 正在运行并锁定标准输出目录时，使用隔离目录构建：

```powershell
dotnet msbuild src\TileLayout.AutoCAD\TileLayout.AutoCAD.csproj /t:Build /p:Configuration=Release /p:OutputPath=..\..\build\plugin\Release-verify\ /p:IntermediateOutputPath=..\..\build\obj\TileLayout.AutoCAD\Release-verify\ /v:minimal
```

在 AutoCAD 2021 中执行 `NETLOAD` 加载 `TileLayout.AutoCAD.dll`，然后执行 `TILEUI`。标准 `dotnet test` 在当前 .NET Framework 测试宿主上可能无法稳定发现测试，发布检查使用项目内反射运行器并记录实际通过数。
