<div align="center">

# 🧰 ReToolbox

### 让 Windows 管理更简单、更现代

**A modern toolbox for Windows, built with WinUI 3 & .NET 8.**

---

<p>
  <a href="../../releases"><img src="https://img.shields.io/badge/version-1.2.0-2EA44F?style=for-the-badge&logo=github&logoColor=white" alt="Version"/></a>
  <a href="../../releases"><img src="https://img.shields.io/badge/download-Setup.exe-0078D4?style=for-the-badge&logo=microsoft&logoColor=white" alt="Download"/></a>
  <a href="./LICENSE.txt"><img src="https://img.shields.io/badge/license-Apache--2.0-blue?style=for-the-badge" alt="License"/></a>
  <a href="#-从源码构建-build-from-source"><img src="https://img.shields.io/badge/build-passing-2EA44F?style=for-the-badge&logo=appveyor&logoColor=white" alt="Build"/></a>
</p>

<p>
  <img src="https://img.shields.io/badge/platform-Windows%2010%2F11-0078D4?style=flat-square&logo=windows&logoColor=white" alt="Platform"/>
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet&logoColor=white" alt=".NET"/>
  <img src="https://img.shields.io/badge/UI-WinUI%203-0078D4?style=flat-square&logo=windows&logoColor=white" alt="WinUI"/>
  <img src="https://img.shields.io/badge/arch-x86%20%7C%20x64%20%7C%20ARM64-555555?style=flat-square&logo=intel&logoColor=white" alt="Arch"/>
  <img src="https://img.shields.io/badge/runtime-self--contained-FF8C00?style=flat-square" alt="Self-contained"/>
</p>

<sub>🌏 <a href="#-中文说明">简体中文</a> · <a href="#-english">English</a></sub>

</div>

<br/>

> [!TIP]
> **ReToolbox** 是一款使用 **WinUI 3 + .NET 8** 开发的现代化 Windows 桌面工具箱，采用 **Mica 材质背景** 与 **NavigationView 导航**，界面简洁、轻量、流畅。程序以 **自包含 (self-contained)** 方式发布 —— **无需安装 .NET 运行时**，下载即用。

<br/>

<!-- =================== TABLE OF CONTENTS =================== -->

## 📑 目录 / Contents

<table>
<tr>
<td valign="top" width="50%">

**中文**

- [✨ 项目亮点](#-项目亮点)
- [🚀 主要功能](#-主要功能)
- [📥 下载与安装](#-下载与安装)
- [🛠️ 从源码构建](#️-从源码构建)
- [🤝 贡献指南](#-贡献指南)
- [❓ 常见问题](#-常见问题)

</td>
<td valign="top" width="50%">

**English**

- [✨ Highlights](#-highlights)
- [🚀 Features](#-features)
- [📥 Download & Install](#-download--install)
- [🛠️ Build from Source](#-build-from-source)
- [🤝 Contributing](#-contributing)
- [❓ FAQ](#-faq)

</td>
</tr>
</table>

<br/>

<!-- =================== SPLIT BANNER =================== -->

<div align="center">

|   🌐 国际化    | 🎨 Mica 材质 | ⚡ 自包含  | 🪟 原生 WinUI 3 | 🧩 模块化 |
| :------------: | :----------: | :--------: | :-------------: | :-------: |
| 中文 / English | 现代设计语言 | 无需运行时 |    流畅体验     | 易于扩展  |

</div>

<br/>

<!-- =================== CHINESE SECTION =================== -->

<a id="-中文说明"></a>

## 🇨🇳 中文说明

<a id="-项目亮点"></a>

### ✨ 项目亮点

<table>
<tr>
<td width="50%">

🪟 **原生 Windows 体验**
基于 WinUI 3 与 .NET 8 构建，原生支持 Mica/Acrylic 材质，跟随系统的明暗主题自动切换。

⚡ **开箱即用**
采用 self-contained 方式发布，目标机器无需预装 .NET 运行时，下载即用。

</td>
<td width="50%">

🧩 **模块化功能**
主页、系统信息、内存清理、软件安装、Windows 更新、激活、Edge、Defender 等常用工具一站式集成。

🎯 **轻量纯净**
无广告、无捆绑、专注效率，让你专注于真正重要的事情。

</td>
</tr>
</table>

<a id="-主要功能"></a>

### 🚀 主要功能

| 图标 | 模块              | 说明                                                                                                                  |
| :--: | :---------------- | :-------------------------------------------------------------------------------------------------------------------- |
|  🏠  | **主页**          | 概览仪表盘与常用功能快捷入口                                                                                          |
|  🖥️  | **系统信息**      | 硬件检测（型号 / 主板 / BIOS / 处理器 / 内存 / 显卡 / NPU / 显示器 / 硬盘 / 声卡 / 网卡），支持截图导出与详细信息查看 |
|  🧠  | **内存管理**      | 内存仪表盘、轻量 / 深度清理、虚拟内存自适应、定时自动清理、进程内存排行                                               |
|  📦  | **软件安装**      | 常用软件一键安装                                                                                                      |
|  🔄  | **管理更新**      | 管理 Windows 更新行为                                                                                                 |
|  🔑  | **系统激活**      | 系统激活相关工具                                                                                                      |
|  🌐  | **管理 Edge**     | 管理 Microsoft Edge 浏览器                                                                                            |
|  🛡️  | **管理 Defender** | 管理 Microsoft Defender 安全中心                                                                                      |

<a id="-下载与安装"></a>

### 📥 下载与安装

> [!IMPORTANT]
> 安装程序需要 **管理员权限** 才能正常运行，请右键“以管理员身份运行”。

**步骤**

1. 前往 [**Releases 页面**](../../releases) 下载最新的 `ReToolbox-Setup.exe`。
2. 右键 → **以管理员身份运行** 安装程序。
3. 按向导提示完成安装。
4. 默认安装路径：`C:\Program Files\ReToolbox`

**系统要求**

| 项目     | 要求                                          |
| :------- | :-------------------------------------------- |
| 操作系统 | Windows 10 1809 (build 17763) 及以上          |
| 架构     | x64 / x86 / ARM64                             |
| 运行时   | ✅ **无需安装 .NET 运行时**（self-contained） |
| 权限     | 安装与部分功能需要管理员权限                  |

<a id="-从源码构建"></a>

### 🛠️ 从源码构建

**环境要求**

- Windows 10 / 11
- [Visual Studio 2022](https://visualstudio.microsoft.com/)（含 _WinUI / .NET 桌面开发_ 工作负载）
  - 或单独安装 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) + MSBuild
- [Inno Setup 6](https://jrsoftware.org/isdl.php)（用于编译安装程序）

**🚀 一键发布**

```powershell
# 一键发布并生成安装程序（输出位于 artifacts\）
.\scripts\build-installer.ps1
```

**🔧 手动构建**

```powershell
# 1. 发布自包含 x64 版本
msbuild ReToolbox\ReToolbox.csproj /t:Restore,Publish `
    /p:Configuration=Release /p:Platform=x64 `
    /p:RuntimeIdentifier=win-x64 `
    /p:PublishDir=artifacts\publish\win-x64-new

# 2. 使用 Inno Setup 编译安装程序
ISCC.exe installer\ReToolbox.iss
```

<a id="-贡献指南"></a>

### 🤝 贡献指南

欢迎任何形式的贡献 —— 新功能、Bug 修复、文档改进、翻译等 🤗

1. Fork 本仓库
2. 创建特性分支 (`git checkout -b feature/AmazingFeature`)
3. 提交改动 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 发起 Pull Request

<a id="-常见问题"></a>

### ❓ 常见问题

<details>
<summary><b>Q: 安装后无法启动 / 闪退？</b></summary>

A: 请尝试以 **管理员身份** 重新运行；并确认系统版本 ≥ Windows 10 1809。

</details>

<details>
<summary><b>Q: 是否需要安装 .NET 运行时？</b></summary>

A: 不需要。ReToolbox 是 self-contained 发布，自带运行时。

</details>

<details>
<summary><b>Q: 支持 ARM64 设备吗？</b></summary>

A: 支持。解决方案已包含 ARM64 配置，可发布原生 ARM64 包。

</details>

<br/>

<!-- =================== ENGLISH SECTION =================== -->

<a id="-english"></a>

## 🇬🇧 English

<a id="-highlights"></a>

### ✨ Highlights

<table>
<tr>
<td width="50%">

🪟 **Native Windows Experience**
Built with WinUI 3 & .NET 8, featuring Mica/Acrylic backdrops and automatic light/dark theme adaptation.

⚡ **Runs Out-of-the-Box**
Self-contained publishing — no separate .NET runtime installation required.

</td>
<td width="50%">

🧩 **Modular Feature Set**
Home dashboard, system info, memory management, software install, Windows Update, activation, Edge and Defender tools — all in one place.

🎯 **Lightweight & Clean**
No ads, no bundles — focused on getting things done.

</td>
</tr>
</table>

<a id="-features"></a>

### 🚀 Features

| Icon | Module             | Description                                                                                                                                     |
| :--: | :----------------- | :---------------------------------------------------------------------------------------------------------------------------------------------- |
|  🏠  | **Home**           | Overview dashboard with quick actions                                                                                                           |
|  🖥️  | **System Info**    | Hardware detection (model / board / BIOS / CPU / memory / GPU / NPU / display / disk / sound / network), with screenshot export and detail view |
|  🧠  | **Memory**         | Memory dashboard, light / deep cleanup, adaptive virtual memory, scheduled auto-cleaning, process memory ranking                                |
|  📦  | **Software**       | One-click install for common applications                                                                                                       |
|  🔄  | **Windows Update** | Manage Windows update behavior                                                                                                                  |
|  🔑  | **Activation**     | System activation utilities                                                                                                                     |
|  🌐  | **Edge**           | Manage the Microsoft Edge browser                                                                                                               |
|  🛡️  | **Defender**       | Manage Microsoft Defender security center                                                                                                       |

<a id="-download--install"></a>

### 📥 Download & Install

> [!IMPORTANT]
> The installer **requires administrator privileges**. Please right-click and choose **Run as administrator**.

**Steps**

1. Visit the [**Releases page**](../../releases) and download the latest `ReToolbox-Setup.exe`.
2. Right-click → **Run as administrator**.
3. Follow the wizard to complete installation.
4. Default install path: `C:\Program Files\ReToolbox`

**System Requirements**

| Item       | Requirement                                           |
| :--------- | :---------------------------------------------------- |
| OS         | Windows 10 1809 (build 17763) or later                |
| Arch       | x64 / x86 / ARM64                                     |
| Runtime    | ✅ **No .NET runtime required** (self-contained)      |
| Privileges | Installer and some features need administrator rights |

<a id="-build-from-source"></a>

### 🛠️ Build from Source

**Prerequisites**

- Windows 10 / 11
- [Visual Studio 2022](https://visualstudio.microsoft.com/) with the _WinUI / .NET desktop_ workload
  - Or [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) + MSBuild
- [Inno Setup 6](https://jrsoftware.org/isdl.php) (to compile the installer)

**🚀 One-Click Build**

```powershell
# Publish and build the installer in one step (output under artifacts\)
.\scripts\build-installer.ps1
```

**🔧 Manual Build**

```powershell
# 1. Publish a self-contained x64 build
msbuild ReToolbox\ReToolbox.csproj /t:Restore,Publish `
    /p:Configuration=Release /p:Platform=x64 `
    /p:RuntimeIdentifier=win-x64 `
    /p:PublishDir=artifacts\publish\win-x64-new

# 2. Compile the installer with Inno Setup
ISCC.exe installer\ReToolbox.iss
```

<a id="-contributing"></a>

### 🤝 Contributing

Contributions of any kind are welcome — new features, bug fixes, docs, translations 🤗

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

<a id="-faq"></a>

### ❓ FAQ

<details>
<summary><b>Q: The app won't start / crashes on launch?</b></summary>

A: Try running it **as administrator**, and ensure your OS is Windows 10 1809 or later.

</details>

<details>
<summary><b>Q: Do I need to install the .NET runtime?</b></summary>

A: No. ReToolbox is self-contained and ships with everything it needs.

</details>

<details>
<summary><b>Q: Is ARM64 supported?</b></summary>

A: Yes. The solution includes an ARM64 configuration so you can publish native ARM64 builds.

</details>

<br/>

<!-- =================== FOOTER =================== -->

---

<div align="center">

## 🌟 Star History

<a href="../../stargazers">
  <img src="https://img.shields.io/badge/⭐_Stars-Star%20this%20repo-FFD700?style=for-the-badge" alt="Stars"/>
</a>
<a href="../../network/members">
  <img src="https://img.shields.io/badge/🍴_Forks-Fork%20this%20repo-2EA44F?style=for-the-badge" alt="Forks"/>
</a>
<a href="../../watchers">
  <img src="https://img.shields.io/badge/👀_Watchers-Watch%20updates-0078D4?style=for-the-badge" alt="Watchers"/>
</a>

<br/>
<br/>

### 🙏 致谢 / Acknowledgements

ReToolbox is inspired by and based on
[**Atlas-OS/atlas-toolbox**](https://github.com/Atlas-OS/atlas-toolbox).

<br/>

### 📜 许可证 / License

本项目基于 [**Apache License 2.0**](./LICENSE.txt) 开源。
<br/>
Licensed under the Apache License, Version 2.0.

<br/>

<sub>Made with ❤️ for the Windows community</sub>

</div>
