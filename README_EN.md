# CleanReader Desktop Legado Modified Version

Based on CleanReader Desktop 3.2302.1.0, this modified version integrates the Legado book source parsing engine and upgrades to .NET 8 and WinUI 3.

## 📖 Project Introduction

CleanReader is a modern Windows desktop ebook reader supporting EPUB, TXT, and other formats. This modified version adds support for the **Legado book source format**, enabling the application to parse and use thousands of online book sources from the open-source Legado reader.

## 🛠️ Modification Content

1. **Integrated Legado Book Source Parsing Engine**
   - Implemented `IBookSourceEngine` interface
   - Integrated Jint JavaScript engine, supporting `<js>...</js>` code blocks and `@js:` prefix rules
   - Supports four rule modes: JavaScript code blocks, @js: prefix, JSONPath, XPath
   - Provides JsBridge class with high-frequency methods like `base64Encode`, `base64Decode`, `timeFormatUTC`, `log`

2. **Technology Stack Upgrade**
   - Upgraded to **.NET 8**
   - Migrated to **WinUI 3** framework
   - Using **Windows App SDK 1.8**

3. **Architecture Refactoring**
   - Solved circular dependency issues by moving book source parsing engine to `NovelService` project
   - Refactored project references to unidirectional dependency: `CleanReader.App` → `CleanReader.Core` → `NovelService`
   - Optimized MVVM architecture for better maintainability

## ⚠️ Current Known Issues

### Launch Crash Issue
**Exception Information**: `combase.dll`, error code `80131534`

**Problem Description**: The application crashes immediately after installation when launching. Windows Event Viewer shows the following error:
```
Faulting application name: CleanReader.App.exe
Faulting module name: combase.dll
Exception code: 0x80131534
```

**Possible Causes**:
1. **Missing Windows App Runtime 1.8** - Required runtime component for WinUI 3 applications
2. **COM component registration issue** - System lacks necessary COM components
3. **.NET 8 runtime problem** - Runtime not correctly installed or version conflict

**Temporary Solutions**:
1. Install Windows App SDK Runtime 1.8
2. Ensure .NET 8 Desktop Runtime is installed
3. Run the application as administrator

## 📋 Environment Requirements

- **Operating System**: Windows 10 version 1809 or later / Windows 11
- **Runtime**:
  - [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
  - [Windows App SDK 1.8 Runtime](https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads)
- **Development Environment**:
  - Visual Studio 2022 17.8 or later
  - Windows SDK 10.0.22621.0
  - .NET 8 SDK

## 🚀 Build Steps

### 1. Clone Repository
```bash
git clone https://github.com/cgop12/CleanReader-Desktop-Legado.git
cd CleanReader-Desktop-Legado
```

### 2. Restore NuGet Packages
```bash
dotnet restore
```

### 3. Build Solution
```bash
dotnet build CleanReader.Desktop.sln --configuration Debug --platform x64
```

### 4. Generate MSIX Package
- Open the solution in Visual Studio
- Right-click the `CleanReader.App` project → **Publish** → **Create App Packages**
- Select **Sideload** mode to generate the MSIX package

### 5. Install Application
```powershell
# Run PowerShell as administrator
cd src\CleanReader.App\bin\x64\Debug\net8.0-windows10.0.22621.0
.\install_build.ps1
```

## 🆘 Help Needed

We urgently need help in the following areas:

1. **WinUI 3 Launch Issue Diagnosis**
   - Solve `combase.dll` error code `80131534`
   - Debug WinUI 3 application launch process

2. **Legado Book Source Parsing Optimization**
   - Improve JavaScript execution environment
   - Handle complex book source rules

3. **Performance Optimization**
   - Improve book source search and parsing performance
   - Optimize memory usage

4. **UI/UX Improvements**
   - Enhance book source management interface
   - Improve reading experience

## 📄 License Declaration

This project uses a **dual-license** model:

### Option 1: MIT License
- Allows commercial use, modification, distribution, private use
- Requires retention of copyright notice
- See [LICENSE-MIT](LICENSE-MIT) for details

### Option 2: GNU AGPLv3 License
- Requires open-source derivative works
- Includes network service distribution terms
- See [LICENSE-AGPL](LICENSE-AGPL) for details

**You can choose either license to use this project**. If your use case meets the requirements of the MIT License, we recommend choosing the MIT License; if you want to ensure derivative works remain open-source, please choose AGPLv3.

## 📁 Project Structure

```
CleanReader.Desktop-3.2302.1.0/
├── src/
│   ├── CleanReader.App/          # Main application project (WinUI 3)
│   ├── CleanReader.Core/         # Core business logic
│   ├── NovelService/             # Book source parsing service (with Legado engine)
│   └── CleanReader.Bundle/       # Packaging configuration
├── assets/                       # Resource files
├── tools/                        # Tool scripts
└── SideloadPackage/              # Sideload package configuration
```

## 🤝 Contribution Guide

1. Fork this repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'feat: add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📞 Contact Information

- **GitHub Issues**: [Report problems or suggestions](https://github.com/cgop12/CleanReader-Desktop-Legado/issues)
- **Discussions**: Welcome to communicate in GitHub Discussions

## 🙏 Acknowledgments

- Original CleanReader developer [Richasy](https://github.com/Richasy)
- Open-source Legado reader project
- All contributors and community members

## 🔗 Related Repositories

- **[CleanReader](https://github.com/Clean-Reader/CleanReader.Desktop)** – Original CleanReader desktop ebook reader
- **[Legado](https://github.com/gedoor/legado)** – Open-source reader project providing book source format and parsing rules

---

**Note**: This project is an experimental modified version and may have stability issues. Recommended for use in testing environments.