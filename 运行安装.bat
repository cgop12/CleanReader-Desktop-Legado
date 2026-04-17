@echo off
chcp 65001 >nul
echo ================================================
echo    CleanReader 安装助手
echo ================================================
echo.
echo 请选择安装方式：
echo.
echo [1] 使用现有完整安装包（推荐）
echo [2] 使用简化版安装脚本
echo [3] 使用调试版安装脚本
echo [4] 查看安装指南
echo [5] 退出
echo.
set /p choice="请输入选项 (1-5): "

if "%choice%"=="1" goto option1
if "%choice%"=="2" goto option2
if "%choice%"=="3" goto option3
if "%choice%"=="4" goto option4
if "%choice%"=="5" goto option5

echo 无效选项
pause
exit /b

:option1
echo.
echo 正在打开现有完整安装包目录...
echo 请右键点击 Install.ps1，选择"使用 PowerShell 运行"
start "" "D:\重构工程\文档\CleanReader.Desktop_3.2302.1.0_x64\CleanReader.Bundle_3.2302.1.0_Test"
exit /b

:option2
echo.
echo 正在运行简化版安装脚本...
echo 如果出现权限提示，请点击"是"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install_simple.ps1"
pause
exit /b

:option3
echo.
echo 正在运行调试版安装脚本...
echo 错误日志将保存到 install.log
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install_debug.ps1"
pause
exit /b

:option4
echo.
echo 正在打开安装指南...
start "" "%~dp0安装指南.txt"
exit /b

:option5
exit /b