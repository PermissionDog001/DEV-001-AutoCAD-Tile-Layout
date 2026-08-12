# DEV-001 实现阶段总账

> 更新日期：2026-08-02
> 用途：快速回答“每一阶段已经实现什么、验证到哪里、详细证据在哪”

## 1. 使用说明

本总账是简明索引，不复制专项文档中的全部测试步骤、哈希和异常过程。未来工作见 [development-roadmap.md](development-roadmap.md)，完整累计项目记录见 [../PROJECT.md](../PROJECT.md)。

状态含义：

- **已完成**：该阶段冻结范围的自动门和必要宿主验收已通过。
- **部分完成/暂停**：已有可保留实现，但缺样本或后续决定，不能扩大能力宣称。
- **未开始**：尚未获得该阶段的实现与验收证据。

## 2. 基础矩形产品线

| 阶段 | 已实现内容 | 自动/构建证据 | AutoCAD 2021 证据 | 详细文档 |
|---|---|---|---|---|
| M0 | 项目目录、需求基线和 Git 初始化 | 基线文件建立 | 不适用 | [PROJECT.md](../PROJECT.md) |
| M1 | `NETLOAD` 技术探针、四线读取、尺寸/西南角、测试线写回和一次撤销 | 探针项目可编译 | 加载、选线、写线、一次撤销通过 | [technical-probe-m1.md](technical-probe-m1.md) |
| M2 | 宿主无关矩形验证与 600×600 网格核心 | 24 项自动测试；Core 无 Autodesk 引用 | 不需要 | [core-algorithm-m2.md](core-algorithm-m2.md) |
| M3 | `TILE600` 选择、验证、图层、事务写回与撤销 | 自动验证和正式插件构建通过 | 正式插件全项通过 | [autocad-integration-m3.md](autocad-integration-m3.md) |
| M4 | 脱敏真实 DWG、错误处理和原图保护矩阵 | 最终回归通过 | 16 项实机矩阵通过 | [dwg-acceptance-m4.md](dwg-acceptance-m4.md) |
| M5 | V0.1.0 最小包、说明和 SHA-256 | Release 27/27 | 交付包冒烟、23 条和一次撤销通过 | [release-v0.1.0.md](release-v0.1.0.md) |
| M6 | 可配置砖宽/高、有限数与正值验证、10,000 安全上限 | Debug/Release 各 48/48 | 由 M9 集中覆盖 | [core-parameterization-m6.md](core-parameterization-m6.md) |
| M7 | `TILELAYOUT` 参数输入、共享选择/事务路径和 `TILE_LAYOUT` | Debug/Release 各 51/51 | 由 M9 集中覆盖 | [autocad-adapter-m7.md](autocad-adapter-m7.md) |
| M8 | V0.2 自动质量门、锁定恢复和隔离构建 | Debug/Release 各 51/51；无 Autodesk DLL 复制 | 不重复实机 | [automated-quality-gate-m8.md](automated-quality-gate-m8.md) |
| M9 | 参数化矩形完整脱敏 DWG 验收 | 自动边界保留 | 数值、方向、取消、图层、超限、撤销和关闭不保存通过 | [dwg-acceptance-m9.md](dwg-acceptance-m9.md) |
| M10 | V0.2.0 发布检查 | **已完成** | **v0.2.0 正式包、SHA-256、标签和私有 GitHub Release 已核对** | [release-v0.2.0.md](release-v0.2.0.md) |
| SC1 | `TILELAYOUT` 的 SW/SE/NW/NE 工程起铺角控制 | Debug 56/56、隔离编译通过 | 最小实机通过 | [start-control.md](start-control.md) |

## 3. 正交简单房产品线

| 阶段 | 已实现内容 | 自动/构建证据 | AutoCAD 2021 证据 | 详细文档 |
|---|---|---|---|---|
| OR1 | `TILEORTHO`；4 条及以上 WCS 正交 `LINE` 的单一、简单、无洞闭环验证与多片段裁切 | Debug/Release 81/81 | 最小实机通过 | [orthogonal-simple-room.md](orthogonal-simple-room.md) |
| OR2 | 复核多凹凸、阶梯、狭通道、同线多片段、大 WCS 和拒绝矩阵 | Debug/Release 170/170 | 连续步骤 1～10 通过 | [orthogonal-simple-room.md](orthogonal-simple-room.md) |

## 4. 门洞控制矩形产品线

| 阶段 | 已实现内容 | 自动/构建证据 | AutoCAD 2021 证据 | 详细文档 |
|---|---|---|---|---|
| DR1 | 门洞控制、0.42T 下限、大小砖分配和交互计划冻结 | 规则与样例冻结 | 不适用 | [door-controlled-rectangular-layout.md](door-controlled-rectangular-layout.md) |
| DR2 | 宿主无关门洞矩形候选、默认/居中翻转、砖块分类和指标基座 | Debug/Release 97/97 | 不需要 | [door-controlled-rectangular-layout.md](door-controlled-rectangular-layout.md) |
| DR3 | `TILEDOORRECT` 两点门洞、摘要、临时预览、翻转和接受后单事务写回 | Debug/Release 116/116 | 由 DR4 集中覆盖 | [door-controlled-rectangular-layout.md](door-controlled-rectangular-layout.md) |
| DR4 | 两点门洞脱敏副本完整验收 | 自动回归保留 | 12/12；零写入拒绝、4 条写回、一次撤销通过 | [door-controlled-rectangular-layout.md](door-controlled-rectangular-layout.md) |
| DR5 | 动态门块辅助识别首轮实现 | Debug/Release 140/140 | 真实动态正样本为 0，路线暂停 | [door-entity-recognition-dr5.md](door-entity-recognition-dr5.md) |
| DR5-S | 顶层静态门块唯一签名识别和产品接入 | Debug/Release 163/163 | 16/16；接受 21 条、拒绝/取消零写入 | [door-static-block-recognition-dr5s.md](door-static-block-recognition-dr5s.md) |

## 5. 门洞控制复杂正交房产品线

| 阶段 | 已实现内容 | 自动/构建证据 | AutoCAD 2021 证据 | 详细文档 |
|---|---|---|---|---|
| DOR1 | L-01/L-03/L-04/L-05/P-01 规则发现；区分硬规则、原始指标和未冻结策略 | 规则结论形成 | 样例观察记录 | [door-controlled-orthogonal-layout.md](door-controlled-orthogonal-layout.md) |
| DOR2 | 整房/显式主次区候选、实际异形砖、突出带独立/吸收和 10,000 上限 | Debug/Release 187/187；Core 无 Autodesk 引用 | 不含产品接入 | [door-controlled-orthogonal-layout.md](door-controlled-orthogonal-layout.md) |
| DOR3 | 项目策略、房间语义、候选状态、分层缺失项和可审计 `DecisionRecord` | Debug/Release 195/195 | 不含产品接入 | [door-controlled-orthogonal-layout.md](door-controlled-orthogonal-layout.md) |
| DOR4 | `TILEORTHOUI` 最小只读 PaletteSet 和真实 DOR3 详情展示 | Debug/Release 201/201 | 空面板最小冒烟通过 | [door-controlled-orthogonal-layout.md](door-controlled-orthogonal-layout.md) |
| DOR5 | 真实输入绑定、决策展示、零写入预览请求和阶段性字母动作循环 | Debug/Release 208/208 | 完整功能清单通过；工作副本哈希不变 | [door-controlled-orthogonal-layout.md](door-controlled-orthogonal-layout.md) |
| DOR6 | 六步中文引导 Palette、图面选择桥接、候选分组、人工原因、记录失效和普通语言恢复动作 | Debug/Release 221/221 | 2026-07-30 完整清单通过；工作副本哈希不变 | [door-controlled-orthogonal-layout.md](door-controlled-orthogonal-layout.md) |
| DOR7 | 四阶段产品界面、同源绘图计划、确定性 SVG、零写入临时矢量预览、L-01 冻结恢复候选及确认前方案看图 | Debug/Release 235/235；Core Autodesk 引用和预览写入令牌均为 0 | 集中步骤 1～20 与末次 4 点定向复核全部通过；DWG 哈希不变 | [door-controlled-orthogonal-layout.md](door-controlled-orthogonal-layout.md) |
| DOR7-G1 | 通用项目边砖规则、实际砖块逐项诊断、有限全房相位、Pareto 保留和中性 N 区域接口 | 首次实机 JIT 缺陷修复后 Debug/Release 248/248；Core Autodesk 引用、写入令牌、输出 Autodesk DLL 均为 0 | 修复版候选切换/预览、原步骤 4～8 和零写入保护通过 | [door-controlled-orthogonal-layout.md](door-controlled-orthogonal-layout.md#29-dor7-g1-通用复杂正交房间候选与项目规则重构) |
| DOR7-G2 | 项目规则确认、候选分组/审计、窄砖诊断、自动邻接区域和四页浮动对话框 | 最终 Debug/Release 259/259；Core Autodesk 引用、G2 写入令牌、输出 Autodesk DLL 均为 0 | 集中 11 项产品复核、门洞误拒绝和窄窗布局定向修正均通过 | [door-controlled-orthogonal-layout.md](door-controlled-orthogonal-layout.md#30-dor7-g2-项目规则确认多候选比较与窄砖诊断预览编码前冻结) |
| DOR7-G3 | 270° 反射角锚定相位、实际分格线命中、同组 Pareto 取舍和同源只读墙角诊断 | Debug/Release 268/268；10 秒性能门、Core Autodesk 隔离、G3 写入令牌和输出 Autodesk DLL 均通过 | 待一次 AutoCAD 2021 集中只读复核 | [door-controlled-orthogonal-layout.md](door-controlled-orthogonal-layout.md#31-dor7-g3-候选质量与墙角地砖缝对齐) |
| DOR8 | 已预览方案的正式写回与一次撤销闭环 | **暂缓，等待另行启动与规则冻结** | 尚未安排 | [development-roadmap.md](development-roadmap.md) |

## 6. 当前能力边界

- 复杂正交房已能得到真实工程候选，完成普通用户可审计决策，并由同一宿主无关计划生成确定性 SVG 和 AutoCAD 零写入临时预览。DOR7-G2 产品修正已收口；DOR7-G3 自动门已通过、待集中宿主复核，正式写回仍未实现。
- 项目绝对下限已作为统一毫米阈值真实比较所有适用砖块；G3 只增加墙体平面 270° 反射角与地砖分格缝的客观命中指标。墙砖对缝、综合审美权重及其他未冻结策略仍不会被猜测。
- 任意斜边、旋转网格、内部孔洞/柱洞、多房间联排和自动区域猜测仍不在已实现范围。
- 动态门块辅助识别保留现有代码，但在获得真实正样本前不宣称完成。
- `TILE600`、`TILELAYOUT`、`TILEORTHO`、`TILEDOORRECT` 是已有独立流程；后续复杂房任务不得顺带改变它们。

## 7. DOR6 最终保护基线

- Debug 插件：112,128 字节，SHA-256 `018ED1FFA9EF5F54AE5ACE01A6D0104AA277B3367A690C2DBA4312396FA290EE`。
- Release 插件：104,960 字节，SHA-256 `26C64FF025F8CC3A75CD25874904E941A5BE9F8287308BE6B5D1FD101608459C`。
- DOR6 工作副本：38,893 字节，SHA-256 `B96AC3F50390E98E4E4037304E8197C98859AB9855DF263FAD382D1D0B8701DF`。
- 只读 fixture：SHA-256 `646A3A7A22CF40E5EC0B9CF8621A17AFAB09BB27928772C05D9CB3F4202DDA75`。
- 历史 `dist/TileLayout-0.1.0.zip`：12,531 字节，SHA-256 `322077112229CA8E0EDFB0CEE0B1F3F192A24EAC2CC23D11DCEA413CC3431141`。
- DOR6 未修改版本、`dist`、标签或远端，也未提交、推送或创建 PR；`U` 结果符合预期，但逐字命令行原文未留存。

## 8. DOR7 自动质量门基线

- 四阶段产品流程和术语已冻结并实现；默认界面不依赖内部类型名，原始 DOR3 顺序、代码、诊断、指标和 `DecisionRecord` 继续保留在折叠工程详情及内部模型中。
- `LayoutDrawingPlan` 是自动 SVG、AutoCAD 临时预览和未来 DOR8 正式写回的唯一几何来源。L-01/L-04 D/L-04 E 快照 SHA-256 分别为 `D01CC93E862BC6B969B12F76AF1F4F35460B15C08D17FA8766EE739A22742768`、`D6ED634149FEC17977B397E79FE826010CF7CCB3CFD8418E2B670809C1AE6046`、`2110ECB01E8FF54B5B39454BF3E12B824140A025118067928DBA322DB72E0237`。
- Debug/Release 完整解决方案构建成功，两套完整 MSTest 均为 229/229；Core Autodesk 引用数、DOR7 预览写入令牌和构建输出 Autodesk DLL 数均为 0。Debug/Release 插件 SHA-256 分别为 `CABFCB5F660F06DDD3CF3F439D3021A3CA2BE481DF45FFED13FAD6592EA85FD3`、`ADF96BB0BCDEA06C8ABB555EAAEBFD99F9E2F938186FA6EF82DDA0FAE16D2122`。
- DOR6 工作副本仍为 38,893 字节、SHA-256 `B96AC3F50390E98E4E4037304E8197C98859AB9855DF263FAD382D1D0B8701DF`；版本、`dist`、发布物、标签、远端及提交状态未改变。
- 本节记录自动质量门刚结束时的状态；随后集中实机、修复和末次定向复核已全部通过，最终状态见第 9 节。
- 首次 L-01 实机截图确认自动推荐和普通用户指标正确，但暴露自动方案人工原因误提示。`build/dor7-host-fix1` 已修复状态文案、原因区显隐和切换方案时的未保存原因清理；Debug/Release 仍为 229/229。修复版 Debug 插件为 122,880 字节、SHA-256 `5570C7962AA2B692492E9800E6E374DAEB97F93A9FA8BF4FAD7AD313B92174DF`，集中清单尚未完成。
- L-01 东墙门洞实机进一步暴露已冻结恢复候选漏生成：原 76 mm 相位被正确淘汰，但 300 mm 半砖/576 mm 过渡砖相位缺失。`build/dor7-host-fix2` 已恢复该分支并保留淘汰候选原顺序；X 向为 `576/600/600/300`，凹口 376 mm，全房最窄 276 mm。新增两项回归和一份同源 SVG，Debug/Release 完整测试均为 231/231；Debug/Release 插件 SHA-256 分别为 `C20EC30260297F2316FD96A42B53DFD7F99370CBDDA15937CE4B06113AD97AFC`、`1578DB1DF578003B5F669559EFDCF575F03EE25003CC39B6E2486CE213E31AF2`。实机继续暂停在最小东墙复测，最终 DWG 哈希待关闭不保存后读取。
- 用户确认东墙预览与算法一致后，又发现北/南墙右半边门洞仍因沿墙控制侧落在东侧而只产生西侧 76 mm 淘汰候选。`build/dor7-host-fix3` 已追加受真实边界局部门洞约束的沿墙控制侧恢复，得到 `600/600/600/276`，并保持整段墙误选与 L-04 不可信边界拒绝。新增两项回归和两份 SVG，Debug/Release 完整测试均为 233/233；Debug/Release 插件 SHA-256 分别为 `1F19BEF28121DB80B74B9691415C7B96BCD6FF6E33E683119A5FCD6E52B7B3F9`、`2E62D7AD9F48639AD1EB5434799DB25987314B86DB25FD63937A5B2BD2016F4A`。

## 9. DOR7 集中实机与末次交互修正

- 用户确认步骤 11～20 全部符合文档预期；结合此前记录，集中清单 1～20 已完成。关闭不保存后的测试副本仍为 38,893 字节、SHA-256 `B96AC3F50390E98E4E4037304E8197C98859AB9855DF263FAD382D1D0B8701DF`，对象/图层和既有命令零写入证据通过。
- 唯一待人工确认候选现在自动成为当前查看项，并明确引导用户先点击“临时查看所选方案”，再填写原因和保存确认记录。自动聚焦与临时查看均不创建 `DecisionRecord`，`HasWriteAuthorization` 始终为 `false`。
- 待人工确认候选可在记录前生成同一 `LayoutDrawingPlan` 并进入 AutoCAD `DrawVector` 临时预览；不可使用、输入不可信、能力不支持和仍缺规则的候选继续不能生成计划或预览。
- 普通概况新增四侧计划边砖和完整裁切后的最窄位置；无法由轴带证明属于某面外墙时明确写“异形转角或边界裁切处”，不猜测墙面。
- Debug/Release 完整构建和全量 MSTest 均为 235/235。Debug 插件 126,464 字节、SHA-256 `FCFF2C18AB7493C250588FB92BB87CA566AF23FCB274479EE62E1FA3E0919BA4`；Release 插件 118,272 字节、`03466FE59D2DF53472A67F81581A7AB3B733E7C41A33193A9B950943CA3856C0`。Core 两套哈希保持 `35EDEA57BF4EEDDC50C58FFA0DB7268445A8234EBA80706A0E393B58FD87EB04` 和 `164846C6ABB8354F79B2992AD4A6DAD7F03EF089EC12532139EF2BC828274D2B`；Autodesk 引用/复制和写入令牌均为 0。
- 历史 `dist` ZIP、程序集版本、标签和远端未改变，未提交、推送或创建 PR。用户已确认新增控件的最小定向实机复核全部符合预期，DOR7 完成；DOR8 尚未启动。

## 10. DOR7-G1 通用规则与候选生成自动质量门

- 用户冻结推荐下限、项目绝对下限、低于推荐值的复核状态、硬淘汰和阈值等号规则；实现按实际 `TileFootprint` 的适用方向真实比较，不再使用样例名称、候选来源或旧保留标志决定结果。所有 footprint 都有分类；连续异形砖以单个整体测量并保留轮廓诊断。
- 默认整房候选全部硬失败后，核心生成有限的顶点余数、间隙中点及阈值接触相位；每轴 64、组合 1,024、非支配保留 64 的上限和截断状态均为公开结果。只用冻结指标作 Pareto 支配，不能唯一决定时保留多个候选及原始原因。
- 新增无主次语义的确定性 N 矩形区域分解与共享边连接图，仅作为未来区域组合核心接口，不接 UI、命令或写回。
- `复杂房间案例.dwg` 的精确 14 顶点边界进入测试夹具，源文件 SHA-256 为 `C654BFB1A1D7C74B91DE97E5CCF80644DB7290DFC3BC52CA5E451A171C83FBDF`。随机西墙门洞无法恢复，旧会话的砖块计数和 `121.606 mm` 不作为夹具断言；测试门采用公开的确定性规则。
- G1 初版新增 11 项泛化回归；首次实机步骤 3 暴露通用相位没有 X/Y `BoundaryBandPlan`，Palette 选中方案时 `FormatCandidateOverview` 调用 `GetAxisPlan` 抛出未处理异常。修复为所有通用相位构造真实双轴计划，并让概况对不完整候选安全降级；新增 2 项回归直接覆盖复杂房全部候选概况。Debug/Release 完整解决方案和 MSTest 均为 248/248。修复版 Debug/Release Core SHA-256 分别为 `F31F2355A87A2BCF32C266D29C94F082D64AFAE4C3ACF3E48F061ECD2AA8D5A0`、`726029BC2A23A9D5DE0428637CA79ED232FFDEA8CA0A581C19DC14B6D5C90144`；Core Autodesk 引用、写入令牌和输出 Autodesk DLL 均为 0。
- 保护 DOR7 DWG 哈希仍为 `B96AC3F50390E98E4E4037304E8197C98859AB9855DF263FAD382D1D0B8701DF`，样例源 DWG 哈希也未改变。版本、`dist`、四个既有命令流程、发布物、标签和远端未修改；没有提交、推送或创建 PR。
- 用户加载修复版后确认不再出现 JIT：多个待确认/淘汰候选可逐项切换，方案 947 的真实绿色分格预览可显示、刷新和清除；阶段 4 汇总显示 1,009 个有上限的原始候选且没有人工确认记录。用户同时确认原步骤 4～8 全部符合预期，样例源 DWG、保护 DOR7 DWG 和历史 `dist` 哈希均不变。精确门洞 WCS 未逐字留存，实机截图不替代确定性自动夹具。DOR7-G1 完成；后续 DOR7-G2 第一轮技术实现已验证但产品验收重新打开，DOR8 仍未启动。

## 11. DOR7-G2 第一轮实现与产品验收重新打开

- G2 沿用四阶段产品外壳，在第一阶段接入固定推荐下限确认、项目规则版本和绝对下限门，在第三阶段接入满足规则/待复核/规则缺失三组候选、硬淘汰折叠筛选分页、窄砖清单及中性区域只读显示。视觉框架未重做，不代表 UI/交互未接入。
- 复杂夹具的 1,009 个原始候选完整保留；100 mm 规则下待复核 12、硬淘汰 997，硬淘汰默认折叠并分 20 页。按用户冻结，硬淘汰不能进入图面诊断、确认或写回。
- 用户确认 AutoCAD 2021 现有功能集中清单全部符合预期；第 7 步的“勾选图面显示”已澄清为“在图中显示中性区域和共享边”复选框。Debug/Release 末次重跑均为 253/253，G2 工作副本和保护 DWG 哈希不变。
- 随后的普通用户复核确认产品门仍未通过：中性区域只读分解没有替代手工主要区/相邻区/接合边和门洞影响矩形；Palette 仍暴露模式、版本和高密度工程输入，原因门与逐步问题解决路径也未达到目标。G2 重新打开，DOR8 未启动。

## 12. DOR7-G2 普通用户产品修正自动门

- 门洞两点现在直接匹配完整正交房的同一段真实外边界，并由 G1 中性矩形分解唯一定位邻接区域；内部共享边明确拒绝，凹角外墙按实际邻接矩形转换为 `DoorOpening`。自动输入仍忠实进入原 `RoomDecision`，默认 `WholeRoomSinglePhase`，不重算候选或推断主次语义。
- `TILEORTHOUI` 从停靠 `PaletteSet` 改为可缩放非模态浮动对话框；四页只显示房间与规则、门洞、选择方案和图面核对。普通界面不再暴露模式、`P-1`、控制矩形、主要区、相邻区或接合边；候选组与淘汰审计分开，窄砖/自动分区图面开关集中到第 4 页，底部按钮固定等宽。
- 多个完全合规候选允许无原因记录明确选择；低于推荐值的项目复核候选仍必须填写原因。规则缺失和硬淘汰边界不变，DOR8 写回权限仍不存在。
- 新增自动外墙/邻接区域、共享边拒绝、凹角墙侧、普通流程和无虚构原因 5 项回归。Debug/Release 完整构建均为 0 警告、0 错误，MSTest 均为 258/258；Core Autodesk 引用、G2 写入令牌和输出 Autodesk DLL 均为 0。
- Debug 插件/Core SHA-256 为 `9C2BFB5D875FB40EE786B882A8E94BF64662CEB6716C0E9B249F1D12BA5E2CBC`、`E7596D004D12B51B77685F363D525E264DA1FB941A7E0A3DFAC86444A18D97F7`；Release 为 `31994A909615BA729336353E3FA88C7FA3577AD1EDDFDA1787EA7088CF1B4F94`、`1550648D18913E1467197F5A5C690453EFEC4AA960DFCB4F947B16BC5791BF0B`。两个保护 DWG 哈希未变，版本与 `dist` 未变。
- 自动门已经通过；新版浮动对话框和门洞自动归属待一次集中 AutoCAD 2021 产品复核，G2 暂不标记最终完成，DOR8 未启动。

## 13. DOR7-G2 宿主通过与 UI 易用性修正

- 用户确认第二次集中 AutoCAD 2021 产品复核 11 项全部符合预期。核心产品流和 DWG 零写入通过；新增反馈是窗口偏大、部分页面需要拖大才完整，以及房间/门洞选择时窗口遮挡图面。
- 主窗口现在在房间或门洞选择期间自动隐藏，并在成功、Esc、错误或命令上下文失败后恢复；不会改变重选取消时保留旧值/旧预览的冻结语义。
- 默认窗口改为 760 × 680、最小 640 × 560，启用 DPI 自动缩放与系统消息字体；压缩顶部常驻信息，步骤页使用单一滚动面。第 4 页把窄砖诊断和只读房间结构拆为页签，并加入右上角小控制条形式的“专注查看图面”。
- 标准 Debug/Release 均 0 警告、0 错误及 258/258。Debug 插件/Core SHA-256 为 `52A7D7DA852628F0EE80D0985C096A934EFDD01356169DBC6F927897918FDD29`、`E7596D004D12B51B77685F363D525E264DA1FB941A7E0A3DFAC86444A18D97F7`；Release 为 `B21A2489DC3E0E071A07A7A1440FA6F859F41C13DB27C295D13CD996EFD04399`、`1550648D18913E1467197F5A5C690453EFEC4AA960DFCB4F947B16BC5791BF0B`。
- G2 UI/内部命令静态写入令牌、Core Autodesk 引用及输出 Autodesk DLL 均为 0；两个保护 DWG 哈希、版本和 `dist` 未变。只待 UI 定向复测，不启动 DOR8。

## 14. DOR7-G2 门洞跨内部自动分区线修复

- 用户在复杂房同一段西外墙上正确选择门洞，却持续收到“没有找到完整容纳门洞的自动邻接区域”。确定性复现表明门洞 `Y 3218.4086～3818.4086` 跨过内部自动分区线 `Y 3275.3494`；旧实现要求一个中性矩形完整包含门洞，因而误拒绝。
- 修复保持“同一真实外墙”前置验证；当单区域不满足时，只合并同墙侧且沿门洞方向连续覆盖的中性区域，并以共同室内范围生成控制矩形。内部共享边、真实凹角、不同外墙和不连续覆盖仍拒绝。
- 新增 1 项回归后，Debug/Release 完整构建 0 警告、0 错误，MSTest 均为 259/259。Debug 插件/Core SHA-256 为 `E42C72D58F607E90AFE68B0A2582020E457B013CD3842D0A03BBE75D57519CD0`、`E7596D004D12B51B77685F363D525E264DA1FB941A7E0A3DFAC86444A18D97F7`；Release 为 `FA5C114E4EE270593EAEBC924495FC7F32549822A58C0231B760AE6D94323B89`、`1550648D18913E1467197F5A5C690453EFEC4AA960DFCB4F947B16BC5791BF0B`。
- 两个保护 DWG 哈希、版本、`dist` 和零写入边界未变。只待同一门洞短定向宿主复测；DOR8 未启动。

## 15. DOR7-G2 候选页缩放安全布局修正

- 实机反馈显示，窗口缩小时第 3 页候选列表会因百分比行与固定详情/原因区域竞争高度而塌缩，导致方案看不到或看不全。
- UI 现在为候选组列表设置 128 px、候选详情设置 118 px、项目复核原因设置 64 px 的最低高度；候选页 `TableLayoutPanel` 开启滚动。小窗口下内容进入滚动范围，不从核心候选结果删除任何方案。
- 该修正不改变候选计算、原始顺序、分组/筛选/分页、诊断、确认条件或 G2 零写入边界。Debug/Release 完整构建均 0 警告、0 错误，MSTest 均为 259/259。
- Debug 插件/Core SHA-256 为 `576B1A82198619C62E8762899AF56A2FC6277479679236E7E59E95394447B9BD`、`E7596D004D12B51B77685F363D525E264DA1FB941A7E0A3DFAC86444A18D97F7`；Release 为 `7936A89240A09A655A8E01ACFDE8508430524576F15B636C213FD7CED47F6587`、`1550648D18913E1467197F5A5C690453EFEC4AA960DFCB4F947B16BC5791BF0B`。版本、`dist`、DWG 哈希和 DOR8 状态未变。

## 16. DOR7-G2 固定操作栏与单一滚动视口修正

- 用户截图显示“临时查看所选方案”被候选详情框遮挡。修复把可保留方案页拆成固定页面操作栏和唯一滚动视口；滚动区只承载候选列表、详情和项目复核原因，查看/确认按钮固定可见。
- `FlowLayoutPanel` 内容宽度随视口同步，候选列表、详情和原因框均保留最低尺寸；本地实例化检查在 640 × 560、760 × 680、1024 × 768 下均无详情/操作栏重叠。
- 候选计算、原始顺序、分组筛选、确认条件、预览同源性和零写入边界未变。Debug/Release 自动测试均为 259/259；Debug 插件/Core SHA-256 为 `69E99854AFA0E8FD3DC33B6D0340CA23857122AC74B3F6F0AAFB9079C7434B8E`、`E7596D004D12B51B77685F363D525E264DA1FB941A7E0A3DFAC86444A18D97F7`；Release 为 `4B2B45B0D7213094DC646C71338D62D4D00F241FF54FC6156B6448EB3055CD44`、`1550648D18913E1467197F5A5C690453EFEC4AA960DFCB4F947B16BC5791BF0B`。

## 17. DOR7-G2 规则收口与 DOR7-G3 启动边界

- 用户确认 G2 不考虑墙砖对缝；下一阶段只研究墙体平面阴角/阳角与地砖分格缝对齐。无法同时照顾全部角点时，优先房间中部的实际凹凸转角，不把门洞附近角点设为更高优先级。
- 双向网格交点重合为最高等级但非硬条件；单条地砖缝准确经过角点为主要优化目标；仅接近角点只作诊断。对齐判断沿用 Core 现有坐标公差，不新增普通用户容差字段。
- G3 将新增可审计的墙角锚定相位候选和对应指标，继续保留无法客观区分的候选及原始事实，沿用 G1 硬规则、推荐/绝对下限和零写入边界，不引入审美总分或墙砖输入。下一任务从 G3-A 规则/数据契约冻结开始；DOR8 未启动。

## 18. DOR7-G3-A 数据契约冻结

- 现状审计确认边界顶点相位已经存在，但 `KeyAlignmentCount` 尚未计算、相位去重不合并来源、Pareto/截断不认识墙角事实，`LayoutDrawingPlan` 也没有墙角诊断。编码前 Debug/Release 现有程序集均重跑 259/259。
- 用户确认四项规则：270° 反射角为优化目标、90° 角只读诊断；硬规则和候选状态分组先行，同组分别最大化双向交点数与至少单轴命中数且不设总分；启用“墙角对缝优先”时非矩形房运行有上限锚定分支并在去重时合并来源，关闭时沿用 G1 候选路径；准确使用现有 `1e-6` 公差，其他结果只保存最近距离。
- 样例源、G2 工作副本 SHA-256 保持 `C654BFB1A1D7C74B91DE97E5CCF80644DB7290DFC3BC52CA5E451A171C83FBDF`，保护 DOR7 DWG 保持 `B96AC3F50390E98E4E4037304E8197C98859AB9855DF263FAD382D1D0B8701DF`。G3-A 未修改产品代码，DOR8 未启动。

## 19. DOR7-G3 首轮实现与待宿主复核

- 已实现逐墙角分类/实际命中/最近距离、结构化锚定来源及合并、同状态组双指标 Pareto 和可审计生成/截断统计。没有墙砖输入、门洞附近加权、审美总分或硬规则越级。
- `LayoutDrawingPlan` 投影同一墙角事实；确定性 SVG、候选文字、“墙角对缝（只读）”页签和 AutoCAD 临时叉形标记共用该计划。图面开关默认关闭，切换只刷新临时矢量。
- 复杂夹具定向回归为 5 个目标反射角、X/Y 锚定相位 5/3、同角双轴/单轴组合 5/286、合并来源 9、1,009 个原始候选和 14 个非支配保留候选；约 1.6 秒，通过 10 秒安全门。
- Debug/Release 完整回归均 268/268，构建 0 警告、0 错误。Core 参考仅 `mscorlib/System.Core/System`；G3 只读适配/预览命令的写入令牌和两套输出 Autodesk DLL 均为 0。
- Debug 插件/Core SHA-256 为 `3E0BEED6EADD095827A6B5B74389F87273FAC4AE7CA5B3E6614EBA598016755C`、`1E2A1EDD078ED79BE1548F1BCD6E78CF688D5176393CA13ADC9654A7E9848EA1`；Release 为 `BC92CE8A31097F8B32B372D32B5A9AD2216AF57C351706AC7D888D8AC788C8C3`、`449B516E16DD4911AB06FA8829A9A03CCF9C0A9D5C12316B96917D5BBCB25FF2`。版本仍 `0.1.0.0`，样例/G2/DOR7 DWG 与历史 `dist` 哈希不变。
- 自动门已结束，待一次 AutoCAD 2021 集中只读复核。四个既有命令和 DOR8 边界不变；复核通过前不标记 G3 完成。

## 当前补充：G3 近似正交边界输入修复

- 现状问题：真实图纸中的边界 LINE 可能相对 WCS X/Y 轴只有很小角度偏差，原严格验证会直接报告“全部边界线必须与 WCS X/Y 轴平行”。
- 已实现：`OrthogonalBoundaryNormalizer` 对每条边输出最近轴、角度偏差和最大端点修正；仅在 `0.05°/3 mm/3 mm` 固定限制内建立只读正交副本，原始 LINE 不修改。端点无法安全合并、超限或非共面时保持拒绝。
- 接入范围：仅 `TILEORTHOUI` 的当前向导使用副本；门洞投影使用当前副本对应的局部匹配容差，其他命令和 Core 严格公差不变。工程详情逐条显示诊断，普通界面只显示归一化提醒。
- 验证：Debug/Release 全量 MSTest 均 281/281，Core 与 AutoCAD 适配隔离构建均 0 警告、0 错误；静态扫描近似正交引导路径未发现写入令牌。AutoCAD 实机集中只读复核仍待执行，DOR8 未启动。

## 20. DOR7-G3 可选墙角对缝优先与候选推荐

- 增加默认关闭的“墙角对缝优先”复选框；关闭沿用 G1 候选生成与原始顺序，仅保留已有候选的墙角只读诊断；开启才运行有上限的墙角锚定相位搜索，并排序可保留候选、标出推荐首选。排序保持硬规则/状态组边界，优先入口第一视觉范围无低于推荐下限窄砖，再比较安全墙角对缝、入口视觉盲区窄砖和既有指标。
- 目标 270° 墙角逐角检查竖缝两侧砖宽和横缝两侧砖长，均 `>= 2/3T`（等号通过）才计安全双向/单缝；不安全不奖励，绝对下限仍硬淘汰。原始跨度、标志和原因进入 `TileAssessments`、`WallCornerAssessment` 与同源 `LayoutDrawingPlan`。
- Debug/Release MSTest 均 282/282；Core/Adapter 隔离构建 0 警告、0 错误。AutoCAD 预览和诊断仍零写入，DOR8 未启动，待一次集中宿主复核。
- 复核修复：旧实现关闭复选框时仍运行墙角锚定分支，只取消排序；现关闭路径沿用 G1 候选生成与原始顺序，开启后才运行墙角锚定搜索和推荐排序，并以回归覆盖开关切换后的候选数量、搜索报告及顺序恢复。
## 21. G3 关闭墙角对缝时的 G1 门控相位回补

- 用户实测指出：关闭“墙角对缝优先”后，预览仍应遵循既定的门控排版规律；仅关闭墙角锚定优先级，不能连同 G1 的基础相位搜索一起关闭。
- 修复 `EngineeringOrthogonalLayoutCalculator`：关闭开关时沿用 G1 原始候选顺序；当基础候选全部触发硬淘汰时，仍运行有上限的 X/Y 全房相位搜索，并加入“门对面墙侧半砖—中间整砖—门侧过渡砖”的门控边界来源。两个轴分别计算、分别做实际房间轮廓验证。
- 对已有窄边恢复路径增加全房边界重分配尝试。只有所有实际 `TileAssessments` 满足绝对下限的候选才可保留；复杂凹角导致局部窄砖仍低于绝对下限时，候选保留在淘汰审计中，不会进入可预览/可确认方案。
- `CandidateGenerationReport` 新增 `PhaseSearchEnabled`，UI 文案明确区分“G1 基础相位搜索”和“G3 墙角锚定优先”，避免把开关关闭误解为完全不搜索相位。
- 新增两个核心回归：矩形房间两轴半砖/过渡砖规律、复杂正交轮廓两轴全房边界重分配与绝对下限硬淘汰。
- 验证：Debug/Release Core 自动测试均 284/284；Release 串行复跑同样为 284/284；Debug/Release AutoCAD 适配器构建均 0 警告、0 错误。并行构建期间的输出文件占用提示属于并行构建竞争，串行结果已确认；AutoCAD 实机复核待用户执行，DOR8 仍未启动。
## 22. G3 关闭状态墙角诊断与候选摘要澄清

- 实机截图中的关闭状态搜索摘要确认：本轮沿用 G1 基础候选，未运行有上限的 X/Y 相位组合搜索；因此没有执行墙角锚定相位或墙角排序。
- 候选详情仍显示“单缝准确命中”等墙角事实，这是 `WallCornerEvaluator` 对实际分格线的只读诊断，可能由 G1 自然网格偶然经过墙角产生，不参与相位生成、候选排序或自动采用。
- 摘要文案已明确写出上述边界；新增回归确保关闭开关时不产生 `TargetCornerAnchor` 来源。
- 本轮 Debug/Release Core 自动测试均 285/285；适配器因 AutoCAD 当前锁定标准 Debug 输出，改用 `build/plugin-verify/Debug` 隔离输出构建，0 警告、0 错误。AutoCAD 实机复核仍需关闭旧加载程序集后进行，DOR8 未启动。

## 23. G3 复杂凹口的实际外包络边砖摘要

## 24. G3 完整外包络窄余量与关闭状态普通文字

- 根因：门洞控制区可能给出满足推荐下限的自然余量，但完整房间外包络在另一轴实际需要半砖—中间整砖—过渡砖；原 G1 恢复门槛只看已测到的局部边界，导致首个预览继续沿用局部相位。
- 修正：在保持原始 G1 候选和顺序的前提下，恢复门检查完整房间宽/高；若完整外包络余量低于固定推荐下限且控制轴尚未重分配，追加经过实际 footprint 与项目绝对下限复核的房间边界重分配候选。该候选不使用 TargetCornerAnchor。
- UI：关闭“墙角对缝优先”时，普通候选标题、状态、搜索统计和候选概况不输出墙角质量文字；工程详情和明确打开的只读诊断仍保留原始事实。开启后恢复质量摘要和排序提示。
- Debug/Release MSTest 均为 288/288；AutoCAD 适配隔离 Debug/Release 构建均 0 警告、0 错误；DOR8 和 DWG 写回未启动。

- 根因：候选详情把门洞控制区/相位计划的低端和高端直接显示为“西墙/东墙”，在控制区落于进门凹口时会把局部墙段误称为完整房间墙面。
- 修正：摘要从实际 `TileFootprint.BoundarySides` 和 `NominalWidth/NominalHeight` 列出完整房间最外包络的西/东/南/北边砖；同侧多段不同值全部保留。原 `BoundaryBandPlan` 数值改标为仅用于生成相位的参考带，最窄位置也改从实际边界砖反查。
- 新增 L 形房间且控制区位于凹口的回归，证明控制区参考带与完整房间外墙边砖可以不同而不会混淆。Debug/Release Core MSTest 均为 286/286；Debug/Release 适配器隔离构建 0 警告、0 错误；AutoCAD/DWG 仍零写入，DOR8 未启动。

## 25. G3 墙角优先排序与 G1 阈值说明

- 勾选“墙角对缝优先”时，推荐顺序保持硬规则状态组和入口视觉窄砖优先；在此基础上比较 270° 墙角安全双向/单缝命中，G1 门控半砖—整砖—过渡砖来源仅作同组稳定次序，不形成审美总分或越过硬淘汰边界。
- 墙角锚定替代相位与 G1 重分配在候选概况中分开说明。只有对应轴自然余量 `< 0.42T` 才触发 G1 半砖—过渡砖；等号仍满足。348、377.555、450.353、376.582 mm 对 600 mm 砖均已达到 252 mm 推荐下限，因此不强制改排。
- 新增排序、G1 恢复优先和候选阈值解释回归；Debug/Release Core 与宿主无关适配测试均 290/290，AutoCAD 适配 Debug/Release 隔离构建 0 警告、0 错误。复测输出位于 `build/plugin-verify/g3-g1-priority`；DOR8、既有命令、版本、dist、发布物、标签、远端和 DWG 写入边界均不变。

## 26. G3 门洞边界模式裁切诊断与镜像比较

- 本轮更正第 25 节的旧门槛说明：复杂房间只要完整外包络存在可行的半砖或整砖首带、且对侧满足推荐下限，就加入 G1 门洞边界模式候选；自然余量达到推荐下限不再阻止该尝试。墙角优先复选框只负责墙角锚定和墙角质量排序。
- `BoundaryBandPlan` 的名义首带与 `TileFootprint.BoundarySides` 的实际外墙裁切值继续分开记录。截图中的 348、377.555 等数值属于后者，不代表模式首带本身。
- 每轴保留门洞对向优先模式和低优先级镜像模式；新增 `DoorControlledBoundaryPatternClippedBelowAbsoluteMinimum`，明确记录模式经完整凹凸轮廓裁切后低于项目绝对下限的轴、实际值和阈值。该候选硬淘汰，不进入可确认或写回。
- 新增 G3 回归覆盖：镜像模式可比较、复杂夹具模式裁切硬淘汰、候选说明与实际/参考带分离。Debug/Release Core MSTest 均为 `296/296`；隔离宿主构建均为 0 警告、0 错误。Debug/Release 插件 SHA-256 分别为 `93965493B55B8E49BE5B0817A15A6A78D9412CEE9312F169B4F233E0C5DCAFF8`、`1873A0CBB9DB96E95A88BF03488461B3667C7C9108C4F329808422453FD12CED`；Core 分别为 `4C035272E91A0A8BF31294B296119FBF63621A7B69AF79A9D4438C532E5FA96B`、`38CEEEBB3CEA01A73ED0ED7E3B692B8B96514FF5CB36E09D5E14BB963F1EF1C4`。保持 AutoCAD 零写入、DOR8 暂缓、四个既有命令及工作树既有改动不变。
## 27. G3 墙角优先排序与无用途大边砖淘汰（2026-08-03）

- 需求：勾选墙角优先时，准确墙角对缝必须先于无命中方案；复杂房间中某轴存在大于半砖的非整边砖且没有对缝/节材目的的候选不得留在满足规则组。
- 实现：`EngineeringOrthogonalDecisionCalculator` 将双向交点、单缝准确命中和安全双/单缝放在 G1 模式比较之前；`EngineeringOrthogonalLayoutCalculator` 对边数大于 4 的整房/主次候选逐轴检查实际 `BoundaryCutMeasurement`，以同轴准确对缝、G1 半砖/过渡来源或推荐下限至半砖的节材带作为允许理由，否则加入 `LargeBoundaryCutWithoutCornerOrSavingBand` Rejection。矩形命令路径不变。
- UI：`GuidedEliminatedGroup.UnjustifiedLargeBoundaryCut` 和中文筛选项“大于半砖且无对缝/节材用途”单独展示，候选详情保留轴、位置、实际值、半砖阈值及原始指标。
- 验证：Debug/Release Core MSTest `298/298`；AutoCAD 适配隔离 Debug/Release 0 警告、0 错误；静态零写入扫描无命中。DOR8、版本、dist、发布物、远端和既有四命令未改。

## 28. G3 对侧完整整砖/准确对缝节材边砖复核门（2026-08-03）

- 规则：复杂房间按 X/Y 轴逐侧检查 `推荐下限 < 实际边砖 < 半砖`。对侧同轴边界砖全部为完整 `TileFootprint.IsFullTile` 整砖，或对侧存在同轴准确目标墙角缝时放行；否则新增 `SmallBoundaryCutWithoutOppositeFullOrSeam`。
- 实现：Core 复用 `TileFootprint.IsFullTile`、`TileFootprint.BoundarySides`、`BoundaryCutMeasurement.RecommendedMinimum` 和 `WallCornerAssessment`。该诊断为 Warning，候选保留原始几何/指标但进入 `RequiresUserDecision`，不能列入满足规则自动候选；半砖/推荐下限等号沿用坐标公差。显式确认相位同样经过该资格门。
- UI：候选原因显示实际边砖、对侧墙和“缺少对侧完整整砖/准确对缝”，不新增用户参数；预览继续消费同源 `LayoutDrawingPlan`，AutoCAD 零写入。
- 回归：新增完整整砖放行、推荐下限等号组合、违规转项目复核、确认相位不得绕过及 UI 满足组隔离测试；Debug/Release Core MSTest 更新为 `304/304`。插件需重新隔离构建后再进行 AutoCAD 实机只读复核，DOR8、四个既有命令、版本和发布物未改变。

### DOR7-G3 收口 / DOR8 待启动

G3 当前基线为 Debug/Release Core `304/304`，算法与同源零写入预览冻结。下一任务仅增加确认方案写回，不改变候选计算；写回对象、专用图层、重复写回策略、事务/UNDO、失败回滚及实机验收清单需在 DOR8-A 先冻结。

### DOR8-A 冻结结果（2026-08-03；项目规则模式于 2026-08-04 修订）

- 写回授权：允许 `AutomaticUsable` 和用户在看到人工复核提醒后明确确认的 `RequiresUserDecision`；取消复核原因输入。项目规则仍处于“尚未决定”时，`RequiresProjectPolicy`、淘汰、输入不可信、能力不支持及未完成当前确认的候选仍禁止写回。
- 项目规则模式修订：项目页必须在“已确认数值绝对下限 / 按图面确认、不设置数值绝对下限 / 尚未决定”三种互斥状态中明确一种。第二种状态只允许有限、输入可信、能力支持且未被既有硬规则淘汰的候选进入人工视觉确认；需展示实际最小边砖事实和专项提醒，不能自动写回，必须最终确认。
- 写回对象：仅同一 `LayoutDrawingPlan` 的 `DivisionLines` 与 `Connections`；排除中性连接线、区域、墙角诊断、窄砖标记和所有临时预览对象。
- 图层/重复：`TILE_LAYOUT_ORTHO_CONFIRMED`，颜色索引 3，`Continuous`；同一图层允许多个房间，正式线通过 `TILE_ORTHO_ROOM` XData 记录房间范围，同范围拒绝、不同范围追加，不覆盖、不删除、不修正已有属性。
- 交互/保护：写回前最终确认；一个原子写事务和一个 AutoCAD `UNDO` 边界；失败回滚、不自动保存，保留预览但必须重新确认。取消、切换、刷新、清除和未确认状态零写入。
- 实现约束：不改变 G3 算法、候选状态、四个既有命令、版本、dist 或发布物；人工确认采用 DOR8 会话状态，不向核心 `DecisionRecord` 写入空原因。

### DOR8-B 实现记录（2026-08-03；按图面确认模式于 2026-08-04 完成）

- 新增正式写回策略和 AutoCAD 适配：只消费同源 `DivisionLines + Connections`，专用图层为 `TILE_LAYOUT_ORTHO_CONFIRMED`、ACI 3、`Continuous`。
- 默认“尚未决定”门控保持不变；明确选择“按图面确认/不设置数值绝对下限”后，未被既有硬规则淘汰且输入可信、能力支持的 `RequiresProjectPolicy` 候选可自动进入同源预览，显示最小边砖尺寸/位置/数量专项提醒，并只能在最终确认后写回。该候选仍不是 `AutomaticUsable`，也不要求填写复核原因。界面已移除复核原因输入。
- 同一房间范围重复写回直接拒绝；不同房间允许追加。写回单事务提交，异常回滚；失败保留预览并清除确认授权，需重新点击确认；不自动保存 DWG。早期无归属标记实体使用相同正式线几何兼容判重。
- 空计划不创建空目标层；已有但为空的目标层若不是 ACI 3/`Continuous` 也直接拒绝，避免静默修改既有图层属性。
- 修复候选选择后的 Palette 状态同步，新增人工复核候选的主预览状态；取消复核原因不再阻挡预览或最终确认。写回成功标记不再提前禁用后续入口，实际重复写回由房间范围归属保护拒绝。
- 针对“确认后仍显示预览”的反馈，正式写回改为通过 `SendStringToExecute("TILEORTHOUIWRITE")` 排入 AutoCAD 正式命令队列，并在命令内使用单事务；正式写回命令不使用 `NoHistory`，保留 AutoCAD 的撤销边界。预览改用可登记、可擦除的 transient `Line` 对象，并在成功提交后执行明确清除与屏幕刷新。该改动不改变正式对象边界和 G3 算法。
- Debug/Release Core 自动测试均 `316/316`；此前 Full solution Debug/Release Rebuild 通过，当前 AutoCAD 适配 Debug 编译为 0 警告、0 错误。AutoCAD 2021 实机验收待执行，清单见 [dor8-formal-writeback.md](dor8-formal-writeback.md)。

### DOR8-C 会话取消保护与用户入口优化（2026-08-03）

- 正式写回进入 AutoCAD command context 后，界面锁住取消、切换、重选、预览和再次写回入口，避免“取消整个任务”抢在事务完成前清空写回会话。
- 写回成功后点击“取消整个任务”不再发出临时预览清除请求，只清理当前向导状态；已提交的正式对象保留，不调用 `U/UNDO`，随后可使用 `TILEUI` 继续排版其他房间。
- 写回实体使用 `SetDatabaseDefaults` 后再设置专用图层；单事务内只遍历模型空间目标层一次完成房间范围判重，并核验当前写入循环数量，确认结果返回前保持取消/切换入口锁定，并忽略文档激活事件对待写回状态的清空。
- 用户已确认跨房间追加规则：同一目标层允许多个房间，按 `West/East/South/North/Elevation` 房间范围判重；同范围拒绝，不同范围追加。DOR8 早期未写归属标记的旧线使用相同正式线几何兼容判重。
- 新增短命令 `TILEUI`，保留 `TILEORTHOUI`；窗口标题改为“自动排砖插件”。自定义浮动窗体在交互移动期间暂停复杂控件树重绘并使用双缓冲，系统级窗口拖动延迟仍需单独排查 Windows 显示/桌面合成环境。
- 新增写回后取消、跨房间追加和按图面确认候选回归；DOR8 AutoCAD 2021 副本实机仍未执行。

### DOR9-A 规则冻结与首轮实现记录（2026-08-04）

- 用户已冻结三项规则：灰缝默认 `1.5 mm` 且有限非负，砖墙边灰缝为半宽并以灰缝中心做墙角对缝；首期只接受闭合、单外环、共面、WCS 近正交、无 bulge 的 LWPOLYLINE/二维 POLYLINE；抹灰默认 `0 mm`，正值从原始完成面外侧线向房间内部统一偏移。
- Core 计算顺序已固定为：读取/验证原始房间边界 → 确定性生成抹灰完成面 → 以完成面边界计算候选、门洞/控制区域语义和 `LayoutDrawingPlan`。偏移失败、退化、自交、面积不足或房间消失时清除/失效旧预览，不生成可用候选，不写回对象。
- 灰缝只增加网格节距，不改变名义砖尺寸；推荐下限和绝对下限不计灰缝占位；灰缝两侧边界线以及非零抹灰完成面轮廓均通过同源计划的 `DivisionLines` 表达，正式写回继续只消费 `DivisionLines + Connections`。
- UI 设置默认灰缝 `1.5 mm`、抹灰 `0 mm`；改变灰缝/抹灰或完成面相关输入会清除旧临时预览、清除确认状态并要求重新计算/确认。四个旧命令保留原有 `0 mm` 兼容行为。
- 自动验证：Core Debug/Release 测试均 `333/333`；Core Debug/Release 构建 0 警告、0 错误；AutoCAD Debug 构建 0 警告、0 错误；AutoCAD Release 在独立验证输出目录构建 0 警告、0 错误。新增凹多边形边界段同步回归、门洞交互点容差/原始边界到完成面映射回归，以及展示投影缓存回归；标准输出遵守当前 AutoCAD 进程占用边界，未停止进程、删除或清理任何文件。
- 用户已于 2026-08-04 确认 DOR9 修复后的 AutoCAD 2021 实机测试完成。仍待本轮发布前静态写回边界复核、working tree 盘点、拟暂存文件清单与用户确认；未获明确授权前不提交、不推送、不创建标签、不改版本号、不改 `dist`、不发布或同步 GitHub。

### DOR9-B 发布前 UI、交互与性能收口（2026-08-04）

- 向导展示投影改为结果变化时重建：需求文案、满足规则/待复核/规则缺失候选组、淘汰候选筛选与分页均复用只读缓存；候选顺序、状态、资格门、`LayoutDrawingPlan` 和 G3 算法不变。
- WinForms 向导完整刷新、会话重置和淘汰/诊断列表更新采用批量布局/批量绘制；工程详情未展开时不再生成工程详情全文；候选切换、取消任务和刷新异常均保证 `refreshing` 状态复位，避免界面卡在不可操作状态。
- Debug/Release Core 自动测试均 `333/333`；AutoCAD Debug/Release 独立验证构建均 0 警告、0 错误。当前仅记录为发布前收口，不改变四个既有命令、版本号、`dist`、标签、远端或 GitHub 状态。

## 69. DOR9-C 灰缝候选异常修复（2026-08-11）

- 用户反馈：灰缝 `1.5 mm`、抹灰 `0 mm` 时，方案页显示“不可使用（输入不能可靠验证）”，候选搜索显示尚未生成；灰缝 `0 mm`、抹灰 `15 mm` 时可正常生成方案。
- 根因：复杂正交房间的某个排版相位中，局部边砖在应用砖间/砖墙灰缝后无法形成有效实体砖体。该情况从 Core 候选构建层冒泡，被上层输入校验统一转换为“输入不能可靠验证”，提前终止了其它相位搜索。
- 修复：将灰缝导致的局部砖体消失限定为候选级硬淘汰，记录 `GroutTileBodyUnavailable` 诊断并继续搜索其它相位；主次区候选也采用同样的安全处理。淘汰候选保留诊断信息，但不进入可确认预览或正式写回。
- UI：淘汰候选显示灰缝下边砖没有足够空间保留实体砖体的明确原因，不再把该候选误报为输入不可信。
- 边界：未改变灰缝节距、名义砖尺寸、G3 硬规则、推荐/绝对下限、墙角对缝优先或对侧资格门；未改变同源 `LayoutDrawingPlan`、预览零写入和正式写回只消费 `DivisionLines + Connections` 的边界。四个旧命令、版本号、`dist`、标签和远端均未改。
- 验证：Core 与 AutoCAD 适配 Debug/Release 构建均 0 警告、0 错误；新增 2 条回归测试，直接调用全部 335 个 Core 测试方法/数据行均通过。当前环境的标准 VSTest 主机未能稳定发现/执行测试，指定 MSTest 适配器时发生主机栈溢出，因此该项不记为标准 `dotnet test` 通过。
- 用户已确认本次问题修复；本轮只补充追踪文档，不创建新版本发布物，不提交、不推送，下一任务继续继承当前 working tree。

## 70. V0.2.1 项目规则界面与建议下限比例（已完成；2026-08-11）

- 用户确认：引导式项目规则的建议下限比例默认 `0.5`，允许范围为 `0 < 比例 ≤ 0.75`，点击保存比例即视为确认；本轮只开发 UI 与规则参数贯穿，不启动自动标注和起铺点箭头。用户已完成 AutoCAD 2021 实机验收并确认本轮功能通过。
- UI：项目规则页默认选中“按图面确认”；比例输入下方明确说明 `T` 是对应方向的砖尺寸（X 用砖宽、Y 用砖高）。新增“最低允许尺寸（mm）/最低允许比例（T）”二选一，保留转角优先作为可选排序偏好，并继续移除普通界面的 G1/G2 开发术语和重复长说明。
- Core：引导式规则把建议比例传入矩形/复杂正交候选的推荐阈值、边界带和策略校验；项目最低允许比例按 X/Y 对应砖尺寸换算为毫米硬下限，与毫米下限互斥。比例等于 `0.75` 通过，`0`、负数或大于 `0.75` 拒绝；最低允许比例不能高于建议下限比例。通用旧构造器仍保留 `0.42` 兼容默认值，避免改变旧命令行为。
- 加载提示：正式插件程序集和 NETLOAD 引导提示更新为 `V0.2.1`，加载后引导用户输入 `TILEUI`；`TILEORTHOUI` 保留为兼容入口。
- 验证：Debug/Release 隔离完整解决方案构建通过，0 错误；NuGet 漏洞源不可访问产生 1 条 `NU1900` 警告。手动反射调用全部 339 个 Core 测试方法/数据行，339/339 通过；标准 `dotnet test` 当前仍报告没有可用测试发现器，不作为通过依据。用户已完成 AutoCAD 2021 实机验收并确认通过；未修改 `dist`、DWG、提交或远端。
- 任务收口：本轮 V0.2.1 项目规则界面与下限表达完成；下一任务再开发自动标注、非整砖尺寸标注和自动起铺点箭头。

## 自动尺寸标注（2026-08-11）

- 用户已冻结标注规则：大面通用标注只选房间内连续通长的代表性第一行、连续通宽的代表性第一列并逐块标注；特殊切砖、异形砖和特殊位置继续单独标注，每块特殊砖每个方向只取最长必要边，同一轴向同一显示尺寸只保留必要标注，并与通用标注去重；凹边、凸边、转角等房间台阶的长度/深度默认不标注，提供单独开关。标注值按实际边长四舍五入到整数、不带 `mm`；尺寸界线端点与砖边/房间边界重合。
- UI：引导式 UI 第 1 页新增默认勾选的“自动添加尺寸标注（建筑样式，默认勾选）”复选框，标注位置默认为房间内并可切换房间外；房间台阶标注默认关闭；提供 ACI 1–7 常用颜色选择，默认分割线/砖尺寸/凹凸特殊/抹灰边界为 ACI 3/2/6/4。关闭开关会清除旧预览并重新生成不含尺寸的同一候选方案；仅引导式 UI 生效，旧四命令不变。
- Core：新增 `LayoutDrawingDimension`/`LayoutDrawingDimensionBuilder`，尺寸数据进入同源 `LayoutDrawingPlan`；代表行/列按砖块横向/纵向连续覆盖范围选择通长/通宽的大面带并逐块生成，切砖/异形砖每块每轴只选最长代表边，测量段和重复显示尺寸去重并采用独立的整数显示值。
- Core：房间内通用行/列在满足连续通长/通宽的候选中优先选择最接近房间中心且不贴边界的砖边；无内部通长/通宽带时才回退到边界带，避免建筑斜短线遮挡阳角、墙角和对缝关系。
- AutoCAD：预览/写回使用标准 `RotatedDimension`，实体颜色消费同一份计划颜色设置，正式写回注入并使用独立 `TILE_LAYOUT_ANNOTATION` 标注样式，正式标注层为 `TILE_LAYOUT_ORTHO_DIM`；尺寸与分割线共用最终确认事务、房间范围判重和一次 `UNDO` 边界。
- 验证：Debug/Release Core、AutoCAD 适配和测试项目均已在隔离目录构建通过，手动反射执行当前全部 343 个 Core 测试数据行，Debug/Release 均为 `343/343`；标准 VSTest 主机仍因旧式 .NET Framework 测试宿主栈溢出而中止，不记为标准测试通过；用户已完成 AutoCAD 2021 实机复核，确认通长/通宽尺寸链、中心位置、重叠控制、颜色和撤销效果达到预期。未修改 `dist`、版本、提交或远端。
- 收口与交接：自动尺寸标注任务完成，下一任务为自动设置起铺点；当前 working tree、同源 `LayoutDrawingPlan`、预览零写入和正式写回边界继续作为后续开发基线。

## 72. 自动起铺点标志收口（2026-08-12）

- 规则冻结：起铺点位于远离门口墙面的首排/首列、贴墙整砖或半砖对应的四块砖交界灰缝中心；非零灰缝使用实际灰缝中心，不使用瓷砖中心或任意墙边中点。
- 方向冻结：向内箭头由远墙指向房间内部；沿墙箭头指向实际铺贴大方向。方向由门洞对侧、候选相位和砖方向计算，不能依赖屏幕方向。
- Core 新增 `LayoutDrawingStartPoint`/`LayoutDrawingStartPointBuilder`；AutoCAD 新增 `OrthogonalLayoutStartPointEntityFactory`。预览为临时对象，正式写回与同源 `LayoutDrawingPlan` 的分格线、尺寸标注共用事务和一次 `UNDO`。
- 正式图层为 `TILE_LAYOUT_ORTHO_START`，ACI 3；标志由圆、十字、两个三线开放箭头和“起铺点”文字组成。无合格整砖/半砖位置时不生成标志。旧命令不变。
- 验证：Debug/Release Core 反射回归各 `346/346`；Core、测试项目和 AutoCAD 适配项目构建通过；用户已确认 AutoCAD 2021 实机验证达到预期效果。

## 73. V0.2.1 发布完成（2026-08-12）

- V0.2.1 功能范围包括项目规则比例 UI、DOR9-C 灰缝候选恢复、自动尺寸标注和自动起铺点标志；程序集版本为 `0.2.1.0`。
- 已准备本地最小发布包 `dist/TileLayout-0.2.1.zip`、包内说明和 SHA-256 清单，发布明细见 [release-v0.2.1.md](release-v0.2.1.md)。历史版本包未覆盖。
- 已固化 `tools/Invoke-CoreReflectionTests.ps1`，用于 Debug/Release 的可复现 Core 回归；标准 .NET Framework VSTest 宿主发现限制按已知限制记录。
- V0.2.1 面向用户的更新说明已清理，本地包已发布；`origin/main`、`v0.2.1` 标签和 GitHub Release 均已完成。本次不创建 PR。

## 74. V0.2.1 发布内容维护：贴墙边界线贯通（2026-08-12）

- 用户反馈：抹灰完成面旁的瓷砖边界线按砖尺寸切成多个线段，整体删除和调整不便。
- Core 修复：`LayoutDrawingPlanBuilder` 只对 `BuildWallGroutBoundaries` 生成的贴墙灰缝边界做归一化合并；同方向、同固定坐标、同标高且线段间隙不超过灰缝宽度时合并为一条贯通线。内部 `candidate.DivisionLines` 不参与该合并，旧命令仍保持原行为。
- 回归：`DOR9WallGroutBoundariesAreMergedIntoContinuousEdges` 覆盖四条矩形贴墙边界，Debug/Release 反射执行全部 `347/347`；Core、测试项目和 AutoCAD 适配项目 Debug/Release 构建通过。
- 交付：更新 V0.2.1 发布说明、包内使用说明、交付目录说明、压缩包和 SHA-256 清单；仅维护现有 V0.2.1 发布内容，不改变程序集版本、标签或版本号。

## 75. 多段线首尾微间隙读取修复（2026-08-12）

- 样本 `Drawing1.dwg` 的唯一模型空间图元为 `AcDbPolyline`，`Closed=False`；首尾顶点二维间距约 `0.108 mm`。旧适配层只按 `1e-6 mm` 坐标公差判断未闭合多段线，因此在读取阶段失败；炸开后的 LINE 则由 `OrthogonalBoundaryNormalizer` 使用 `3 mm` 端点连接容差处理。
- 适配修复：`TryReadPolylineVertices` 对 LWPOLYLINE/Polyline2d 复用 `GeometryTolerance.NearOrthogonalEndpointJoinTolerance` 判断开放端点闭合资格；`GuidedBoundaryPolylineConverter` 在明确标记为开放端点修复时移除近重复末端，再按原有顶点顺序生成闭环。正式闭合多段线仍只按原有确定性重复顶点规则处理，避免吞掉合法短边。
- 回归：`PolylineConverter_ClosesSmallOpenEndpointGapLikeLineInput` 使用样本坐标覆盖 `0.108 mm` 首尾间隙；Debug/Release 反射回归 `348/348`，Core/测试/AutoCAD 适配 Debug/Release 构建通过。
- 宿主验证：样本只读检查后 DWG SHA-256 未变化；尚未在 AutoCAD 2021 中重新 NETLOAD 并执行 `TILEUI` 选择验证，当前不宣称实机完成。

## 复杂房间起铺点与其他多段线兼容修复（进行中；2026-08-12）

- 用户澄清：前述 `Drawing1.dwg` 只验证了轻量多段线 `0.108 mm` 首尾微间隙；本轮“其他案例偶尔无法选择”的问题不发生在该样本中，不能把 `3 mm` 作为唯一根因。
- 起铺点：四砖交界搜索先检查实际铺贴起始侧，再检查同一远墙的另一端；继续保留贴墙整砖/半砖资格、四砖灰缝中心和实际铺贴方向箭头规则，复杂凹边/台阶不再因为单侧无交界而直接丢失标志。
- 多段线：适配层新增直线型 `3D POLYLINE` 顶点读取；非直线型 3D 多段线以及圆弧/bulge、非共面、自交、多环和超过 `3 mm` 的首尾间隙继续拒绝。原始实体只读，未增加炸开或修改行为。
- 诊断：超过首尾误差上限时输出实测间距，便于下一次实机复现区分容差超限、实体类型和边界拓扑问题；当前不扩大 `3 mm` 安全上限。
- 自动验证：Debug/Release 反射回归均为 `350/350`，完整解决方案构建 `0` 警告、`0` 错误。尚未对用户尚未提供的其他失败案例完成 AutoCAD 2021 宿主复核，当前不宣称实机验收完成。
- 交付：本轮用户可见更新已合并到现有 `V0.2.1` 发布说明、使用说明和 `dist/TileLayout-0.2.1.zip`；新包 SHA-256 为 `828E3A47F9D888F1160BF89887D4DDAD0A03AFA44E52D5930754F090FC54D2EA`，不创建新版本。
