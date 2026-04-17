<# ####################################################
# CleanReader 侧加载包创建脚本
# 功能：构建应用，打包为 MSIX，生成证书，准备安装文件
# 使用方法：右键点击 Create-SideloadPackage.ps1，选择“使用 PowerShell 运行”
# 输出：SideloadPackage 文件夹，包含 install.ps1、.cer 证书和 .msixbundle
#################################################### #>

param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$OutputDir = "SideloadPackage"
)

$ErrorActionPreference = "Stop"

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "    CleanReader 侧加载包创建工具" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

# 获取脚本所在目录（解决方案根目录）
$solutionDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$solutionFile = Join-Path $solutionDir "CleanReader.Desktop.sln"
$appProject = Join-Path $solutionDir "src\CleanReader.App\CleanReader.App.csproj"
$bundleProject = Join-Path $solutionDir "src\CleanReader.Bundle\CleanReader.Bundle.wapproj"
$installScript = Join-Path $solutionDir "install.ps1"

Write-Host "📁 解决方案目录：$solutionDir" -ForegroundColor Gray
Write-Host "⚙️  配置：$Configuration，平台：$Platform" -ForegroundColor Gray
Write-Host "📦 输出目录：$OutputDir" -ForegroundColor Gray
Write-Host ""

# 1. 检查必要工具
Write-Host "🔧 检查必要工具..." -ForegroundColor Cyan
$winappPath = (Get-Command "winapp" -ErrorAction SilentlyContinue).Source
if (-not $winappPath) {
    Write-Host "❌ 未找到 WinApp CLI，请先安装 WinApp CLI" -ForegroundColor Red
    Write-Host "💡 安装命令：dotnet tool install -g winapp.cli" -ForegroundColor Yellow
    exit 1
}
Write-Host "✅ WinApp CLI 已安装：$winappPath" -ForegroundColor Green

# 2. 恢复 NuGet 包
Write-Host "📦 恢复 NuGet 包..." -ForegroundColor Cyan
try {
    dotnet restore $solutionFile
    Write-Host "✅ NuGet 包恢复成功" -ForegroundColor Green
} catch {
    Write-Host "❌ NuGet 包恢复失败：$_" -ForegroundColor Red
    exit 1
}

# 3. 构建主应用
Write-Host "🏗️  构建主应用..." -ForegroundColor Cyan
try {
    dotnet build $appProject --configuration $Configuration --platform $Platform
    Write-Host "✅ 主应用构建成功" -ForegroundColor Green
} catch {
    Write-Host "❌ 主应用构建失败：$_" -ForegroundColor Red
    exit 1
}

# 4. 确定应用输出目录
$appOutputDir = Join-Path $solutionDir "src\CleanReader.App\bin\$Platform\$Configuration\net8.0-windows10.0.22621.0"
if (-not (Test-Path $appOutputDir)) {
    Write-Host "❌ 应用输出目录不存在：$appOutputDir" -ForegroundColor Red
    exit 1
}
Write-Host "📂 应用输出目录：$appOutputDir" -ForegroundColor Gray

# 5. 使用 winapp package 打包
Write-Host "📦 使用 WinApp CLI 打包..." -ForegroundColor Cyan
$packageOutputDir = Join-Path $solutionDir $OutputDir
if (Test-Path $packageOutputDir) {
    Remove-Item $packageOutputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $packageOutputDir -Force | Out-Null

$tempPackageDir = Join-Path $solutionDir "TempPackage"
if (Test-Path $tempPackageDir) {
    Remove-Item $tempPackageDir -Recurse -Force
}
New-Item -ItemType Directory -Path $tempPackageDir -Force | Out-Null

# 复制必要的文件到临时目录
Copy-Item (Join-Path $appOutputDir "*") -Destination $tempPackageDir -Recurse -Force
# 复制清单文件（如果存在）
$manifestSource = Join-Path $solutionDir "src\CleanReader.Bundle\Package.appxmanifest"
if (Test-Path $manifestSource) {
    Copy-Item $manifestSource -Destination $tempPackageDir -Force
}

Write-Host "🔨 正在生成 MSIX 包..." -ForegroundColor Gray
try {
    & winapp package $tempPackageDir --output $packageOutputDir --verbose
    Write-Host "✅ MSIX 包生成成功" -ForegroundColor Green
} catch {
    Write-Host "❌ MSIX 包生成失败：$_" -ForegroundColor Red
    exit 1
} finally {
    # 清理临时目录
    if (Test-Path $tempPackageDir) {
        Remove-Item $tempPackageDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# 6. 查找生成的 MSIX 包和证书
Write-Host "🔍 查找生成的文件..." -ForegroundColor Cyan
$msixFile = Get-ChildItem -Path $packageOutputDir -Filter "*.msix" | Select-Object -First 1
if (-not $msixFile) {
    $msixFile = Get-ChildItem -Path $packageOutputDir -Filter "*.msixbundle" | Select-Object -First 1
}
if (-not $msixFile) {
    Write-Host "❌ 未找到 MSIX 包文件" -ForegroundColor Red
    exit 1
}
Write-Host "📦 找到 MSIX 包：$($msixFile.Name)" -ForegroundColor Gray

# 7. 处理证书
Write-Host "🔐 处理证书..." -ForegroundColor Cyan
$certFile = Get-ChildItem -Path $packageOutputDir -Filter "*.cer" | Select-Object -First 1
if (-not $certFile) {
    # 尝试从 .pfx 生成 .cer
    $pfxFile = Get-ChildItem -Path $packageOutputDir -Filter "*.pfx" | Select-Object -First 1
    if ($pfxFile) {
        Write-Host "🔄 从 PFX 生成 CER 证书..." -ForegroundColor Gray
        $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2
        $cert.Import($pfxFile.FullName)
        $certBytes = $cert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Cert)
        $cerPath = Join-Path $packageOutputDir "CleanReader_Sideload.cer"
        [System.IO.File]::WriteAllBytes($cerPath, $certBytes)
        $certFile = Get-Item $cerPath
        Write-Host "✅ CER 证书已生成：$($certFile.Name)" -ForegroundColor Green
    } else {
        Write-Host "⚠️  未找到证书文件，侧加载可能需要手动导入证书" -ForegroundColor Yellow
    }
} else {
    Write-Host "✅ 找到证书文件：$($certFile.Name)" -ForegroundColor Green
}

# 8. 复制安装脚本
Write-Host "📄 复制安装脚本..." -ForegroundColor Cyan
if (Test-Path $installScript) {
    Copy-Item $installScript -Destination $packageOutputDir -Force
    Write-Host "✅ 安装脚本已复制" -ForegroundColor Green
} else {
    Write-Host "❌ 未找到 install.ps1 脚本" -ForegroundColor Red
    exit 1
}

# 9. 输出结果
Write-Host ""
Write-Host "================================================" -ForegroundColor Green
Write-Host "    侧加载包创建完成！" -ForegroundColor Green
Write-Host "================================================" -ForegroundColor Green
Write-Host ""
Write-Host "📁 输出目录：$packageOutputDir" -ForegroundColor Gray
Write-Host ""
Write-Host "📋 生成的文件：" -ForegroundColor Gray
Get-ChildItem -Path $packageOutputDir | ForEach-Object {
    Write-Host "   - $($_.Name)" -ForegroundColor Gray
}
Write-Host ""
Write-Host "🚀 安装方法：" -ForegroundColor Gray
Write-Host "   1. 将整个文件夹复制到目标计算机" -ForegroundColor Gray
Write-Host "   2. 右键点击 install.ps1，选择'使用 PowerShell 运行'" -ForegroundColor Gray
Write-Host "   3. 按照提示完成安装" -ForegroundColor Gray
Write-Host ""
Write-Host "⚠️  注意：目标计算机需要启用侧加载功能" -ForegroundColor Yellow
Write-Host "💡 提示：在 PowerShell 中运行以下命令启用侧加载：" -ForegroundColor Gray
Write-Host "      Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser" -ForegroundColor Gray
Write-Host "      Install-Module -Name TrustedRoot -Force" -ForegroundColor Gray