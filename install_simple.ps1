<# ####################################################
# CleanReader 侧加载安装脚本（简化版）
# 类似标准 Windows 应用包安装脚本
#################################################### #>

# 检查是否以管理员身份运行
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host "需要管理员权限..." -ForegroundColor Yellow
    Write-Host "正在请求提升权限..." -ForegroundColor Yellow
    
    # 获取当前脚本路径
    $scriptPath = if ($PSCommandPath) { $PSCommandPath } else { $MyInvocation.MyCommand.Path }
    
    # 重新以管理员身份运行
    try {
        Start-Process PowerShell -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$scriptPath`"" -Verb RunAs
    } catch {
        Write-Host "无法以管理员身份运行脚本" -ForegroundColor Red
        Write-Host "请右键点击脚本，选择'以管理员身份运行'" -ForegroundColor Yellow
    }
    exit
}

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "    CleanReader 侧加载安装程序" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

# 检查当前目录
Write-Host "工作目录: $PSScriptRoot" -ForegroundColor Gray

# 查找证书文件
$certFile = Get-ChildItem -Path $PSScriptRoot -Filter "*.cer" | Select-Object -First 1
if (-not $certFile) {
    $certFile = Get-ChildItem -Path $PSScriptRoot -Filter "*.crt" | Select-Object -First 1
}

if (-not $certFile) {
    Write-Host "错误: 未找到证书文件 (.cer 或 .crt)" -ForegroundColor Red
    Write-Host "请确保证书文件与脚本在同一目录下" -ForegroundColor Yellow
    pause
    exit 1
}

Write-Host "找到证书文件: $($certFile.Name)" -ForegroundColor Gray

# 导入证书
try {
    Write-Host "正在导入证书..." -ForegroundColor Cyan
    $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2
    $cert.Import($certFile.FullName)
    $store = New-Object System.Security.Cryptography.X509Certificates.X509Store("Root", "LocalMachine")
    $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
    $store.Add($cert)
    $store.Close()
    Write-Host "证书导入成功" -ForegroundColor Green
} catch {
    Write-Host "证书导入失败: $_" -ForegroundColor Red
    pause
    exit 1
}

Write-Host ""

# 查找应用包文件
$msixFile = Get-ChildItem -Path $PSScriptRoot -Filter "*.msix" | Select-Object -First 1
if (-not $msixFile) {
    $msixFile = Get-ChildItem -Path $PSScriptRoot -Filter "*.msixbundle" | Select-Object -First 1
}

if (-not $msixFile) {
    Write-Host "错误: 未找到应用包文件 (.msix 或 .msixbundle)" -ForegroundColor Red
    Write-Host "请确保应用包文件与脚本在同一目录下" -ForegroundColor Yellow
    pause
    exit 1
}

Write-Host "找到应用包文件: $($msixFile.Name)" -ForegroundColor Gray

# 安装应用包
try {
    Write-Host "正在安装应用包..." -ForegroundColor Cyan
    Add-AppxPackage -Path $msixFile.FullName -ForceApplicationShutdown
    Write-Host "应用安装成功！" -ForegroundColor Green
    Write-Host "现在可以在开始菜单中找到 CleanReader 应用" -ForegroundColor Green
} catch {
    Write-Host "应用安装失败: $_" -ForegroundColor Red
    pause
    exit 1
}

Write-Host ""
Write-Host "================================================" -ForegroundColor Green
Write-Host "    安装完成！" -ForegroundColor Green
Write-Host "================================================" -ForegroundColor Green
Write-Host ""
Write-Host "按任意键退出..." -ForegroundColor Gray
pause