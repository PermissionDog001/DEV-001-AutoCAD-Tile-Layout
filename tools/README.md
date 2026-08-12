# tools

项目专用的构建、打包、诊断和维护工具目录。工具必须可追溯，不得在脚本中硬编码个人路径、密钥或 Autodesk 受限二进制文件。

当前工具：

- `Invoke-CoreReflectionTests.ps1`：在已构建的 Debug/Release 测试程序集上运行 MSTest 方法和 `DataRow`，用于绕过当前 .NET Framework VSTest 宿主的发现/栈溢出限制；从项目根目录调用，参数为 `-Configuration Debug` 或 `-Configuration Release`。
