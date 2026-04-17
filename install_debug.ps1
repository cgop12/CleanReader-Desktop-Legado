<# ####################################################
# CleanReader 侧加载安装脚本（调试版）
# 添加详细日志记录，避免闪退
#################################################### #>

# 日志函数
function Write-Log {
    param(
        [string]$Message,
        [string]$Level = "INFO"
    )
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logEntry = "[$timestamp] [$Level] $Message"
    
    # 输出到控制台
    switch ($Level) {
        "ERROR" { Write-Host $logEntry -ForegroundColor Red }
        "WARN" { Write-Host $logEntry -ForegroundColor Yellow }
        "INFO" { Write-Host $logEntry -ForegroundColor Gray }
        "SUCCESS" { Write-Host $logEntry -ForegroundColor Green }
    }
    
    # 写入日志文件
    $logFile = Join-Path $PSScriptRoot "install.log"
    Add-Content -Path $logFile -Value $logEntry -ErrorAction SilentlyContinue
}

# 检查是否以管理员身份运行
function Test-Admin {
    try {
        $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
        $principal = New-Object Security.Principal.WindowsPrincipal($identity)
        return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    } catch {
        Write-Log "检查管理员权限失败: $_" -Level "ERROR"
        return $false
    }
}

# 主函数
function Main {
    Write-Log "=== CleanReader 安装脚本开始 ===" -Level "INFO"
    Write-Log "PowerShell 版本: $($PSVersionTable.PSVersion)" -Level "INFO"
    Write-Log "当前执行策略: $(Get-ExecutionPolicy -Scope CurrentUser)" -Level "INFO"
    Write-Log "脚本目录: $PSScriptRoot" -Level "INFO"
    
    # 检查管理员权限
    if (-not (Test-Admin)) {
        Write-Log "需要管理员权限，正在请求提升..." -Level "WARN"
        
        # 重新以管理员身份运行
        $scriptPath = if ($PSCommandPath) { $PSCommandPath } else { $MyInvocation.MyCommand.Path }
        
        try {
            Write-Log "启动管理员进程: $scriptPath" -Level "INFO"
            Start-Process PowerShell -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$scriptPath`"" -Verb RunAs
            Write-Log "已启动管理员进程，退出当前进程" -Level "INFO"
            exit 0
        } catch {
            Write-Log "启动管理员进程失败: $_" -Level "ERROR"
            Write-Host "❌ 无法以管理员身份运行脚本" -ForegroundColor Red
            Write-Host "💡 请右键点击脚本，选择'以管理员身份运行'" -ForegroundColor Yellow
            pause
            exit 1
        }
    }
    
    Write-Log "当前以管理员身份运行" -Level "SUCCESS"
    
    # 查找证书文件
    $certFile = Get-ChildItem -Path $PSScriptRoot -Filter "*.cer" | Select-Object -First 1
    if (-not $certFile) {
        $certFile = Get-ChildItem -Path $PSScriptRoot -Filter "*.crt" | Select-Object -First 1
    }
    
    if (-not $certFile) {
        Write-Log "未找到证书文件 (.cer 或 .crt)" -Level "ERROR"
        Write-Host "❌ 未找到证书文件" -ForegroundColor Red
        Write-Host "💡 请确保证书文件与脚本在同一目录下" -ForegroundColor Yellow
        pause
        exit 1
    }
    
    Write-Log "找到证书文件: $($certFile.FullName)" -Level "INFO"
    
    # 导入证书
    try {
        Write-Log "正在导入证书..." -Level "INFO"
        $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2
        $cert.Import($certFile.FullName)
        $store = New-Object System.Security.Cryptography.X509Certificates.X509Store("Root", "LocalMachine")
        $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
        $store.Add($cert)
        $store.Close()
        Write-Log "证书导入成功" -Level "SUCCESS"
    } catch {
        Write-Log "证书导入失败: $_" -Level "ERROR"
        Write-Host "❌ 证书导入失败: $_" -ForegroundColor Red
        pause
        exit 1
    }
    
    # 查找应用包文件
    $msixFile = Get-ChildItem -Path $PSScriptRoot -Filter "*.msix" | Select-Object -First 1
    if (-not $msixFile) {
        $msixFile = Get-ChildItem -Path $PSScriptRoot -Filter "*.msixbundle" | Select-Object -First 1
    }
    
    if (-not $msixFile) {
        Write-Log "未找到应用包文件 (.msix 或 .msixbundle)" -Level "ERROR"
        Write-Host "❌ 未找到应用包文件" -ForegroundColor Red
        Write-Host "💡 请确保应用包文件与脚本在同一目录下" -ForegroundColor Yellow
        pause
        exit 1
    }
    
    Write-Log "找到应用包文件: $($msixFile.FullName)" -Level "INFO"
    
    # 安装应用包
    try {
        Write-Log "正在安装应用包..." -Level "INFO"
        Add-AppxPackage -Path $msixFile.FullName -ForceApplicationShutdown
        Write-Log "应用安装成功" -Level "SUCCESS"
        Write-Host "✅ 安装完成！" -ForegroundColor Green
        Write-Host "✨ 现在可以在开始菜单中找到 CleanReader 应用" -ForegroundColor Green
    } catch {
        Write-Log "应用安装失败: $_" -Level "ERROR"
        Write-Host "❌ 应用安装失败: $_" -ForegroundColor Red
        pause
        exit 1
    }
    
    Write-Log "=== 安装脚本结束 ===" -Level "INFO"
    pause
}

# 执行主函数
try {
    Main
} catch {
    Write-Log "脚本执行过程中发生未捕获的异常: $_" -Level "ERROR"
    Write-Host "❌ 脚本执行失败: $_" -ForegroundColor Red
    pause
    exit 1
}