# ADR-0001：采用 AutoCAD 2021 Managed .NET 插件路线

- 状态：已接受
- 日期：2026-07-19

## 背景

项目需要直接读取和修改 DWG 中的图元。备选路线包括：AutoCAD 插件、完全独立 EXE 加 DWG SDK、DXF 转换原型。

第一版只验证四线矩形房间的 600 mm 地砖分格，不需要开发独立 CAD 查看器。用户环境已有 Autodesk AutoCAD 2021。

## 决策

采用 C# 编写 AutoCAD 2021 Managed .NET 插件，目标框架为 .NET Framework 4.8，平台为 Windows x64。

架构分为：

- 与 AutoCAD 无关的 `TileLayout.Core`；
- 依赖 AutoCAD 2021 API 的 `TileLayout.AutoCAD`；
- 不启动 AutoCAD 即可运行的 `TileLayout.Core.Tests`。

先实现最小 `NETLOAD` 技术探针，再创建完整排版功能。

## 原因

- 直接复用 AutoCAD 的 DWG 数据库、对象选择、图层、事务和撤销能力。
- 避免第一版承担独立 DWG SDK 的授权和查看器开发成本。
- C# Managed .NET API 适合快速验证并便于测试核心算法。
- 核心与宿主分离后，未来仍可新增其他 CAD 适配层。

## 后果

### 正面

- V0.1 技术范围更小，能够优先验证业务规则。
- 输出可直接显示在用户现有 AutoCAD 工作流中。
- 核心算法可单元测试并为未来扩展复用。

### 负面

- 插件依赖 AutoCAD 2021 和对应 .NET Framework/API。
- 不同 AutoCAD 版本可能需要重新编译或验证。
- 用户必须允许本地插件加载和可信路径配置。
- V0.1 不是脱离 AutoCAD 运行的独立 EXE。

## 被否决的备选方案

### 完全独立 EXE + RealDWG/ODA

暂不采用。第一版会增加 SDK 授权、图形显示、对象选择、兼容和分发成本。

### DXF 转换原型

保留为无法加载 AutoCAD 插件时的备用探针，但不作为当前主路线。

## 复审条件

出现以下情况时复审本决策：

- AutoCAD 2021 环境不存在或无法使用 `NETLOAD`；
- 目标用户不具备可扩展的 AutoCAD；
- 软件需要脱离 AutoCAD 对外分发；
- 需要同时支持多个 CAD 品牌。
