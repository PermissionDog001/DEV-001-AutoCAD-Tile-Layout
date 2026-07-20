# tests/fixtures

这里只存放已经脱敏、固定、可复现的测试夹具。真实用户 DWG 不得直接放入本目录或提交。

M4 代表性夹具建议命名为：

```text
m4-real-room-sanitized.dwg
m4-real-room-sanitized.md
```

同名 Markdown 说明必须记录非敏感的来源类型、AutoCAD 保存格式、文件大小、SHA-256、单位、WCS/UCS 状态、代表房间宽高/高程、匿名图层、预期结果和脱敏检查。详细流程与验收矩阵见 `docs/dwg-acceptance-m4.md`。

夹具进入本目录前必须满足：

- 未脱敏中间副本只放在被 Git 忽略的 `work/m4-redaction`；
- 已移除项目名、单位名、人员、联系方式、地址、图签、文字、块属性、外部参照、底图、数据链接和不必要的布局/元数据；
- 只保留代表性四条独立 `LINE`、匿名图层和少量无敏感邻近对象；
- 用户已人工确认脱敏完成；
- DWG 加入 Git 前已另行获得用户明确授权。

当前状态：已建立只读脱敏夹具及同名说明，并完成 M4 实机验收；DWG 未获 Git 提交授权，继续由 `.gitignore` 排除。
