# CleanReader Desktop Legado 改造版

基于 CleanReader Desktop 3.2302.1.0 的改造版本，集成了 Legado 书源解析引擎，升级至 .NET 8 和 WinUI 3。

## 📖 项目简介

CleanReader 是一款现代化的 Windows 桌面电子书阅读器，支持 EPUB、TXT 等格式。本改造版在原版基础上，增加了对 **Legado 书源格式** 的支持，使应用能够解析和使用来自开源阅读器 Legado 的数千个在线书源。

## 🛠️ 改造内容

1. **集成 Legado 书源解析引擎**
   - 实现 `IBookSourceEngine` 接口
   - 集成 Jint JavaScript 引擎，支持 `<js>...</js>` 代码块和 `@js:` 前缀规则
   - 支持四种规则模式：JavaScript 代码块、@js: 前缀、JSONPath、XPath
   - 提供 JsBridge 类，包含 `base64Encode`、`base64Decode`、`timeFormatUTC`、`log` 等高频方法

2. **技术栈升级**
   - 升级至 **.NET 8**
   - 迁移至 **WinUI 3** 框架
   - 使用 **Windows App SDK 1.8**

3. **架构重构**
   - 解决循环依赖问题，将书源解析引擎移至 `NovelService` 项目
   - 重构项目引用为单向依赖：`CleanReader.App` → `CleanReader.Core` → `NovelService`
   - 优化 MVVM 架构，提高可维护性

## ⚠️ 当前已知问题

### 启动崩溃问题
**异常信息**: `combase.dll`，错误码 `80131534`

**问题描述**: 应用安装后启动时立即崩溃，Windows 事件查看器显示以下错误：
```
错误应用程序名称: CleanReader.App.exe
错误模块名称: combase.dll
异常代码: 0x80131534
```

**可能原因**:
1. **缺少 Windows App Runtime 1.8** - WinUI 3 应用的必需运行时组件
2. **COM 组件注册问题** - 系统缺少必要的 COM 组件
3. **.NET 8 运行时问题** - 运行时未正确安装或版本冲突

**临时解决方案**:
1. 安装 Windows App SDK 运行时 1.8
2. 确保系统已安装 .NET 8 Desktop Runtime
3. 以管理员身份运行应用

## 📋 环境要求

- **操作系统**: Windows 10 版本 1809 或更高 / Windows 11
- **运行时**: 
  - [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
  - [Windows App SDK 1.8 Runtime](https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads)
- **开发环境**:
  - Visual Studio 2022 17.8 或更高
  - Windows SDK 10.0.22621.0
  - .NET 8 SDK

## 🚀 构建步骤

### 1. 克隆仓库
```bash
git clone https://github.com/cgop12/CleanReader-Desktop-Legado.git
cd CleanReader-Desktop-Legado
```

### 2. 还原 NuGet 包
```bash
dotnet restore
```

### 3. 构建解决方案
```bash
dotnet build CleanReader.Desktop.sln --configuration Debug --platform x64
```

### 4. 生成 MSIX 包
- 在 Visual Studio 中打开解决方案
- 右键点击 `CleanReader.App` 项目 → **发布** → **创建应用包**
- 选择 **侧载** 模式，生成 MSIX 包

### 5. 安装应用
```powershell
# 以管理员身份运行 PowerShell
cd src\CleanReader.App\bin\x64\Debug\net8.0-windows10.0.22621.0
.\install_build.ps1
```

## 🆘 求助方向

我们急需以下方面的帮助：

1. **WinUI 3 启动问题诊断**
   - 解决 `combase.dll` 错误码 `80131534`
   - 调试 WinUI 3 应用启动流程

2. **Legado 书源解析优化**
   - 完善 JavaScript 执行环境
   - 处理复杂书源规则

3. **性能优化**
   - 改善书源搜索和解析性能
   - 优化内存使用

4. **UI/UX 改进**
   - 改进书源管理界面
   - 增强阅读体验

## 📄 许可证声明

本项目采用 **双许可证** 模式：

### 选项 1: MIT 许可证
- 允许商业使用、修改、分发、私有使用
- 要求保留版权声明
- 详见 [LICENSE-MIT](LICENSE-MIT)

### 选项 2: GNU AGPLv3 许可证
- 要求开源衍生作品
- 包含网络服务分发条款
- 详见 [LICENSE-AGPL](LICENSE-AGPL)

**您可以选择任一许可证来使用本项目**。如果您的使用场景符合 MIT 许可证的要求，建议选择 MIT 许可证；如果您希望确保衍生作品保持开源，请选择 AGPLv3。

## 📁 项目结构

```
CleanReader.Desktop-3.2302.1.0/
├── src/
│   ├── CleanReader.App/          # 主应用项目 (WinUI 3)
│   ├── CleanReader.Core/         # 核心业务逻辑
│   ├── NovelService/             # 书源解析服务 (含 Legado 引擎)
│   └── CleanReader.Bundle/       # 打包配置
├── assets/                       # 资源文件
├── tools/                        # 工具脚本
└── SideloadPackage/              # 侧载包配置
```

## 🤝 贡献指南

1. Fork 本仓库
2. 创建功能分支 (`git checkout -b feature/amazing-feature`)
3. 提交更改 (`git commit -m 'feat: add amazing feature'`)
4. 推送到分支 (`git push origin feature/amazing-feature`)
5. 开启 Pull Request

## 📞 联系方式

- **GitHub Issues**: [报告问题或建议](https://github.com/cgop12/CleanReader-Desktop-Legado/issues)
- **讨论区**: 欢迎在 GitHub Discussions 中交流

## 🙏 致谢

- 原版 CleanReader 开发者 [Richasy](https://github.com/Richasy)
- 开源阅读器 Legado 项目
- 所有贡献者和社区成员

---

**注意**: 本项目为实验性改造版本，可能存在稳定性问题。建议在测试环境中使用。