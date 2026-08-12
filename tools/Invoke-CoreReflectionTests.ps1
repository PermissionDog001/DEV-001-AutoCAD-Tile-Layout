param(
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$frameworkAssemblyPath = Join-Path $projectRoot "build\packages\mstest.testframework\4.3.2\lib\net462\MSTest.TestFramework.dll"
$testAssemblyPath = Join-Path $projectRoot ("build\tests\{0}\TileLayout.Core.Tests.dll" -f $Configuration)

if (-not (Test-Path -LiteralPath $frameworkAssemblyPath)) {
    throw "MSTest.TestFramework.dll not found: $frameworkAssemblyPath"
}

if (-not (Test-Path -LiteralPath $testAssemblyPath)) {
    throw "Test assembly not found: $testAssemblyPath"
}

[void][System.Reflection.Assembly]::LoadFrom($frameworkAssemblyPath)
$testAssembly = [System.Reflection.Assembly]::LoadFrom($testAssemblyPath)
$testMethodAttributeName = "Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute"
$dataRowAttributeName = "Microsoft.VisualStudio.TestTools.UnitTesting.DataRowAttribute"

$total = 0
$passed = 0
$failed = 0

foreach ($testType in $testAssembly.GetExportedTypes() | Sort-Object FullName) {
    $methods = $testType.GetMethods([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance) |
        Where-Object {
            @($_.GetCustomAttributes($true) | Where-Object { $_.GetType().FullName -eq $testMethodAttributeName }).Count -gt 0
        } |
        Sort-Object Name

    if ($testType.IsAbstract -or $methods.Count -eq 0) {
        continue
    }

    $instance = [Activator]::CreateInstance($testType)

    foreach ($method in $methods) {
        $attributes = @($method.GetCustomAttributes($true))
        $dataRows = @($attributes | Where-Object { $_.GetType().FullName -eq $dataRowAttributeName })
        if ($dataRows.Count -eq 0) {
            $dataRows = @($null)
        }

        foreach ($dataRow in $dataRows) {
            $total++
            $caseName = "{0}.{1}" -f $testType.FullName, $method.Name
            try {
                if ($null -eq $dataRow) {
                    [void]$method.Invoke($instance, $null)
                }
                else {
                    [void]$method.Invoke($instance, [object[]]$dataRow.Data)
                }

                $passed++
            }
            catch {
                $failed++
                $failure = $_.Exception.InnerException
                if ($null -eq $failure) {
                    $failure = $_.Exception
                }
                Write-Output ("FAIL {0}: {1}" -f $caseName, $failure.Message)
            }
        }
    }
}

Write-Output ("TOTAL={0} PASS={1} FAIL={2}" -f $total, $passed, $failed)
if ($failed -gt 0) {
    exit 1
}
