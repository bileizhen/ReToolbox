# ReToolbox 重装工具箱 - 实现计划

## 项目概述

在现有的 `ReToolbox` 项目（.NET 8 + WinUI 3）基础上，复刻 Atlas Toolbox 的 UI 设计风格，实现一个"重装工具箱"工具，提供以下核心功能：
1. 自动安装常用软件
2. 激活系统（MAS）
3. 暂停/管理 Windows 更新
4. 卸载 Edge 浏览器

## 技术栈

- **框架**: .NET 8 + WinUI 3 (Windows App SDK)
- **架构模式**: MVVM + DI (Microsoft.Extensions.Hosting)
- **UI 组件**: CommunityToolkit.WinUI.Controls (SettingsCard, SettingsExpander)
- **工具库**: CommunityToolkit.Mvvm, WinUIEx
- **日志**: NLog
- **当前项目**: `d:\source\repos\ReToolbox\ReToolbox\` (空模板状态)

## UI 设计参考 (Atlas Toolbox)

从 Atlas Toolbox 源码中提取的关键 UI 特征：
- **主窗口**: NavigationView + TitleBar + Mica 背景效果
- **导航项**: Home / Software / 各功能分类 + Footer Settings
- **首页**: 大横幅 Banner + TileGallery 水平滚动卡片 + 收藏列表
- **配置页**: BreadcrumbBar + ScrollViewer + SettingsCard 列表（ToggleSwitch/ComboBox/Button）
- **设置页**: SettingsCard + SettingsExpander 分组布局
- **卡片样式**: `ConfigurationSettingsCardTemplate` - 无外边框、统一 Margin
- **字体**: Archivo 字体族

---

## 实现步骤

### 第 1 步：更新项目依赖 (ReToolbox.csproj)

添加 NuGet 包引用：
- `CommunityToolkit.Mvvm` 8.4.0
- `CommunityToolkit.WinUI.Controls.SettingsControls` 8.1.240916
- `CommunityToolkit.WinUI.Controls.Primitives` 8.1.240916
- `CommunityToolkit.WinUI.Animations` 8.1.240916
- `Microsoft.Extensions.Hosting` 9.0.3
- `WinUIEx` 2.5.1
- `NLog` 5.4.0

更新 TargetFramework 为 `net8.0-windows10.0.26100.0`，添加 `WindowsPackageType=None`、`SelfContained=true` 等配置。

### 第 2 步：创建目录结构和基础架构

```
ReToolbox/
├── Assets/
│   ├── Fonts/          (Archivo 字体)
│   └── Logo/           (应用图标)
├── Commands/           (ICommand 实现)
├── Controls/           (自定义控件: TileGallery, HeaderTile, HomePageHeaderImage)
├── Enums/              (枚举类型)
├── Models/             (数据模型)
├── Resources/          (XAML 资源字典: 卡片样式等)
├── Services/           (业务服务: 软件安装、系统激活、更新管理等)
├── Utils/              (工具类: RegistryHelper, CommandPromptHelper 等)
├── ViewModels/         (MVVM ViewModel)
├── Views/              (XAML 页面)
│   └── CustomViews/    (自定义子页面)
├── lang/               (多语言 JSON)
├── App.xaml / App.xaml.cs
├── MainWindow.xaml / MainWindow.xaml.cs
└── Package.appxmanifest
```

### 第 3 步：实现工具类 (Utils/)

创建以下工具类（参考 Atlas Toolbox 实现，简化版）：

1. **`Utils/RegistryHelper.cs`** - 注册表读写操作
   - `GetValue()`, `SetValue()`, `IsMatch()`, `DeleteValue()`, `KeyExists()`, `MergeRegFile()`

2. **`Utils/CommandPromptHelper.cs`** - CMD 命令执行
   - `RunCommand()`, `RunCommandAsync()` - 执行命令并返回输出

3. **`Utils/ProcessHelper.cs`** - 进程管理辅助
   - 以管理员权限运行脚本、检查进程等

### 第 4 步：实现服务层 (Services/)

每个功能对应一个 Service，统一实现 `IConfigurationService` 接口：

```csharp
public interface IConfigurationService
{
    bool IsEnabled();
    void Enable();
    void Disable();
}
```

#### 4.1 软件安装服务 (`Services/SoftwareInstallService.cs`)
- 使用 winget 命令行安装软件
- 支持批量安装、进度反馈
- 预定义常用软件列表（浏览器、开发工具、媒体播放器等）

#### 4.2 系统激活服务 (`Services/ActivationService.cs`)
- 调用 MAS (Microsoft Activation Scripts) 的 IRM 命令
- `irm https://get.activated.win | iex`
- 检测当前激活状态 via `slmgr.vbs`

#### 4.3 Windows 更新管理服务 (`Services/WindowsUpdateService.cs`)
- 暂停更新: 修改注册表 `HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate`
- 启用更新: 恢复注册表设置
- 检查当前更新状态

#### 4.4 Edge 卸载服务 (`Services/EdgeRemoverService.cs`)
- 使用 CMD 调用 Edge 卸载命令
- 检查 Edge 是否已安装
- 清理残留

### 第 5 步：实现数据模型 (Models/)

1. **`Models/SoftwareItem.cs`** - 软件项模型（名称、winget ID、图标、类别、是否选中）
2. **`Models/Configuration.cs`** - 配置项模型（名称、Key、描述、类型、图标）
3. **`Models/ToolboxConfig.cs`** - 工具箱配置（当前状态等）

### 第 6 步：实现 ViewModel 层

1. **`ViewModels/HomePageViewModel.cs`** - 首页数据（快捷操作入口）
2. **`ViewModels/SoftwarePageViewModel.cs`** - 软件列表、选中状态、安装命令
3. **`ViewModels/ConfigPageViewModel.cs`** - 通用配置页（更新管理、Edge 卸载等）
4. **`ViewModels/SettingsPageViewModel.cs`** - 设置页（关于信息）
5. **`ViewModels/ConfigurationItemViewModel.cs`** - 单个配置项 VM（名称、描述、当前状态）

### 第 7 步：实现自定义控件 (Controls/)

参考 Atlas Toolbox 复刻：

1. **`Controls/HomePageHeaderImage.xaml`** - 首页横幅背景（渐变色 + 图片叠加）
2. **`Controls/TileGallery.xaml`** - 水平滚动快捷入口卡片
3. **`Controls/HeaderTile.xaml`** - 单个快捷入口卡片

### 第 8 步：实现资源字典 (Resources/)

1. **`Resources/ConfigurationItemTemplate.xaml`** - 统一的卡片样式
   - `ConfigurationSettingsCardTemplate` - SettingsCard 样式
   - `ConfigurationSettingsExpanderTemplate` - SettingsExpander 样式

### 第 9 步：实现主窗口 (MainWindow.xaml)

仿照 Atlas Toolbox 的主窗口结构：
- **TitleBar** - 自定义标题栏（图标 + 应用名 + 搜索框）
- **NavigationView** - 左侧导航菜单
  - Home（首页）
  - Software（软件安装）
  - System Activation（系统激活）
  - Windows Update（更新管理）
  - Edge Remover（Edge 卸载）
  - Footer: Settings（设置）
- **ContentFrame** - 右侧内容区域
- **Mica 背景效果**

### 第 10 步：实现各功能页面 (Views/)

#### 10.1 首页 (`Views/HomePage.xaml`)
- 横幅 Banner (HomePageHeaderImage)
- TileGallery 快捷操作卡片
- 各功能模块的快捷入口

#### 10.2 软件安装页 (`Views/SoftwarePage.xaml`)
- 参考 Atlas Toolbox SoftwarePage 布局
- 软件列表（SettingsCard + CheckBox）
- 底部安装按钮 + 进度条
- 按类别分组：浏览器、开发工具、媒体工具、系统工具等

预置软件列表：
| 类别 | 软件 | winget ID |
|------|------|-----------|
| 浏览器 | Google Chrome | Google.Chrome |
| 浏览器 | Mozilla Firefox | Mozilla.Firefox |
| 开发工具 | Visual Studio Code | Microsoft.VisualStudioCode |
| 开发工具 | Git | Git.Git |
| 开发工具 | Windows Terminal | Microsoft.WindowsTerminal |
| 媒体 | VLC | VideoLAN.VLC |
| 系统工具 | 7-Zip | 7zip.7zip |
| 系统工具 | PowerToys | Microsoft.PowerToys |
| 其他 | Telegram | Telegram.TelegramDesktop |

#### 10.3 系统激活页 (`Views/ActivationPage.xaml`)
- 当前激活状态显示
- "一键激活" 按钮（执行 MAS 命令）
- 激活进度/结果显示

#### 10.4 Windows 更新页 (`Views/WindowsUpdatePage.xaml`)
- 当前更新状态
- ToggleSwitch: 暂停/恢复更新
- 更新延迟设置（ComboBox: 1周/2周/5周等）

#### 10.5 Edge 卸载页 (`Views/EdgeRemoverPage.xaml`)
- 当前 Edge 安装状态
- 卸载按钮 + 确认对话框
- 卸载进度显示

#### 10.6 设置页 (`Views/SettingsPage.xaml`)
- 关于信息
- 版本号
- GitHub 链接

### 第 11 步：实现 App.xaml.cs 基础设施

- DI 容器配置（HostBuilder）
- 多语言支持框架（简化版，仅中文）
- 全局异常处理
- 日志配置

### 第 12 步：更新 App.xaml

- 引入资源字典
- 配置自定义字体
- 全局样式覆盖

### 第 13 步：测试与验证

- 编译项目确认无错误
- 验证各功能页面导航正常
- 验证基本 UI 布局与 Atlas Toolbox 一致
- 确认管理员权限执行场景的处理

---

## 关键实现细节

### 管理员权限
由于涉及系统级操作（注册表修改、软件安装、系统激活），应用需要以管理员权限运行。在 `app.manifest` 中配置 `requestedExecutionLevel` 为 `requireAdministrator`。

### 软件安装实现
使用 `winget install --id <ID> --accept-package-agreements --accept-source-agreements` 命令批量安装，通过 `Process` 类启动并监控进度。

### 系统激活实现
执行 PowerShell 命令：`irm https://get.activated.win | iex`，或者通过 `irm https://massgrave.dev/Get | iex` 调用 MAS 脚本。

### 更新暂停实现
修改注册表键值：
- `HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU` -> `SetAutoUpdate = 0`
- `HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate` -> 禁用更新相关策略

### Edge 卸载实现
执行命令：`"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe" --uninstall --system-level --verbose-logs --force-uninstall`
