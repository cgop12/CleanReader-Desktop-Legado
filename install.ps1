<# ####################################################
# CleanReader 侧加载安装脚本
# 功能：自动导入证书并安装 MSIX 应用包
# 使用方法：右键点击 install.ps1，选择“使用 PowerShell 运行”
# 注意：需要管理员权限，脚本会自动请求提升权限
#################################################### #>

# 暂停脚本，等待用户按键（兼容各种 PowerShell 环境）
function Pause-Script {
    param([string]$Message = "按任意键继续...")
    
    Write-Host ""
    Write-Host $Message -ForegroundColor Gray -NoNewline
    
    try {
        # 尝试使用 ReadKey（交互式控制台）
        $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
    } catch {
        try {
            # 回退方案：使用 ReadHost（会显示输入内容）
            Read-Host | Out-Null
        } catch {
            # 最后方案：等待 5 秒
            Write-Host "（等待 5 秒后继续...）" -ForegroundColor Yellow
            Start-Sleep -Seconds 5
        }
    }
    Write-Host ""
}

# 检查执行策略
function Check-ExecutionPolicy {
    $currentPolicy = Get-ExecutionPolicy -Scope CurrentUser
    Write-Host "🔍 当前用户执行策略：$currentPolicy" -ForegroundColor Gray
    
    # 如果策略是 Restricted，尝试设置为 RemoteSigned
    if ($currentPolicy -eq "Restricted") {
        Write-Host "⚠️  检测到执行策略为 Restricted，脚本可能无法运行" -ForegroundColor Yellow
        Write-Host "💡 建议运行以下命令：Set-ExecutionPolicy RemoteSigned -Scope CurrentUser" -ForegroundColor Yellow
        Write-Host "   或右键点击脚本，选择'使用 PowerShell 运行'" -ForegroundColor Yellow
        Pause-Script -Message "按任意键继续尝试..."
    }
}

# 检查是否以管理员身份运行，如果不是则重新启动
function Request-AdminElevation {
    param([string]$ScriptPath)
    if (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
        Write-Host "⚠️  当前未以管理员身份运行，正在请求提升权限..." -ForegroundColor Yellow
        Write-Host "   如果出现用户账户控制提示，请点击'是'以继续安装" -ForegroundColor Yellow
        try {
            $process = Start-Process PowerShell -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$ScriptPath`"" -Verb RunAs -PassThru -Wait
            exit $process.ExitCode
        } catch {
            Write-Host "❌ 无法启动管理员进程：$_" -ForegroundColor Red
            Write-Host "💡 请手动以管理员身份运行此脚本" -ForegroundColor Yellow
            Pause-Script -Message "按任意键退出..."
            exit 1
        }
    }
    Write-Host "✅ 已获得管理员权限" -ForegroundColor Green
}

# 导入证书到本地计算机的受信任根证书存储区
function Import-CertificateToStore {
    param([string]$CertPath)
    
    if (-Not (Test-Path $CertPath)) {
        Write-Host "❌ 未找到证书文件：$CertPath" -ForegroundColor Red
        return $false
    }
    
    Write-Host "🔐 正在导入证书：$CertPath" -ForegroundColor Cyan
    try {
        $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2
        $cert.Import($CertPath)
        $store = New-Object System.Security.Cryptography.X509Certificates.X509Store("Root", "LocalMachine")
        $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
        $store.Add($cert)
        $store.Close()
        Write-Host "✅ 证书导入成功" -ForegroundColor Green
        return $true
    } catch {
        Write-Host "❌ 证书导入失败：$_" -ForegroundColor Red
        return $false
    }
}

# 安装 MSIX 应用包
function Install-MsixPackage {
    param([string]$MsixPath)
    
    if (-Not (Test-Path $MsixPath)) {
        Write-Host "❌ 未找到应用包文件：$MsixPath" -ForegroundColor Red
        return $false
    }
    
    Write-Host "📦 正在安装应用包：$MsixPath" -ForegroundColor Cyan
    try {
        Add-AppxPackage -Path $MsixPath -ForceApplicationShutdown -Verbose
        Write-Host "✅ 应用安装成功！" -ForegroundColor Green
        Write-Host "✨ 现在可以在开始菜单中找到 CleanReader 应用" -ForegroundColor Green
        return $true
    } catch {
        Write-Host "❌ 应用安装失败：$_" -ForegroundColor Red
        return $false
    }
}

# 主函数
function Main {
    # 显示诊断信息
    Write-Host "🔍 PowerShell 版本：$($PSVersionTable.PSVersion)" -ForegroundColor Gray
    Write-Host "🔍 当前执行策略：$(Get-ExecutionPolicy -Scope CurrentUser)" -ForegroundColor Gray
    Write-Host "================================================" -ForegroundColor Cyan
    Write-Host "    CleanReader 侧加载安装程序" -ForegroundColor Cyan
    Write-Host "================================================" -ForegroundColor Cyan
    Write-Host ""
    
    # 获取脚本所在目录（兼容不同 PowerShell 版本）
    $scriptPath = $null
    if ($PSCommandPath) {
        $scriptPath = $PSCommandPath
    } elseif ($MyInvocation.MyCommand.Path) {
        $scriptPath = $MyInvocation.MyCommand.Path
    } else {
    Write-Host "❌ 无法确定脚本路径，请手动指定" -ForegroundColor Red
    Pause-Script -Message "按任意键退出..."
    exit 1
    }
    
    $scriptDir = Split-Path -Parent $scriptPath
    Write-Host "📁 工作目录：$scriptDir" -ForegroundColor Gray
    Write-Host "📄 脚本路径：$scriptPath" -ForegroundColor Gray
    
    # 检查执行策略
    Check-ExecutionPolicy
    
    # 请求管理员权限
    Request-AdminElevation -ScriptPath $scriptPath
    
    # 查找证书文件（支持 .cer 或 .crt 扩展名）
    $certFile = Get-ChildItem -Path $scriptDir -Filter "*.cer" | Select-Object -First 1
    if (-not $certFile) {
        $certFile = Get-ChildItem -Path $scriptDir -Filter "*.crt" | Select-Object -First 1
    }
    
    if (-not $certFile) {
        Write-Host "❌ 未找到证书文件（.cer 或 .crt）" -ForegroundColor Red
        Write-Host "💡 请确保证书文件与脚本在同一目录下" -ForegroundColor Yellow
        Pause-Script -Message "按任意键退出..."
        exit 1
    }
    
    # 查找应用包文件（支持 .msix 或 .msixbundle）
    $msixFile = Get-ChildItem -Path $scriptDir -Filter "*.msix" | Select-Object -First 1
    if (-not $msixFile) {
        $msixFile = Get-ChildItem -Path $scriptDir -Filter "*.msixbundle" | Select-Object -First 1
    }
    
    if (-not $msixFile) {
        Write-Host "❌ 未找到应用包文件（.msix 或 .msixbundle）" -ForegroundColor Red
        Write-Host "💡 请确保应用包文件与脚本在同一目录下" -ForegroundColor Yellow
        Pause-Script -Message "按任意键退出..."
        exit 1
    }
    
    Write-Host "📄 找到证书文件：$($certFile.Name)" -ForegroundColor Gray
    Write-Host "📦 找到应用包文件：$($msixFile.Name)" -ForegroundColor Gray
    Write-Host ""
    
    # 导入证书
    if (-not (Import-CertificateToStore -CertPath $certFile.FullName)) {
        Write-Host "❌ 证书导入失败，安装中止" -ForegroundColor Red
        Pause-Script -Message "按任意键退出..."
        exit 1
    }
    
    Write-Host ""
    
    # 安装应用包
    if (-not (Install-MsixPackage -MsixPath $msixFile.FullName)) {
        Write-Host "❌ 应用安装失败" -ForegroundColor Red
        Pause-Script -Message "按任意键退出..."
        exit 1
    }
    
    Write-Host ""
    Write-Host "================================================" -ForegroundColor Green
    Write-Host "    安装完成！感谢使用 CleanReader" -ForegroundColor Green
    Write-Host "================================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "⚠️  注意：如果应用无法启动，请确保已安装 Windows App SDK 运行时" -ForegroundColor Yellow
    Write-Host "💡 提示：如需卸载，可在设置 → 应用 → 应用和功能中找到 CleanReader" -ForegroundColor Gray
    
    # 等待用户按任意键退出
    Write-Host ""
    Write-Host "按任意键退出..." -ForegroundColor Gray
    Pause-Script
}

# 执行主函数
try {
    Main
} catch {
    Write-Host "❌ 脚本执行过程中发生错误：$_" -ForegroundColor Red
    Write-Host "💡 请检查：1) 是否以管理员身份运行 2) 证书和应用包文件是否存在" -ForegroundColor Yellow
    Write-Host "📋 错误详情：" -ForegroundColor Red
    Write-Host $_.Exception -ForegroundColor Red
    Pause-Script -Message "按任意键退出..."
    exit 1
}