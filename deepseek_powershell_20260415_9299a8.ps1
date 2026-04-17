# Check-CleanReaderEnv.ps1
# 仅检查 CleanReader.Desktop 项目所需的构建环境，不修改任何文件

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  CleanReader.Desktop 环境检查报告" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 1. 检查 .NET SDK
Write-Host "[1] .NET SDK 版本" -ForegroundColor Yellow
$sdks = dotnet --list-sdks 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "    状态: 未安装或无法获取" -ForegroundColor Red
} else {
    $net8Sdk = $sdks | Where-Object { $_ -match "^8\.0\." }
    if ($net8Sdk) {
        Write-Host "    状态: 已安装 .NET 8 SDK" -ForegroundColor Green
        $net8Sdk | ForEach-Object { Write-Host "      $_" }
        $versionMatch = [regex]::Match($net8Sdk, "8\.0\.(\d+)")
        if ($versionMatch.Success -and [int]$versionMatch.Groups[1].Value -ge 402) {
            Write-Host "    版本检查: 满足要求 (>=8.0.402)" -ForegroundColor Green
        } else {
            Write-Host "    版本检查: 可能过低，建议升级到 8.0.402+" -ForegroundColor DarkYellow
        }
    } else {
        Write-Host "    状态: 未找到 .NET 8 SDK" -ForegroundColor Red
    }
}
Write-Host ""

# 2. 检查 Windows SDK
Write-Host "[2] Windows SDK" -ForegroundColor Yellow
$winSdkPaths = @(
    "C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64",
    "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64",
    "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64"
)
$foundSdk = $null
foreach ($path in $winSdkPaths) {
    if (Test-Path $path) {
        $foundSdk = $path
        break
    }
}
if ($foundSdk) {
    Write-Host "    状态: 已安装 Windows SDK" -ForegroundColor Green
    Write-Host "    路径: $foundSdk"
} else {
    Write-Host "    状态: 未找到 Windows SDK (10.0.19041+)" -ForegroundColor Red
    Write-Host "    提示: 需安装 Windows SDK 或 Windows App SDK 扩展" -ForegroundColor Gray
}
Write-Host ""

# 3. 检查 MSBuild
Write-Host "[3] MSBuild" -ForegroundColor Yellow
$msbuildPath = (Get-Command msbuild -ErrorAction SilentlyContinue).Source
if (-not $msbuildPath) {
    $vsPath = "${env:ProgramFiles}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
    if (Test-Path $vsPath) {
        $msbuildPath = $vsPath
    }
}
if ($msbuildPath) {
    Write-Host "    状态: 可用" -ForegroundColor Green
    Write-Host "    路径: $msbuildPath"
} else {
    Write-Host "    状态: 未找到 MSBuild" -ForegroundColor Red
    Write-Host "    提示: 需安装 Visual Studio Build Tools 或 .NET SDK" -ForegroundColor Gray
}
Write-Host ""

# 4. 检查 Visual Studio 组件
Write-Host "[4] Visual Studio 工作负载" -ForegroundColor Yellow
$vsWhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (Test-Path $vsWhere) {
    $vsPath = & $vsWhere -latest -property installationPath 2>$null
    if ($vsPath) {
        Write-Host "    已安装 Visual Studio: $vsPath" -ForegroundColor Green
        $sdkManifest = "$vsPath\SDKs\Microsoft.WindowsAppSDK\*"
        if (Test-Path $sdkManifest) {
            Write-Host "    Windows App SDK 组件: 已安装" -ForegroundColor Green
        } else {
            Write-Host "    Windows App SDK 组件: 未检测到，可能需要安装" -ForegroundColor DarkYellow
        }
    } else {
        Write-Host "    未检测到 Visual Studio" -ForegroundColor DarkYellow
    }
} else {
    Write-Host "    未安装 Visual Studio (可能仅使用命令行工具)" -ForegroundColor DarkYellow
}
Write-Host ""

# 5. 检查项目关键配置
Write-Host "[5] 项目配置检查" -ForegroundColor Yellow
$toolkitProj = "src\Toolkit.Desktop\Toolkit.Desktop.csproj"
if (Test-Path $toolkitProj) {
    $content = Get-Content $toolkitProj -Raw
    if ($content -match "<AllowUnsafeBlocks>true</AllowUnsafeBlocks>") {
        Write-Host "    Toolkit.Desktop 不安全代码编译: 已启用" -ForegroundColor Green
    } else {
        Write-Host "    Toolkit.Desktop 不安全代码编译: 未启用 (可能导致 CS0227 错误)" -ForegroundColor DarkYellow
    }
} else {
    Write-Host "    未找到 Toolkit.Desktop.csproj" -ForegroundColor Gray
}
Write-Host ""

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  检查完成" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# 暂停，防止窗口闪退
Read-Host -Prompt "`n按 Enter 键退出"