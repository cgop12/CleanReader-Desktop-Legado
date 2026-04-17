# 测试脚本 - 模拟安装脚本运行环境
Write-Host "测试 PowerShell 环境" -ForegroundColor Cyan
Write-Host "PowerShell 版本: $($PSVersionTable.PSVersion)" -ForegroundColor Gray
Write-Host "执行策略: $(Get-ExecutionPolicy -Scope CurrentUser)" -ForegroundColor Gray
Write-Host "当前目录: $PWD" -ForegroundColor Gray
Write-Host "脚本目录: $PSScriptRoot" -ForegroundColor Gray

# 测试 pause 命令
Write-Host "`n测试 pause 命令..." -ForegroundColor Yellow
try {
    pause
    Write-Host "pause 命令正常工作" -ForegroundColor Green
} catch {
    Write-Host "pause 命令失败: $_" -ForegroundColor Red
}

# 测试 Read-Host
Write-Host "`n测试 Read-Host 命令..." -ForegroundColor Yellow
try {
    $null = Read-Host "按 Enter 继续"
    Write-Host "Read-Host 命令正常工作" -ForegroundColor Green
} catch {
    Write-Host "Read-Host 命令失败: $_" -ForegroundColor Red
}

# 测试管理员权限检查
Write-Host "`n测试管理员权限检查..." -ForegroundColor Yellow
try {
    $isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    Write-Host "管理员状态: $isAdmin" -ForegroundColor Gray
} catch {
    Write-Host "管理员检查失败: $_" -ForegroundColor Red
}

Write-Host "`n测试完成，按任意键退出..." -ForegroundColor Cyan
pause