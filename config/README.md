# config

保存非敏感配置模板。V0.1 的砖尺寸与规则固定在需求基线中，暂不创建可编辑规则配置。

不得在此保存密码、令牌、证书私钥或生产连接串。

AutoCAD 本机引用配置：

1. 复制 `AutoCAD.Local.props.example` 为 `AutoCAD.Local.props`；
2. 在本地文件中填写 AutoCAD 2021 安装目录；
3. `AutoCAD.Local.props` 被 Git 忽略，不提交个人绝对路径；
4. 也可以使用环境变量 `AUTOCAD2021_DIR` 代替本地文件。
