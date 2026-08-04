# DOR8 正式写回验收记录

状态：DOR8-A 基础写回边界已冻结；项目绝对下限决策模式已于 2026-08-04 按用户实际使用反馈修订冻结，DOR8-B/C 已实现并完成自动验证；AutoCAD 2021 实机写回仍待使用 DWG 副本执行。

日期：初版 2026-08-03；项目规则模式修订 2026-08-04

## 已冻结并实现的边界

- 正式写回只读取当前候选原对象中的同一 `LayoutDrawingPlan`，按原顺序写入 `DivisionLines` 和 `Connections`。
- `NeutralConnections`、中性区域、房间/砖块轮廓、墙角诊断、窄砖标记和其他临时预览标记不写回。
- `AutomaticUsable` 可写回；`RequiresUserDecision` 只在界面显示人工复核提醒并经过最终确认后写回，不要求填写复核原因。
- 项目规则尚未完成决定时，`RequiresProjectPolicy`、淘汰、输入不可信、能力不支持和未完成当前确认的候选均拒绝写回；用户明确选择“按图面确认、不设置数值绝对下限”后，符合视觉确认门槛的 `RequiresProjectPolicy` 候选才进入专项提醒和最终确认链路。
- 专用图层为 `TILE_LAYOUT_ORTHO_CONFIRMED`，颜色索引为 3，线型为 `Continuous`；新建对象使用 ByLayer 属性。
- 同一专用图层允许多个房间；每条正式线使用 `TILE_ORTHO_ROOM` XData 记录 `West/East/South/North/Elevation` 房间范围。同一范围直接拒绝重复写回，不删除或覆盖既有对象；不同范围可以追加。目标图层已存在但为空时不修改既有图层属性；只有属性已为 ACI 3/`Continuous` 才继续，否则直接拒绝。DOR8 早期无归属标记的旧实体使用相同正式线几何兼容判重。
- 写回前显示一次最终确认提示。确认状态与 G3 的人工原因记录分离；取消、预览、切换候选、刷新、清除、未确认和失败均保持原图零对象写入。
- 正式写回使用单个 AutoCAD 数据库事务；提交前的任何异常由事务回滚。实现不自动保存 DWG，实机必须确认一次 `U/UNDO` 可移除本次全部正式对象。
- 写回成功后清除临时预览；写回失败保留预览、清除本次确认授权，并要求用户重新点击“确认并写入图纸”。
- 若同源计划没有 `DivisionLines` 或 `Connections`，不创建空目标层，直接提示无需写回。

## 自动验证

- Core Debug：`dotnet test tests\\TileLayout.Core.Tests\\TileLayout.Core.Tests.csproj --configuration Debug --no-restore`，316/316 通过。
- Core Release：`dotnet test tests\\TileLayout.Core.Tests\\TileLayout.Core.Tests.csproj --configuration Release --no-restore`，316/316 通过。
- Full solution Debug/Release：此前 Rebuild 均通过；最新数据库核验修复已用 `build/plugin-verify/dor8-writeback-keep-state/Debug` 和 `Release` 隔离输出完成 AutoCAD 适配编译。由于 AutoCAD 保留现场并锁定普通插件 DLL，本轮未结束进程或覆盖普通输出。
- 现有回归覆盖：只写 `DivisionLines + Connections`、排除中性连接线、`RequiresUserDecision` 无理由确认、默认“尚未决定”项目规则缺失拒绝、按图面确认候选可预览但不自动通过、写回授权必须显式允许视觉模式、确认独立于预览失败并可重新确认。
- 新增按钮状态回归：候选列表选中后可直接请求主预览；人工复核候选不需要 `DecisionRecord`；成功写回后再次预览/确认入口不被会话标记提前置灰，重复目标层仍由适配层直接拒绝。
- 针对确认后仍停留在预览的问题，正式写回通过 `SendStringToExecute("TILEORTHOUIWRITE")` 排入 AutoCAD 正式命令队列，并在命令内使用单事务；正式写回命令不使用 `NoHistory`，保留 AutoCAD 的撤销边界。预览改用 transient manager 登记的内存 `Line`，成功写回后显式擦除并刷新屏幕。该链路已通过 Debug/Release 编译，尚未替代 AutoCAD 2021 实机确认。

## DOR8-A 修订冻结：项目绝对下限决策模式（2026-08-04）

- 项目规则页必须明确记录三种互斥状态：已确认数值绝对下限、明确选择“按图面确认/不设置数值绝对下限”、尚未决定。未主动选择前，仍按项目规则缺失处理，不能通过候选选择绕过确认门。
- 已确认数值绝对下限时，低于该下限的候选继续按既有 G3 硬规则淘汰；算法、候选生成、推荐/绝对下限、墙角对缝优先、对侧完整整砖/准确对缝资格门均不改变。
- 用户明确选择“按图面确认/不设置数值绝对下限”后，有限、输入可信、能力支持且未被其他硬规则淘汰的候选可以进入人工视觉确认流程。该模式不把候选标为 `AutomaticUsable`，也不要求填写复核原因；界面必须显示实际最小边砖尺寸、位置、数量及“没有数值绝对下限，程序无法判断是否满足项目要求”的提醒。
- 上述视觉确认候选可使用同源 `LayoutDrawingPlan` 预览，并只能在用户看到专项提醒后点击最终“确认并写入图纸”才能写回。取消提醒、取消预览、切换候选、写回失败和未最终确认继续零写入。
- 输入不可信、能力不支持、绘图计划不完整、已被既有硬规则淘汰或项目规则仍处于“尚未决定”的候选，不因视觉确认模式放宽。

## DOR8-C 会话取消保护、入口和窗口体验

- 正式写回请求进入 AutoCAD command context 后，界面会暂时锁住取消、切换、重选、预览和再次写回入口；因此“取消整个任务”不会抢在事务提交前清空当前写回状态。
- 写回完成后，点击“取消整个任务”不会再发出临时预览清除请求，只清理当前向导状态；已提交的 `TILE_LAYOUT_ORTHO_CONFIRMED` 正式实体保留，并可继续选择其他房间；该按钮不会调用 `U/UNDO`。
- 正式 `Line` 写入先应用 AutoCAD 数据库默认属性，再明确设置专用图层和房间范围归属；同一事务内只遍历模型空间目标层一次完成房间范围判重，写入循环核验本次 `DivisionLines + Connections` 数量，未通过核验不会清除预览或显示成功提示。确认结果返回前，取消/切换入口保持锁定，文档激活事件不会清空待写回状态。
- 2026-08-04 用户确认同一图层允许多个房间，按房间范围判重。不同房间正式线可追加到同一目标层；同一房间再次确认会保留预览并拒绝写回。
- 新增短命令 `TILEUI`，旧 `TILEORTHOUI` 保留兼容；浮动窗口标题改为“自动排砖插件”。窗口移动期间暂停控件树重绘并启用双缓冲，以降低复杂 WinForms 控件在拖动中的重复绘制。若其他应用也同步出现窗口拖动延迟，仍需在 Windows 层检查显示驱动、DWM/硬件加速、远程桌面或系统负载。
- 新增写回后取消/重新排版会话回归；Core Debug/Release 自动测试均为 `316/316`。上述入口、窗口移动和正式对象保留仍需 AutoCAD 2021 DWG 副本实机确认。

## DOR8-B 性能与候选预览补充（2026-08-04）

- 选择方案页现在在选中 `AutomaticUsable` 或 `RequiresUserDecision` 候选后自动请求同源临时预览；明确选择“按图面确认”后选中 `RequiresProjectPolicy` 也会自动预览并显示专项提醒；“查看所选方案”保留为手动重看入口，项目规则尚未决定和淘汰候选仍不会自动预览或写回。
- 同一候选重复请求不再重建 `LayoutDrawingPlan`；候选列表、边界砖诊断列表和房间结构/候选搜索只读详情在输入或显示条件未改变时复用已生成内容。快速连续切换候选时，预览命令只保留最后一次显示/清除请求。
- 临时图形清除/重画不再每次强制完整 `Editor.Regen()`；仅当 AutoCAD 拒绝擦除某个 transient 对象时才使用 Regen 兜底，正常路径只更新屏幕。正式写回的重复判定只遍历模型空间目标层一次，写入数量由本次写入循环核验，不再重复扫描整张数据库。
- 上述优化不改变 G3 算法、候选状态、`LayoutDrawingPlan` 来源、预览零写入边界、正式对象边界或四个既有命令。Core Debug/Release 自动测试均为 `316/316`；AutoCAD 适配 Debug/Release 构建 0 警告、0 错误。
- 本轮隔离程序集：`build/plugin-verify/auto-preview-performance/Debug`、`Release`。Debug AutoCAD/Core SHA-256 为 `97BCB13D0A3687FBB0B5956DB7B80358A538C631BD5409369049BE76D53B9DB4` / `52C323BAA5CEF56E0D7F9F48E5D022C201AF903EFA1744B12FD34779AB812C4C`；Release 为 `74E00800C084C27C87FC83AD90C182E4C2C4E4A1B0AD735920577CCB73628624` / `0F659EE3281662A1E3D55DDC11690923F14A339550E645EB4B438926F7E9235A`。
- 仍需在 AutoCAD 2021 的 DWG 副本中实测自动预览、候选切换耗时、图面核对耗时和正式写回耗时；本地编译不能替代实机对 `UNDO`、SHA-256、跨房间追加和四个命令取消冒烟的验收。

## 静态写入边界审计

- 新 DOR8 路径只调用 `TryGetAuthorizedFormalLines`、`EnsureConfirmedLayoutLayer` 和 `WriteFormalDrawingLines`。
- 新路径没有调用候选计算器、重新构造 `LayoutDrawingPlan`、重算网格或重新生成候选。
- 旧 `TILE600`、`TILELAYOUT`、`TILEORTHO`、`TILEDOORRECT` 的既有写回路径未改动。
- 预览通过 AutoCAD transient manager 的内存 `Line` 显示并显式擦除；正式对象只在明确确认后的单一写回事务中创建。

## AutoCAD 2021 实机清单

必须在关闭旧 DLL 后，用 AutoCAD 2021 打开独立 DWG 副本；不得直接操作 `inputs` 原件或稳定 fixture 原件。

1. 将 `tests/fixtures/m4-real-room-sanitized.dwg` 复制到 `work/dor8-acceptance/dor8-writeback-copy.dwg`，记录副本初始文件大小、SHA-256 和 AutoCAD 版本。
2. `NETLOAD` `build/plugin/Debug/TileLayout.AutoCAD.dll`，执行 `TILEUI`（`TILEORTHOUI` 仍可用）。
3. 只显示预览，确认模型空间数据库没有新增正式对象；执行预览取消、清除、刷新、候选切换、关闭对话框和重新打开，确认 DWG 未变化。
4. 选择 `AutomaticUsable` 候选：第一次最终确认提示选择取消，确认零写入；再次点击确认并通过提示后，确认只生成正式分格线和必要连接边。
5. 检查正式对象全部位于 `TILE_LAYOUT_ORTHO_CONFIRMED`，图层颜色索引为 3、线型为 `Continuous`；确认没有中性连接线、诊断、窄砖或其他预览标记。
6. 写回成功后点击“取消整个任务”，确认正式对象仍保留；再执行 `TILEUI`，为另一房间完成一次排版，确认前一房间的正式对象不消失。
7. 确认原四条墙线和既有对象未修改。同一房间重复点击应直接拒绝，不删除、覆盖或追加，并保留预览、要求重新确认；选择空间不重叠的第二个房间时，应追加到同一目标层，并核对本次新增数量。
8. 选择 `RequiresUserDecision` 候选：看到人工复核提醒后可直接最终确认；界面不要求填写理由。项目规则仍处于“尚未决定”、淘汰、输入不可信、能力不支持或未完成复核的候选必须拒绝。
8a. 明确选择“按图面确认/不设置数值绝对下限”模式，选择一个未被其他硬规则淘汰的候选；确认候选详情显示实际最小边砖尺寸、位置和数量及专项提醒，预览与最终写回均消费同一 `LayoutDrawingPlan`，最终确认后按既有图层、事务、UNDO 和重复保护规则写回。
9. 使用锁定目标图层或其他可控失败条件验证事务回滚：不保留部分正式对象，预览保留，必须重新点击确认；确认图纸未自动保存。
10. 对成功写回执行一次 `U` 或 `UNDO`，确认本次全部正式对象一次性删除，墙线和既有对象仍保持不变。
11. 关闭图纸并选择“不保存”，重新计算副本 SHA-256，必须与第 1 步完全一致。
12. 分别对 `TILE600`、`TILELAYOUT`、`TILEORTHO`、`TILEDOORRECT` 做取消冒烟测试，确认取消不新增或修改对象。

实机每一步的实际结果、命令行提示、对象数量、SHA-256 和异常信息都必须记录后，才能将 DOR8 标记为完成；在此之前不得宣称实机写回验收通过。
