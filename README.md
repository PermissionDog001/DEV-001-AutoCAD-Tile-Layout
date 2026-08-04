# AutoCAD瓷砖自动排版插件

DEV-001 是一个面向 Autodesk AutoCAD 2021 的 C# Managed .NET 插件项目。

## 当前状态

当前工作基线已完成灰缝、闭合多段线、抹灰完成面以及界面和性能收口。用户已确认修复后的 AutoCAD 2021 实机测试完成；当前 GitHub 更新包含源码、测试和说明文档。版本号和现有 `dist` 发布包保持不变。

DOR9 当前边界：灰缝默认 `1.5 mm` 且允许 `0`；抹灰厚度默认 `0 mm`，正值按“原始边界 → 房间内完成面 → 完成面排版”计算；首期闭合多段线限定为 WCS 近正交、无 bulge、单外环的 LWPOLYLINE/二维 POLYLINE。UI 展示投影已改为结果变化时重建，候选/淘汰分页和需求文案不在普通刷新中重复计算；完整刷新、会话重置和列表更新增加批量绘制与异常复位保护。Core Debug/Release 自动测试均为 `333/333`；算法规则、版本号和 `dist` 发布包未改变。

M1 至 M5 均已完成。M4-01 至 M4-16 全部通过；M5 Release 重建和 27/27 自动测试通过，V0.1.0 最小包、说明和 SHA-256 已核验，AutoCAD 2021 发布包冒烟也已确认正式 DLL 加载成功、生成 23 条并可一次撤销。包中只有两个运行 DLL 和使用说明，未包含 Autodesk DLL、DWG、PDB、缓存或测试组件。未发现需要修改产品源码的可复现缺陷；V0.1.0 已发布到私有 GitHub Release，尚未公开发布。

V0.2 范围已于 2026-07-20 冻结：新增 `TILELAYOUT`，支持用户按次输入方砖或矩形砖的宽、高；继续使用四条 WCS 轴对齐 `LINE`、西南角起铺、灰缝 0 和直接截断规则。M6 参数化核心、M7 AutoCAD 适配、M8 自动质量门和 M9 AutoCAD 2021 脱敏 DWG 实机验收均已完成；Debug/Release 自动测试各 51/51 通过，方砖/矩形砖、宽高方向、取消/错误、图层属性、重复追加、超限拒绝、一次撤销、原墙线保护和不自动保存均已形成证据，未发现可复现产品缺陷。M10 发布检查尚未执行，版本号和发布物未变。

在 M9 工作树上启动的“工程起铺控制”阶段已完成：`TILELAYOUT` 在砖宽、砖高之后允许选择西南、东南、西北或东北起铺角，默认西南；切砖余量确定地落在所选角的对边。`TILE600` 继续固定西南角且无新增提示。Debug 56/56 自动测试、正式插件隔离编译和 AutoCAD 2021 脱敏副本最小实机验证均已通过。该阶段未启动 M10，未变更版本或发布物。

在 Git 基线 `6aa51d9` 上启动的“正交简单房间边界与裁切”已完成需求/接口冻结、核心算法、独立 `TILEORTHO` AutoCAD 适配、自动质量门、聚焦代码审查和 AutoCAD 2021 最小实机。新命令接受 4 条及以上同高程、WCS X/Y 轴对齐 `LINE` 组成的单一简单闭环，支持共线分段矩形和 L/U 形凹房间；网格锚点定义为区域 WCS 包围盒四角，候选线裁切为一个或多个室内片段。审查修复了大 WCS 坐标偏移下绝对坐标面积计算的消减问题；实机共线分段矩形、L 形凹角裁切、断口拒绝、一次撤销、原线保护和关闭不保存均通过。OR2 随后确认该通用契约也覆盖多个凸出/凹入、阶梯、狭通道和同线多片段情形；新增回归后 Debug/Release 自动测试各 170/170、完整解决方案双配置均已重建，AutoCAD 2021 连续实机步骤 1～10 全部通过。M10、版本和发布物保持不变。

门洞控制的正交矩形工程排版已完成 DR1 规则、DR2 宿主无关核心、DR3 AutoCAD 自动实现和 DR4 AutoCAD 2021 脱敏副本实机验收。新命令 `TILEDOORRECT` 只读选择四条矩形边界，按 WCS 输入门洞两端点，显示门洞/候选摘要和临时矢量预览，并提供接受、翻转、重选、取消；翻转只使用 DR2 已生成的居中等价候选。只有接受后才以单个写事务创建或复用 `TILE_LAYOUT_DOOR_RECT` 并写入分格线。锁定恢复、Debug/Release 完整解决方案构建和两套 116/116 自动测试均通过；DR4 的 12 项宿主清单全部通过，现有三个命令兼容冒烟通过，未发现需要修改代码的可复现产品缺陷。

DR5 门块/门图元辅助识别已完成首轮编码和自动质量门。`TILEDOORRECT` 的门洞第一点入口新增显式 `[对象(O)]`，默认仍为原两点输入；对象模式只读分析用户选择的模型空间顶层动态块当前可见 WCS `LINE/ARC`，只有唯一单扇平开门签名且原 `DoorOpeningPointAdapter` 通过时才进入原预览。任何歧义、不支持或点适配失败均报告原因并回退两点，对象选择 Esc 则取消命令。Debug/Release 完整解决方案隔离重建和两套 140/140 自动测试已通过。首次 AutoCAD 2021 验收确认 S1b-P2 和 S1c-P1 都是顶层静态块并被正确回退；用户随后确认现有图纸应当都没有动态块并撤回 S2/S3 的动态分类。当前真实动态正样本为 0，完整宿主验收暂停；静态块仍不自动接受。

用户已决定暂停动态块路线、等待未来真实样本，并把现有图纸需求转入下一独立任务“顶层静态门块辅助识别”。下一任务必须先完成静态块样本/API 属性复核和新拒绝矩阵，再决定如何复用现有几何核心；在此之前不直接移除 `StaticBlock` 门禁。

DR5-S 已完成 G3 样本门槛、受控产品接入和 AutoCAD 2021 连续实机验收：R2 的 S1/S2/S3 六正件均在合法四线矩形中唯一识别并由原 `DoorOpeningPointAdapter` 直通；R3 的 S1-a 真实多候选块正确返回 `Ambiguous (MultipleDistinctSignatures)`；R5 的 S2-b 补齐同一门几何的“非镜像 + 属性 A”和“非镜像 + 属性引用 0”正变体。顶层块、XREF、均匀缩放、唯一签名和原点适配门禁继续保留；动态算法及暂停状态、原两点输入、预览和接受后单事务写回均未改变。完整解决方案 Debug/Release 隔离重建和两套 163/163 自动测试通过；16 项宿主清单全部通过，接受生成 21 条、紧接一次 `U` 整体撤销，拒绝/取消零写入，四份测试副本及全部 intake DWG 哈希不变。

门洞控制的正交异形房间已完成第一轮规则发现：L-01、L-03、L-04 A～E、L-05和多凹凸P-01样例确认了整房单一相位与主次区组合两类候选、独立窄带与连续异形砖的区别、突出带优先吸收、中央整砖与临墙非整砖原则，以及墙砖对缝只作为有上限的整体美观因素。第一轮当时尚未冻结项目绝对下限；DOR7-G1 现已把它冻结为由项目明确给出的毫米阈值，并统一真实比较所有适用实际砖块。综合美观指标、数量/连续性/位置附加规则和墙砖缝 DWG 图元语义仍未冻结，因此不会伪造最终异形唯一择优，也不启动 M10。

DOR2 已完成复杂正交房间的宿主无关工程候选核心。它在单一、简单、无洞的 WCS 正交闭环内复用 DR2 轴带候选和 OR2 扫描线裁切，生成整房单一相位，以及由调用方明确给出主区、次区和连接边的分区候选；连接平行方向继承主区砖缝，垂直方向可在明确分界重置。核心会生成实际 `TileFootprint`，区分独立窄砖与连续 L/阶梯形切砖，并枚举突出带独立成砖或由相邻砖吸收；吸收后的单轴跨度不得超过一砖。L-04 D 和 L-05 中低于 `0.42T` 的人工证据仅作为“策略未决”候选保留，不猜测第二绝对下限；多个合法候选不计算总分、不伪造唯一答案。Debug/Release 完整解决方案均重建通过，自动测试各 187/187；未新增 AutoCAD 命令、预览或写回。

DOR3 已在 DOR2 之上补齐人机协同决策契约：`EngineeringOrthogonalDecisionResult` 以稳定顺序保存候选状态、原始诊断/指标和分层 `DecisionRequirement`，并把项目策略、房间语义、候选选择、研究/受控生产人工例外分开处理；DOR3 验收基线为 Debug/Release 195/195。

DOR4 已新增最小 `PaletteSet` 入口 `TILEORTHOUI`。它只显示 DOR3 的项目策略、房间语义、候选选择问题和候选原始详情；普通用户不能输入裸 X/Y 相位。只有 DOR3 明确的唯一自动候选可直接请求零写入预览；人工例外必须由上游带回匹配的 `DecisionRecord`，研究和受控生产均明确标注为非自动合规。DOR4 不创建图层、实体或写事务，不改变四个既有命令的流程。Debug/Release 完整解决方案重建和自动测试均为 201/201；2026-07-29 已在 AutoCAD 2021 从 E 盘 Debug DLL 完成最小面板冒烟，`TILEORTHOUI` 成功打开空状态 PaletteSet，命令行确认未载入房间结果且未生成对象。关闭重开、`U` 和测试副本前后 SHA-256 尚未形成独立记录；正式写回仍未接入。

DOR5 已把 `TILEORTHOUI` 接到受控、只读的复杂房输入流程：边界、项目策略/模式、控制区、门洞、房间意图、主次区、连接边和候选 `DecisionRecord` 均由用户明确提供，任何缺失项继续由真实 DOR3 `DecisionRequirement` 呈现；普通用户仍无裸 X/Y 相位入口。选择、取消、重选、修改语义、候选切换、重新计算和预览请求均无写回授权。完整解决方案 Debug/Release 隔离重建和两套自动回归均为 208/208。2026-07-29 用户已在 AutoCAD 2021 连续执行 DOR5 实机清单并确认唯一自动候选、策略/语义缺失、多候选、研究/受控生产 `DecisionRecord`、重选、无效边界、取消点和四个既有命令兼容冒烟全部符合预期；工作副本关闭不保存后仍为 38,893 字节，前后 SHA-256 均为 `B96AC3F50390E98E4E4037304E8197C98859AB9855DF263FAD382D1D0B8701DF`。DOR5 自动与宿主验收至此完成。

DOR6 已把 `TILEORTHOUI` 重构为六步中文引导式 Palette：普通用户在面板内完成房间、项目规则、铺贴方式、图面控制信息、候选比较/人工原因和只读汇总，不再记忆动作字母或手工输入 WCS 坐标。图面选择由面板按钮安全排队到 AutoCAD 命令上下文；候选分为“自动推荐”“需要人工确认”“不可使用”，缺失项、按钮禁用原因和下一步均使用普通中文，原始 DOR3 代码与诊断保留在工程详情。首次 AutoCAD 2021 实机发现“全部候选不可用”提示矛盾，并进一步确认脱敏图没有门边标记、使用整段北墙端点会被正确解释成整墙门洞；第三修复版进一步补齐主次连接边的相对方位引导。2026-07-30 用户在 AutoCAD 2021 完成 L-01 自动推荐、L-04 D 缺失策略、L-04 E 研究/受控生产人工记录、模式/策略/门洞失效、取消边界、无效输入、跨图恢复、整个任务取消和四个既有命令兼容验证，均符合预期；研究与受控生产人工决定始终标明“非自动合规”。Debug/Release 自动测试均为 221/221。关闭不保存后 DOR6 工作副本仍为 38,893 字节，前后 SHA-256 均为 `B96AC3F50390E98E4E4037304E8197C98859AB9855DF263FAD382D1D0B8701DF`；`U` 结果符合预期但逐字原文未留存。DOR6 自动与宿主验收至此完成。

DOR7 已完成编码前冻结、实现、自动质量门和 AutoCAD 2021 集中实机。`TILEORTHOUI` 已整理为“确定铺贴要求—在图中标明重点—比较排版方案—在图中预览并结束”四阶段产品界面；默认界面使用门洞影响范围、主要/相邻铺贴区、两区接合边和人工确认记录等普通术语，原始 DOR3 代码、候选 ID、诊断和指标只在默认折叠的“工程详情”中显示。Core 的唯一同源 `LayoutDrawingPlan` 同时驱动确定性 SVG、AutoCAD `DrawVector` 临时矢量预览和未来 DOR8 写回输入。预览支持显示、刷新、清除、返回修改、结束和跨图恢复，不创建或修改图层、实体或写事务。实机发现的自动方案提示、L-01 东/北/南门冻结恢复、唯一人工方案聚焦、确认前看图和最窄位置说明均已修复并复核；最终 Debug/Release 自动测试为 235/235，保护 DWG 哈希不变。

DOR7-G1“通用复杂正交房间候选与项目规则重构”已完成核心实现、自动质量门和 AutoCAD 2021 集中兼容复核。项目绝对下限现按毫米真实比较每个实际 `TileFootprint`：达到 `0.42T` 自动满足，达到项目绝对下限但低于推荐值时进入人工复核，低于项目绝对下限才淘汰，等号通过；旧的候选来源标志不再改变判定。每块砖均输出轮廓、边界侧、尺寸、连续异形属性、逐轴测量和原因。默认方案硬失败后，核心从正交顶点余数、余数间隙中点、推荐阈值和项目阈值接触点生成有限、可解释、去重的整房相位，并用冻结的 Pareto 指标筛选；每轴最多 64 个相位、最多 1024 个组合、最多保留 64 个非支配通用相位，截断状态显式输出。新增中性 N 矩形区域分解和共享边连接图，但不推断主区、次区或相位重置。来自 `复杂房间案例.dwg` 的 14 顶点精确边界已固定为测试夹具；原会话随机西墙门洞无法恢复，因此不伪造原 `183/44/3/7/121.606 mm` 会话断言，测试门洞由明确比例规则生成并覆盖变换。首次集中实机发现通用相位候选缺少旧 Palette 概况所需的 X/Y 轴带计划；修复后每个通用相位均提供完整轴带计划，概况格式化也能对不完整输入安全降级。定向复测不再弹出 JIT，候选切换、真实临时预览、刷新、清除、既有命令冒烟和零写入保护均符合预期。Debug/Release 完整解决方案和 248/248 测试均通过，Core Autodesk 引用、写入令牌和输出 Autodesk DLL 均为 0。G1 完成当时尚未启动 G2 或 DOR8；G2 的后续完成状态见下一段，DOR8 仍未启动。

DOR7-G2 第一轮技术门和 AutoCAD 2021 现有功能清单通过后，普通用户产品验收曾因手工拓扑和工程化界面重新打开。现已完成针对性产品修正：`TILEORTHOUI` 改为可缩放的 AutoCAD 风格浮动对话框，固定为“房间与规则—门洞—选择方案—图面核对”四页；普通用户只确认房间、门洞、推荐下限和项目绝对下限，不再看到“项目执行/方案研究”、`P-1`、门洞影响矩形、主要区、相邻区或接合边。门洞两点直接在完整正交房外边界验证，程序用 G1 中性分区唯一定位邻接区域，内部共享边不能冒充外墙，且默认全房连续相位；自动分区仅在图面核对页只读显示。候选按“满足规则/待项目复核/规则缺失”分视图，硬淘汰独立文字审计；多个完全合规方案可直接采用而无需编造原因，只有低于推荐值的项目复核方案要求原因。窄砖及自动分区图面开关集中到第 4 页，底部按钮固定等宽。Debug/Release 完整构建均为 0 警告、0 错误，自动测试均为 258/258，静态零写入门通过；新版浮动对话框和自动门洞链仍待一次集中 AutoCAD 2021 产品复核，因此 G2 尚不标记最终完成。DOR8 继续暂缓。

## V0.1 目标

用户执行正式命令 `TILE600`，选择组成矩形房间的四条墙线。插件识别房间西南角，以 600 × 600 mm、灰缝 0 mm 的固定规则，从西向东、从南向北生成地砖分格线，并写入 `TILE_LAYOUT_600` 专用图层。

## 普通用户快速开始

推荐执行 `TILEUI` 打开引导式窗口；旧名称 `TILEORTHOUI` 仍然兼容。按“选择房间 → 设置砖和灰缝 → 设置抹灰完成面 → 设置门洞 → 选择方案 → 图面核对 → 确认写入”的顺序操作。预览和取消不会写入图纸，只有最后确认后才会写入专用图层。

完整的更新说明见 [docs/release-notes.md](docs/release-notes.md)，逐步使用方法见 [docs/user-guide.md](docs/user-guide.md)。

## 支持范围

- Windows x64
- Autodesk AutoCAD 2021
- C# / .NET Framework 4.8
- 模型空间
- WCS 世界坐标
- 与 WCS X/Y 轴平行的四线矩形房间
- `TILEUI`/`TILEORTHOUI`：支持多条 `LINE`，或一个闭合的 `LWPOLYLINE`/传统二维 `POLYLINE`；支持当前正交凹多边形房间
- 灰缝：默认 `1.5 mm`，允许 `0`；砖墙边界灰缝按一半计算
- 抹灰完成面：默认 `0 mm`；正值向房间内部生成完成面后再计算排砖
- 独立 `TILEORTHO`：4 条及以上 WCS 正交 `LINE` 组成的单一、无洞、无自交简单房间
- 独立 `TILEDOORRECT`：四线 WCS 轴对齐矩形、参数化砖宽/高和同墙两点门洞
- `TILEDOORRECT` 显式对象辅助：模型空间顶层动态块当前可见状态中的唯一单扇平开门 `LINE/ARC` 签名
- 图纸单位：毫米

## 暂不支持

- 旋转矩形和自定义 UCS
- 任意斜边、旋转网格、未满足冻结单扇签名的门块、散门图元、双扇/无弧等非唯一门型、异形房间门洞产品接入、柱、地漏和多外环
- 居中、对称、窄砖优化和材料损耗优化
- 圆弧/bulge 多段线、`Polyline3d`、带洞或多外环边界、旋转边界和自定义 UCS
- 多房间通缝、墙砖和独立 EXE

## 安全原则

- 不修改或删除原四条墙线。
- 不修改、炸开或移动原始多段线。
- 所有排版结果写入专用新图层。
- 一次操作可以通过一次 AutoCAD `UNDO` 撤销。
- 插件不自动保存或覆盖 DWG。

## 开发入口

- 当前产品路线图（DOR7/DOR8、验收门与实机测试预算）：[docs/development-roadmap.md](docs/development-roadmap.md)
- DOR9-A 灰缝/多段线/抹灰完成面规则草案：[docs/dor9-grout-polyline-plaster.md](docs/dor9-grout-polyline-plaster.md)
- 已实现阶段总账（每阶段能力、测试和证据入口）：[docs/implementation-ledger.md](docs/implementation-ledger.md)
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
- 门洞控制的正交矩形工程排版规则与开发计划：[docs/door-controlled-rectangular-layout.md](docs/door-controlled-rectangular-layout.md)
- DR5 门块/门图元辅助识别规则、样本清单与编码门：[docs/door-entity-recognition-dr5.md](docs/door-entity-recognition-dr5.md)
- DR5-S 顶层静态门块 API 证据、规则、拒绝矩阵与分层样本门：[docs/door-static-block-recognition-dr5s.md](docs/door-static-block-recognition-dr5s.md)
- 门洞控制的正交异形房间样例、规则发现、DOR2/DOR3 核心和 DOR4 UI 验收：[docs/door-controlled-orthogonal-layout.md](docs/door-controlled-orthogonal-layout.md)
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

OR1 自动实现、聚焦代码审查和 81/81 测试已通过；OR2 又补充多凹凸、阶梯、狭通道、同线多片段、大 WCS 和复杂房消息回归，Debug/Release 完整套件均达到 170/170。AutoCAD 2021 连续实机步骤 1～10 全部通过；完整步骤及证据见 [docs/orthogonal-simple-room.md](docs/orthogonal-simple-room.md)。任意斜边、洞口/柱、旋转网格和多房间通缝继续暂缓。

## 门洞控制矩形核心与 AutoCAD 接入

`EngineeringRectangularLayoutCalculator` 是独立于 AutoCAD 的 DR2 入口。它接收轴对齐矩形、砖宽/高和门洞所在墙及沿墙范围，分别计算门洞法向与沿墙方向：合法自然余量保持不动；`0 < r < 0.42T` 且至少有一块整砖时重分配为 `0.5T + 中间整砖 + (0.5T + r)`；恰好 `0.42T` 和整除尺寸不触发重分配。四面门洞共用同一旋转/镜像规则，居中时使用固定默认方向并提供一个只翻转等价沿墙分配的候选。

`TILEDOORRECT` 的正式流程为“矩形边界 → 砖宽/砖高 → 门洞两点或显式门对象 → 门洞摘要 → 候选摘要与临时预览 → 接受/翻转/重选/取消”。临时预览不创建数据库实体；只有接受后才在一个写事务中创建或复用 `TILE_LAYOUT_DOOR_RECT` 并写入结果。DR4 两点路径 12/12 实机通过；DR5-S 顶层静态门块路径 16/16 实机通过，接受生成 21 条并可一次 `U` 撤销，拒绝/取消零写入，测试副本哈希不变。动态门块路线因真实正样本为 0 保持暂停。详细规则与证据见 [docs/door-controlled-rectangular-layout.md](docs/door-controlled-rectangular-layout.md)、[docs/door-entity-recognition-dr5.md](docs/door-entity-recognition-dr5.md) 和 [docs/door-static-block-recognition-dr5s.md](docs/door-static-block-recognition-dr5s.md)。

## 复杂正交房工程候选与只读决策面板

DOR2 在宿主无关核心中生成整房单相位和显式主次区候选，复用 DR2 轴带与 OR2 裁切，保留实际砖块、突出带独立/吸收拓扑、原始诊断和未加权指标。DOR3 在其上加入项目策略、房间语义、候选选择和 `DecisionRecord`，不会猜测缺失的控制区、门洞、主次区、连接边或未冻结权重。

DOR7-G2 继续使用 `TILEORTHOUI` 和真实 `EngineeringOrthogonalDecisionResult`，入口名称为“复杂房地砖排版”。宿主已由停靠 `PaletteSet` 改为可缩放的标准 WinForms 浮动对话框；四页只公开房间/砖规格、推荐下限、项目绝对下限、门洞、候选和图面核对。自动分解得到的中性区域、内部策略版本和 `WholeRoomSinglePhase` 仍进入原 DOR3/G1 请求，不在 UI 重算候选或隐藏原始事实。排版方案保持原始顺序；完全合规方案可直接采用，低于推荐值的方案必须填写项目复核原因，规则缺失方案只能诊断，硬淘汰只能文字审计。普通用户没有动作字母、裸 X/Y 相位、坐标输入、主次区、接合边或内部类型名入口。

DOR7 没有引入新的项目策略或正式写回。`LayoutDrawingPlanBuilder` 只投影真实 DOR3 候选；确定性 SVG 快照与 AutoCAD 临时矢量都读取同一计划。预览使用 `Editor.DrawVector` 和 `Regen`，支持显示、刷新、清除、返回修改、结束及跨图后明确刷新，不创建或修改 DWG 图层、实体或写事务；四个既有命令保持原流程。

L-01 实机发现两类已冻结候选漏生成。东墙整砖相位在凹口处只剩 76 mm 时，现会保留原淘汰候选并追加 X 向 `576/600/600/300` 的半砖/过渡砖候选，凹口有效宽度 376 mm；用户已确认该预览与算法一致。北墙或南墙门洞位于右半边时，现会把产生西侧 76 mm 的近门控制相位保留为淘汰项，并追加冻结的“西侧整砖、中间整砖、东侧 276 mm”候选。恢复只适用于完整房间真实边界上的局部门洞，不放宽整段墙误选和 L-04 不可信门洞边界。

集中实机步骤 1～20 已全部符合文档预期，关闭不保存后测试副本仍为 38,893 字节、SHA-256 `B96AC3F50390E98E4E4037304E8197C98859AB9855DF263FAD382D1D0B8701DF`。根据实机反馈补齐的唯一待确认方案自动聚焦、确认前“临时查看所选方案”、不可使用方案预览禁用及四侧/最窄位置说明，也已由用户完成最小定向复核并全部符合预期。完整解决方案 Debug/Release 自动回归为 235/235，Core 无 Autodesk 引用，预览写入令牌为 0，版本和 `dist` 均未改变。DOR7 至此完成；详细记录见 [docs/door-controlled-orthogonal-layout.md](docs/door-controlled-orthogonal-layout.md)，后续 DOR8 路线见 [docs/development-roadmap.md](docs/development-roadmap.md)。

DOR7-G2 第二次集中 AutoCAD 2021 产品复核的 11 项均由用户确认符合预期，自动分区、门洞归属、候选分组、项目复核原因门、同源窄砖诊断、跨图/Esc/取消、既有命令冒烟和 DWG 零写入成立。实机同时反馈浮动窗仍偏大、部分内容需放大才完整，以及图面选择时遮挡模型。随后完成 G2 UI 易用性修正：房间/门洞选择期间自动隐藏并在成功、Esc 或错误后恢复；默认窗口缩为 760 × 680、支持 DPI 自动缩放和单页滚动；顶部信息压缩；第 4 页把窄砖诊断与只读房间结构改为页签；新增可收成右上角小控制条的“专注查看图面”。该修正不改候选核心、同源计划或零写入边界，尚待一次短定向实机复测。

UI 修正版实机复测时又发现：西墙门洞虽然位于同一段真实外墙，但跨过程序自动分区线时会被错误拒绝。现已改为先验证同一真实外墙，再合并沿该墙连续覆盖门洞的中性区域；内部共享边仍不能充当门洞墙，跨真实凹角或覆盖不连续仍拒绝。Debug/Release 自动测试现为 259/259，G2 仍保持零写入；只需对原失败门洞做一次短复测，DOR8 未启动。

随后用户确认窗口缩小时第 3 页候选方案仍可能被压缩到不可读。现已为候选列表、候选详情和项目复核原因设置可读最低高度，并启用候选页滚动；窗口仍可缩放，内容不会因缩小而消失。该修正不改变候选事实、顺序、筛选、确认边界或零写入边界，Debug/Release 自动测试仍为 259/259。

用户进一步发现“临时查看所选方案”会被详情区域遮挡。现已把可保留方案页改为单一滚动视口，候选详情和项目复核原因留在滚动内容中，“临时查看所选方案”和“保存人工确认记录”移到固定页面操作栏；底部全局导航继续固定。该修正已通过三种窗口尺寸的本地布局检查，标准 Debug/Release 自动测试仍为 259/259，尚待一次 AutoCAD 定向复测。

## 当前阶段：DOR7-G3 候选质量与墙角—地砖缝对齐

G2 已完成当前用户流程、项目规则确认、多候选审计、窄砖诊断预览和 UI 可用性收口。下一任务进入 G3，目标是改善复杂正交房的候选质量，不启动 DOR8 写回。

G3 规则已冻结为：不做墙砖对缝；只评估墙体平面阴角/阳角与地砖分格缝的关系。房间中部的实际凹凸转角优先，双向网格交点为最高等级但不强求，单条地砖缝准确经过角点为主要优化目标；仅接近角点的结果只作诊断。相位候选须增加可审计的墙角锚定来源，沿用 G1 硬规则、推荐/绝对下限和原始指标，不添加审美总分，不改变 `TILE600`、`TILELAYOUT`、`TILEORTHO`、`TILEDOORRECT` 或正式写回边界。

G3-A 已进一步冻结可计算边界：只有房间轮廓 270° 反射角作为墙角对齐优化目标；90° 角只作只读诊断。有效命中必须来自实际裁切后的分格线，并从角点向室内延伸正长度。硬规则和“满足规则/待项目复核/规则缺失”分组先行，只在同组 Pareto 比较中分别最大化双向交点命中数和至少单轴准确命中数；二者不合成总分。非矩形房在启用“墙角对缝优先”时运行有上限的墙角锚定分支，去重时合并全部来源；准确阈值沿用 `GeometryTolerance.Coordinate`，其他结果只报告最近分格缝距离。

G3 首轮实现已完成自动门：候选现保存稳定墙角评估和结构化相位来源，同组 Pareto 纳入双向交点数/单缝命中数，候选摘要、生成报告、`LayoutDrawingPlan`、确定性 SVG 和 AutoCAD 临时矢量共用同一事实。第 4 页新增“墙角对缝（只读）”和可选图面标记，不会创建或修改 DWG 对象。Debug/Release 完整回归均为 268/268，正式写回仍待 DOR8 另行启动。

### 近似正交边界输入（G3 修复）

`TILEORTHOUI` 现在会先保留原始 LINE 并进行只读方向诊断。严格 WCS X/Y 轴边界继续沿用原验证；只有原验证因轻微方向偏差失败、且所有边分别满足固定角度上限 `0.05°`、最大端点修正上限 `3 mm` 和端点连接容差 `3 mm` 时，才建立临时正交计算副本。副本只进入房间验证、候选计算和同源预览，原始 LINE、既有命令和 DWG 均不修改；超出任一上限仍明确拒绝。

普通界面只显示“已建立只读正交计算副本”的简短提醒；勾选“显示工程详情”后可查看每条边的最近 WCS 轴、角度偏差、端点修正和阈值通过/超限状态。归一化房间的门洞两点匹配只在当前会话使用同一端点修正容差，不放宽其他命令的几何公差。Debug/Release 全量 MSTest 均为 282/282，Core 与 AutoCAD 适配隔离构建均为 0 警告、0 错误；本轮仍不启动 DOR8，也不改变版本、dist 或发布物。
## DOR7-G3 当前更新：可选“墙角对缝优先”

在 G3 候选质量实现中新增了一个默认关闭的“墙角对缝优先”选项。关闭时沿用 G1 的基础候选生成与原始顺序；如果既有门洞候选全部硬失败，仍会运行有上限的 G1 X/Y 基础相位搜索（包括半砖/过渡砖边界来源），但不启用墙角锚定优先，也不按墙角指标排序。已有候选仍保留墙角只读诊断。勾选后才额外运行有上限的墙角锚定分支，并对可保留候选按稳定规则推荐：先降低入口第一视觉范围内低于推荐下限的边界窄砖，再优先 270° 墙角的 2/3 安全对缝，最后才比较入口视觉盲区窄砖和既有边界指标。切换开关会重新计算候选并使旧预览/确认失效；低于项目绝对下限仍是硬淘汰，项目规则缺失和低于推荐值的状态边界不变，不计算审美总分。

安全对缝逐角检查竖缝两侧砖宽和横缝两侧砖长；两侧均达到 `2/3T`（等号通过）才计入安全双向/单缝指标，否则不奖励该对缝。每个目标角的跨度、是否安全及原因继续从 `TileAssessments`、`WallCornerAssessment` 和同源 `LayoutDrawingPlan` 展示，预览与诊断保持 AutoCAD 零写入。新增回归后 Debug/Release 均为 282/282；DOR8、四个既有命令和发布物仍未启动或改变。一次集中 AutoCAD 2021 产品复核仍需按路线图执行。

### G3 开关行为修复

复核发现原实现虽然读取了复选框，却始终运行墙角锚定候选分支，关闭时只取消排序，因此复杂房间看起来“开关都在按墙角算法”。现已将开关限定为 G3 墙角优先层：关闭时沿用 G1 基础候选和原始顺序；只有 G1 基础候选全部硬失败时，才运行 G1 的有上限 X/Y 相位搜索，其中明确包含“对向边半砖—中间整砖—门侧过渡砖”的两轴重分配尝试，并逐个实际 footprint 复核；勾选后才额外运行墙角锚定搜索并排序可保留候选。若复杂凹凸使该重分配仍低于项目绝对下限，候选继续硬淘汰，不把不合格预览伪装成可保留方案。切换会使旧预览和确认记录失效；已有候选的墙角诊断仍只读保留。新增回归覆盖关闭→开启→关闭的搜索状态、G1 重分配来源和顺序恢复。
## G3 关闭墙角优先时的诊断边界补充

当“墙角对缝优先（可选，默认关闭）”未勾选时，搜索摘要会明确显示沿用 G1 基础候选且未运行墙角锚定相位。候选详情中的墙角命中数仍保留，用于只读审计；它可能是自然网格偶然经过墙角的事实，不参与相位生成、候选排序或自动采用。Debug/Release Core 自动测试当前均为 285/285。

## G3 复杂凹口的边砖摘要修正

## G3 关闭可选质量优先时的完整外包络重分配与普通界面收口

当门洞控制矩形本身是自然余量、但完整房间最外包络在某一轴出现低于固定推荐下限的余量时，G1 恢复候选现在改用完整房间实际宽度/高度重新计算该轴的“半砖—中间整砖—过渡砖”分配，再用同一 LayoutDrawingPlan 预览；不再把控制区局部墙段的自然相位误当成整房方案。该修正不运行墙角锚定、不改变绝对下限硬淘汰，也不改变四个既有命令。

未勾选“墙角对缝优先”时，普通候选标题、状态、搜索统计和候选概况不再显示墙角对缝质量文字；原始角点事实仍可在工程详情和显式只读诊断中审计。勾选后才显示角点质量摘要和推荐排序信息。新增完整外包络窄余量与普通文字回归后，Debug/Release MSTest 均为 288/288；AutoCAD 适配 Debug/Release 隔离构建均 0 警告、0 错误，DOR8、版本、dist 和发布物均未改变。

本轮供实机复测的隔离程序集位于 build/plugin-verify/g3-final/Debug 与 build/plugin-verify/g3-final/Release；Debug 插件/Core SHA-256 分别为 9AABC5530CCFC2C3725B9C1B930F3F6050AB016F7A7DCFE893894B8AC7AEF0C9、66923028FEB8BA8B6BC0469AEBD635F1F2DA7015F5592C7491AFBD528C5060F1，Release 分别为 6F7AC96C63576FA9F8376CB90C9DC3950C9F839876F40142B66712DFDF90C6CB、6FDD78123FF87A61C7CDCE6D3FD059A6D9A578075D4FF7D98511CC386488A37C。

候选详情现在把两类数据明确分开：`完整房间最外包络实际边砖` 从实际 `TileFootprint.BoundarySides` 和砖块尺寸读取，西/东对应完整房间的最小/最大 WCS X 外墙，南/北对应最小/最大 WCS Y 外墙；`排版相位参考带` 仅说明门洞控制区（或全房相位）用于生成网格的起算带，不再冒充完整房间墙段。最窄位置也改为从实际边界砖反查墙面，凹口局部墙段不会再被错误标成西墙或东墙。新增凹口控制区回归后 Debug/Release Core 自动测试均为 286/286；本轮仍不启动 DOR8。

## G3 当前补充：墙角优先排序与 G1 阈值说明

- 勾选“墙角对缝优先”时，候选排序保持冻结的字典序：先按硬规则状态组，再按入口第一视觉范围内低于推荐下限的窄砖数量，再比较 270° 墙角的安全双向/单缝命中，最后才用 G1 门控半砖—整砖—过渡砖来源、入口盲区窄砖、边界复核量和原始序号作稳定次序。G1 来源是原始事实，不是审美总分，也不会越过绝对下限硬淘汰或项目规则缺失状态。
- 半砖—整砖—过渡砖只在对应轴自然余量低于固定推荐下限 `0.42T` 时触发；等号仍视为满足。截图中的 348、377.555、450.353、376.582 mm 均高于 600 mm 砖的 252 mm 推荐下限，因此方案 2/3 的墙角锚定替代相位不会被强制改成半砖重分配。候选概况现明确显示“墙角锚定替代相位”及该轴是否触发 G1，避免把两类相位混为一谈。
- G1 恢复候选仍以门洞对面墙半砖、门洞墙过渡砖为方向约束；若完整房间实际边界在该轴低于推荐下限，则两轴逐一重建并用同源 `LayoutDrawingPlan` 复核。若用户希望在余量已经达到推荐下限时也强制采用半砖—过渡砖，需要另行冻结一条新的项目规则，本轮不自行扩大行为。
- 本轮 Debug/Release Core 与宿主无关适配自动测试均为 290/290；AutoCAD 适配 Debug/Release 隔离构建 0 警告、0 错误，复测程序集位于 `build/plugin-verify/g3-g1-priority/Debug` 和 `Release`。DOR8、四个既有命令、版本、`dist`、发布物、标签和远端均未改变；仍需一次集中 AutoCAD 2021 只读复测。

### G3 本轮修正：门洞边界模式与完整轮廓裁切的分离

- 方案概况中的“完整房间最外包络实际边砖”只来自最终 `TileFootprint.BoundarySides` 和实际裁切尺寸；它回答的是“完整房间外墙上实际留下的边砖多宽”。“排版相位参考带”才是生成连续网格时使用的名义首带。凹口、凸角或其他轮廓裁切可以使两者不同，因此 348、377.555、450.353、376.582 mm 不能单凭数值被解释为半砖、整砖或过渡砖。
- G1 门洞边界模式现在独立于“墙角对缝优先”复选框：只要完整房间该轴存在可行的“半砖/整砖 + 对侧不低于推荐下限”模式，就加入有界候选搜索；自然余量已经达到推荐下限不再阻止该模式尝试。复选框只控制墙角锚定候选和墙角质量排序，不关闭 G1 模式。
- 门洞对向优先方向先生成，另一侧镜像方向以较低相位优先级保留，方便在复杂凹口裁切后比较；同组不以未经确认的总分淘汰这两类 G1 模式。若名义模式经过完整轮廓裁切产生低于项目绝对下限的独立边界切砖，该候选仍硬淘汰，并新增“门洞边界模式裁切后低于绝对下限”诊断；可保留的自然/其他安全候选继续显示。
- 候选说明会把“模式已生成但被完整轮廓硬淘汰”和“当前方案实际采用自然/墙角相位”分开。预览仍消费同一 `LayoutDrawingPlan`，本轮没有写入图层、实体、DWG 或 DOR8 事务。
- 本轮自动门：Debug/Release Core MSTest 均为 `296/296`；AutoCAD 适配 Debug/Release 隔离构建均为 0 警告、0 错误。隔离程序集 SHA-256：Debug `TileLayout.AutoCAD.dll` `93965493B55B8E49BE5B0817A15A6A78D9412CEE9312F169B4F233E0C5DCAFF8`、`TileLayout.Core.dll` `4C035272E91A0A8BF31294B296119FBF63621A7B69AF79A9D4438C532E5FA96B`；Release `TileLayout.AutoCAD.dll` `1873A0CBB9DB96E95A88BF03488461B3667C7C9108C4F329808422453FD12CED`、`TileLayout.Core.dll` `38CEEEBB3CEA01A73ED0ED7E3B692B8B96514FF5CB36E09D5E14BB963F1EF1C4`。本轮仍不提交、不删除既有资料、不改版本/`dist`/发布物，也未启动 AutoCAD 写回。
### G3 本轮修正：墙角优先排序与复杂房间大边砖淘汰

- 勾选“墙角对缝优先”时，候选排序现在先比较硬规则状态和入口视觉范围窄砖，再比较双向网格交点准确命中、单条地砖缝准确命中及安全双/单缝，之后才比较 G1 门洞边界模式。因而同一状态下无准确对缝的方案不能排在有准确对缝的方案之前；没有可命中的候选时才继续按 G1 模式和稳定原始顺序比较。未勾选时 G1 模式仍可搜索，墙角事实只读且不参与排序。
- 对四条边以上的复杂正交房间，按 X/Y 轴逐条检查最终 `TileFootprint` 的实际边界测量。若某轴存在大于半砖且小于整砖的边界砖，必须满足同轴目标墙角准确对缝、明确的 G1 半砖/过渡分配，或同轴存在达到推荐下限且不超过半砖的节材边砖；否则新增 `LargeBoundaryCutWithoutCornerOrSavingBand` 硬淘汰诊断，不再进入“满足规则”候选。等号沿用既有公差和推荐下限规则，矩形四线命令路径不变。
- 淘汰候选在 Palette 中新增独立分组“大于半砖且无对缝/节材用途”，详情同时显示轴、实际尺寸、半砖阈值和西/东/南/北位置；预览与图面诊断继续消费同源 `LayoutDrawingPlan`，淘汰候选不可确认、不可写回。显式确认的历史项目相位不因本新增门控被删除，继续按既有确认记录路径处理。
- 本轮 Debug/Release Core MSTest 均为 `298/298`；AutoCAD 适配 Debug/Release 隔离构建 0 警告、0 错误。新隔离程序集 SHA-256：Debug `TileLayout.AutoCAD.dll` `B1529BDB6EA8E80939BDB0F821CEFD81F0A6C69C16EDA3237945C84791935FC6`、`TileLayout.Core.dll` `C14745DF31657E7F08F123801967096DCE72FC2887E238BA3950944F59114C8B`；Release `TileLayout.AutoCAD.dll` `7293DDF669C5C3BA6755754538991DE8BA8E65382555F140106276538BCB006C`、`TileLayout.Core.dll` `364DDDD8DDF5A05E702FE36886766A31B1D67914C340F1CE9F78E3E856B2AE4A`。静态零写入检查通过；DOR8、版本、`dist`、发布物、标签、远端和四个既有命令均未改变。

### G3 本轮补充：对侧整砖/准确对缝才允许小于半砖节材边砖

- 对每个复杂房间的东西、南北轴分别检查：边界砖实际尺寸达到推荐下限且小于半砖时，只有对侧墙的全部实际边界砖都是完整 `TileFootprint.IsFullTile` 整砖，或对侧存在同轴准确命中目标墙角的地砖缝，才允许该节材边砖进入自动满足候选；推荐下限等号沿用该组合资格门。
- “整砖”现在按完整砖事实判断，而不再只看当前轴向名义跨度；角砖或连续异形砖即使某一轴跨度等于砖尺寸，也不能冒充完整整砖。推荐下限、半砖等号和坐标容差沿用 Core 既有规则，不新增用户字段。
- 不满足条件的候选保留原始 `TileAssessments`、墙角事实和同源预览，但进入“待项目复核”而非“满足规则”，并显示对侧墙、实际尺寸及缺少完整整砖/准确对缝的原因；不改变四个既有命令，不产生 DWG 写入。
- 新增 `SmallBoundaryCutWithoutOppositeFullOrSeam` 回归覆盖；本轮 Debug/Release Core MSTest 均为 `304/304`。隔离插件构建位于 `build/plugin-verify/g3-opposite-boundary/Debug` 与 `Release`：Debug AutoCAD/Core SHA-256 为 `3F65FE5527B686A09B252D924C6AACBFD3A6F224F5405B985BA467C56B702F3D` / `9AFA9073BF45ECE5322E485ED2BE9E0C7E7366936F20FC052D597E4A005734D1`，Release 为 `FFC66579C8B1FD926B340CAC0925BE9A4A64A8A79562CAA9B0D50B11E15CCB67` / `D524E16F4790AC31223BA52491C2A575D1840236E87701D49695D86CE9D5E019`。完成后仍需 AutoCAD 2021 只读集中复核，DOR8 正式写回继续暂停。
- 本轮复核修正：显式确认相位也必须经过该对侧资格门，不能绕过；推荐下限等号也纳入组合门控，新增等号回归、确认相位回归、自动满足分组回归和“完整整砖”判定，当前自动测试基线更新为 `304/304`，新插件需重新构建后再进行 AutoCAD 2021 只读复测。

### 下一任务：DOR8 确认方案正式写回

G3 算法与候选质量规则现已冻结，当前版本继续保持 AutoCAD 零写入。下一独立任务只实现“用户明确确认后的方案写回”：必须复用已确认候选的 `LayoutDrawingPlan`，不得在写回层重新计算或改变排版；淘汰候选、项目规则尚未决定的候选和未完成项目复核的候选不得写回。项目可明确选择“按图面确认/不设置数值绝对下限”，此时符合既有几何与能力边界的候选仍需专项提醒和最终确认后才能写回。写回前应先冻结专用图层、重复写回处理、事务/UNDO 边界和取消保护，再进行 AutoCAD 2021 实机验证。DOR8 完成前不改变 G3 算法、不改变四个既有命令、不修改版本或发布物。

### DOR8-A 写回规则冻结（2026-08-03；项目规则模式于 2026-08-04 修订）

- 正式写回只消费当前确认候选的同一 `LayoutDrawingPlan`，写入 `DivisionLines` 与 `Connections`；排除 `NeutralConnections`、中性区域、房间/砖块轮廓、墙角诊断、窄砖标记和其他预览辅助标记。
- 候选权限采用受控人工确认：`AutomaticUsable` 和用户已看到提醒并明确确认的 `RequiresUserDecision` 可写回；不要求填写复核原因。项目规则仍处于“尚未决定”时，`RequiresProjectPolicy`、淘汰、输入不可信、能力不支持和未完成当前确认的候选禁止写回。
- 项目规则页新增互斥决策语义：已确认数值绝对下限、明确选择“按图面确认/不设置数值绝对下限”、尚未决定。第二种模式不是自动放行；有限、输入可信、能力支持且未被其他既有硬规则淘汰的候选进入人工视觉确认，界面显示实际最小边砖事实和专项提醒，用户最终确认后才可写回。
- 专用图层固定为 `TILE_LAYOUT_ORTHO_CONFIRMED`，颜色索引 3，线型 `Continuous`，实体按图层属性绘制。同一图层允许多个房间；正式线携带房间范围归属，只有相同房间范围才拒绝重复写回，不覆盖、不删除既有对象，也不自动修改已有图层属性。
- 写回入口明确显示最终确认提示；写回使用单个原子写事务和一个 AutoCAD `UNDO` 边界，失败回滚且不自动保存。失败后保留当前预览，但必须重新点击确认；取消、切换候选、清除/刷新失效状态和预览均不写入 DWG。
- 本冻结不改变 G3 算法、候选状态计算、`TILE600`、`TILELAYOUT`、`TILEORTHO`、`TILEDOORRECT` 或版本/发布物。

### DOR8-B 实现状态（2026-08-03）

- 已实现“确认并写入图纸”入口、最终确认提示、`TILE_LAYOUT_ORTHO_CONFIRMED` 图层、按房间范围的重复写回保护、单事务回滚和失败后重新确认保护。
- 已实现只写同源 `LayoutDrawingPlan.DivisionLines + Connections`；诊断、中性连接线和临时预览标记不进入正式对象。
- 已补充空计划不创建空目标层、既有空目标层属性不静默修改的保护；属性不符合冻结值时直接拒绝并保留预览。
- 已修正方案选择后的预览状态同步；`AutomaticUsable`、提醒后的 `RequiresUserDecision`，以及明确选择“按图面确认”后的 `RequiresProjectPolicy` 均可直接进入零写入预览。成功写回后再次确认不在界面提前置灰，由正式线上的房间范围归属保护拒绝同一房间重复写回。
- 已修正“确认后仍停留在预览、未固定到图纸”的执行入口：正式写回通过 `SendStringToExecute("TILEORTHOUIWRITE")` 排入 AutoCAD 正式命令队列，并在命令内使用单事务写入；正式写回命令不使用 `NoHistory`，保留 AutoCAD 的撤销边界。预览由 transient manager 登记并在成功提交后明确擦除，再刷新屏幕。该修复已通过双配置编译，仍需 AutoCAD 2021 DWG 副本实机确认。
- 已加固正式写回与“取消整个任务”的边界：写回执行期间锁住取消、切换、重选、预览和再次写回入口；写回完成后取消不会再发出预览清除请求，只清理当前向导状态，已提交正式实体保留，可继续排版其他房间。
- 写回实体现在先调用 AutoCAD 数据库默认属性，再明确设置专用图层；同一事务内只遍历模型空间目标层一次完成房间范围判重，写入循环核验本次正式线数量，避免写回后再次扫描整张数据库。正式线同时记录房间范围归属。确认结果未返回前取消入口保持锁定，文档切换事件不会清空待写回状态。
- 用户已确认同一图层允许多个房间，按房间范围判重：不同房间可以追加到同一专用图层；同一房间再次写回仍直接拒绝。对 DOR8 早期版本已生成、没有房间归属标记的旧实体，适配层保留相同正式线几何的兼容判重，避免旧房间被重复追加。
- 新增短命令 `TILEUI`，旧 `TILEORTHOUI` 保留；浮动窗口标题改为“自动排砖插件”。窗口移动期间暂停复杂控件树重绘并启用双缓冲，以减少拖动时的重复绘制；其他应用也出现拖动延迟时仍需检查 Windows 显示驱动和系统层负载。
- Core Debug/Release 自动测试均为 `316/316`；最新视觉确认回归和多房间重复保护已完成，AutoCAD 适配 Debug 编译为 0 警告、0 错误。此前完整解决方案双配置 Rebuild 已通过；本轮未结束 AutoCAD 进程，也未覆盖或清理既有普通输出。
- AutoCAD 2021 DWG 副本实机写回、一次 `UNDO`、关闭不保存 SHA-256 和四个既有命令取消冒烟仍待执行，详见 [docs/dor8-formal-writeback.md](docs/dor8-formal-writeback.md)。

### DOR8-B 性能与自动预览补充（2026-08-04）

- 当前实现中，选择方案页选中 `AutomaticUsable` 或 `RequiresUserDecision` 后自动显示同源临时预览；明确选择“按图面确认”后选中 `RequiresProjectPolicy` 也会自动预览，并显示实际最小边砖事实和专项提醒；项目规则尚未决定和淘汰候选仍不自动预览或写回。
- 复用同一候选的 `LayoutDrawingPlan`，缓存未变化的候选列表、诊断列表和只读详情；快速切换时合并待执行的预览命令，避免重复清除/重画。
- 临时预览正常清除/重画不再强制完整 `Regen`；DOR8 重复保护只扫描模型空间目标层一次，写入数量按当前写入循环核验，减少大图纸上的重复数据库遍历。
- Core Debug/Release 自动测试均为 `316/316`；AutoCAD 适配 Debug 构建 0 警告、0 错误。AutoCAD 2021 DWG 副本实机耗时、自动预览、写回、UNDO、SHA-256 和四命令取消冒烟仍待执行。

### DOR9-B 发布前 UI、交互与性能收口（2026-08-04）

- 用户已确认 DOR9 修复后的 AutoCAD 2021 实机测试完成；本轮只收口向导界面、状态交互和展示性能，不改变 G3/DOR9 算法、四个既有命令、同源 `LayoutDrawingPlan` 或正式写回边界。
- 需求文案、候选分组、淘汰筛选/分页改为结果变化时重建并复用只读投影；WinForms 完整刷新、会话重置和列表更新采用批量布局/绘制，工程详情未展开时不生成全文，异常路径复位忙碌状态。
- Core Debug/Release 自动测试均为 `333/333`；Core 与 AutoCAD 适配 Debug/Release 独立构建均 0 警告、0 错误。静态写入边界复核和 working tree 盘点完成前，不修改版本号、`dist`、标签、远端或 GitHub Release。
