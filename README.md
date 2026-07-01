<div align="center">

# ReToolbox

**Windows 系统工具箱 / Windows System Toolbox**

基于 WinUI 3 与 .NET 8 构建的现代 Windows 桌面工具箱，提供软件安装、更新管理、系统激活与 Edge 管理等常用功能。

A modern Windows desktop toolbox built with WinUI 3 and .NET 8, bundling everyday utilities such as software installation, update management, system activation and Edge management.

[![Platform](https://img.shields.io/badge/platform-Windows%20x64-0078D4)](#)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](#)
[![Version](https://img.shields.io/badge/version-1.2.0-2EA44F)](../../releases)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue)](./LICENSE.txt)

</div>

---

## 中文说明

### 简介

ReToolbox 是一款使用 **WinUI 3 + .NET 8** 开发的 Windows 桌面应用，采用 Mica 材质背景与 NavigationView 导航，界面现代、轻量。程序为 **自包含（self-contained）** 发布，无需单独安装 .NET 运行时即可运行。

### 功能

- 🏠 **主页** —— 概览与快捷入口
- 🖥️ **系统信息** —— 硬件检测(型号/主板/BIOS/处理器/内存/显卡/NPU/显示器/硬盘/声卡/网卡),支持截图导出与详细信息
- 🧠 **内存管理** —— 内存仪表盘、轻量/深度清理、虚拟内存自适应、定时自动清理、进程内存排行
- 📦 **软件安装** —— 常用软件一键安装
- 🔄 **管理更新** —— 管理 Windows 更新
- 🔑 **系统激活** —— 系统激活相关工具
- 🌐 **管理 Edge** —— 管理微软 Edge 浏览器
- 🛡️ **管理 Defender** —— 管理微软 Defender

### 下载与安装

1. 前往 [Releases 页面](../../releases)。
2. 下载最新的 `ReToolbox-Setup.exe`。
3. 右键“以管理员身份运行”（安装程序需要管理员权限）。
4. 按照向导完成安装，默认安装到 `C:\Program Files\ReToolbox`。

> 程序为 64 位自包含发布，仅支持 **Windows 10 1809（build 17763）及以上** 的 x64 系统。

### 从源码构建

**环境要求**

- Windows 10/11
- [Visual Studio 2022（含 WinUI / .NET 桌面开发工作负载）](https://visualstudio.microsoft.com/) 或 .NET 8 SDK + MSBuild
- [Inno Setup 6](https://jrsoftware.org/isdl.php)（用于生成安装程序）

**步骤**

```powershell
# 一键发布并生成安装程序（输出位于 artifacts\）
.\scripts\build-installer.ps1
```

或手动执行：

```powershell
# 发布自包含 x64 版本
msbuild ReToolbox\ReToolbox.csproj /t:Restore,Publish `
    /p:Configuration=Release /p:Platform=x64 `
    /p:RuntimeIdentifier=win-x64 `
    /p:PublishDir=artifacts\publish\win-x64-new

# 使用 Inno Setup 编译安装程序
ISCC.exe installer\ReToolbox.iss
```

---

## English

### Overview

ReToolbox is a Windows desktop application built with **WinUI 3 + .NET 8**. It ships with a Mica backdrop and NavigationView-based UI, and is published as a **self-contained** binary, so no separate .NET runtime installation is required.

### Features

- 🏠 **Home** — overview and quick actions
- 🖥️ **System Info** — hardware detection (model / board / BIOS / CPU / memory / GPU / NPU / display / disk / sound / network), with screenshot export and detail view
- 🧠 **Memory** — memory dashboard, light/deep cleanup, adaptive virtual memory, scheduled auto-cleaning, process memory ranking
- 📦 **Software** — install common applications
- 🔄 **Windows Update** — manage Windows updates
- 🔑 **Activation** — system activation utilities
- 🌐 **Edge** — manage the Microsoft Edge browser
- 🛡️ **Defender** — manage Microsoft Defender

### Download & Install

1. Go to the [Releases page](../../releases).
2. Download the latest `ReToolbox-Setup.exe`.
3. Right-click and **Run as administrator** (the installer requires elevation).
4. Follow the wizard; the default install path is `C:\Program Files\ReToolbox`.

> The build is a 64-bit self-contained package targeting **Windows 10 1809 (build 17763) and later** on x64.

### Build from Source

**Prerequisites**

- Windows 10/11
- [Visual Studio 2022 (with the WinUI / .NET desktop workload)](https://visualstudio.microsoft.com/), or the .NET 8 SDK + MSBuild
- [Inno Setup 6](https://jrsoftware.org/isdl.php) (to produce the installer)

**Steps**

```powershell
# Publish and build the installer in one step (output under artifacts\)
.\scripts\build-installer.ps1
```

Or build manually:

```powershell
# Publish a self-contained x64 build
msbuild ReToolbox\ReToolbox.csproj /t:Restore,Publish `
    /p:Configuration=Release /p:Platform=x64 `
    /p:RuntimeIdentifier=win-x64 `
    /p:PublishDir=artifacts\publish\win-x64-new

# Compile the installer with Inno Setup
ISCC.exe installer\ReToolbox.iss
```

---

## 致谢 / Acknowledgements

ReToolbox is inspired by and based on [Atlas-OS/atlas-toolbox](https://github.com/Atlas-OS/atlas-toolbox).

## 许可证 / License

本项目基于 [Apache License 2.0](./LICENSE.txt) 开源。

Licensed under the Apache License, Version 2.0.
