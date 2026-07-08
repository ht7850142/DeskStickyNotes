# DeskStickyNotes

DeskStickyNotes 是一个轻量、开源、类似 Windows 7 风格的 Windows 10/11 桌面便签工具。

它面向希望“便签一直在工作流里可见”的用户：桌面优先、单个便签可置顶、可折叠成可拖动小图标、托盘管理、本地保存，并使用原生 WPF 界面。

English: [README.md](README.md)

## AI 辅助开发

本项目的初始代码、UI 迭代、文档和打包流程由 AI 辅助生成，并经过本地构建和手动验证。原始中文开发提示词保留在 [docs/开发提示词.md](docs/开发提示词.md)。

## 项目定位

Windows 11 自带 Sticky Notes 更像普通应用窗口。DeskStickyNotes 的定位更窄：让便签出现在桌面上，并且在需要时可以置顶保持可见。

- 桌面优先的便签窗口
- 单个便签可置顶
- 可折叠为小图标，减少遮挡
- 系统托盘管理
- 本地 JSON 保存
- 支持安装包和绿色版
- 简单富文本，而不是大型笔记软件

## 优势和强项

- **Windows 11 上可见的桌面便签**：便签更像轻量桌面组件，而不是一个完整笔记工作区。
- **单个便签可置顶**：重要便签可以固定在其他窗口上方，适合临时任务、提醒、待办和参考信息。
- **折叠成小图标**：置顶便签不想占空间时，可以折叠成可拖动小图标，需要时再展开。
- **托盘管理，不占任务栏**：普通便签窗口不显示任务栏按钮，通过系统托盘新建、显示、隐藏、设置和退出。
- **本地优先，更私密**：便签以 JSON 保存在 `%AppData%`，没有账号、同步、广告、遥测或网络功能。
- **小而够用的富文本**：支持加粗、斜体、下划线、删除线、待办、标题、对齐、超链接和颜色。
- **安装包和绿色版都支持**：可选择小体积 framework-dependent 包，也可选择自包含/内置运行时包。
- **开源且 AI 辅助开发**：代码结构清楚，文档完整，并保留原始开发提示词。

## 适合谁

- 怀念 Windows 7 便签体验的 Windows 11 用户
- 希望重要便签始终显示在其他窗口上方的用户
- 希望用小图标保留置顶提醒、减少遮挡的用户
- 不想登录账号、不想同步云端的便签用户
- 想要托盘管理、桌面显示的轻量便签用户
- 想找开源 Windows sticky notes 工具的用户
- 想参考 WPF/.NET 桌面应用结构的开发者

## 功能

- 多个独立便签窗口
- 普通便签不显示任务栏按钮
- 系统托盘菜单：新建、显示全部、隐藏全部、设置、退出
- 双击托盘图标显示已有便签
- 自动保存内容、位置、大小、颜色、显示状态和置顶状态
- 富文本：加粗、斜体、下划线、删除线、列表、待办、标题、对齐、超链接
- 启动时恢复便签
- 单个便签可置顶
- 可折叠成可拖动的小图标
- 可选的 Win+D 后保持显示机制
- 中文和英文界面，支持自动识别语言
- 支持开机自启
- 数据保存在 `%AppData%\DeskStickyNotes`

## 下载安装

发布包建议放到 GitHub Releases。

| 包 | 适合场景 |
| --- | --- |
| `DeskStickyNotesSetup-*-fd.exe` | 已安装 .NET 8 Desktop Runtime，安装包较小 |
| `DeskStickyNotesSetup-*-runtime.exe` | 安装包内置 .NET 8 Desktop Runtime，体积较大 |
| `DeskStickyNotes-portable-*-fd.zip` | 绿色版，要求系统已有 .NET 8 Desktop Runtime |
| `DeskStickyNotes-portable-*-runtime.zip` | 自包含绿色版，不需要额外安装运行时 |

安装包支持自定义安装路径，也支持重新安装覆盖旧版本。

## 产品截图

### 便签窗口

![DeskStickyNotes Windows 11 桌面便签窗口，包含富文本和待办项](docs/images/note-window.png)

### 折叠图标模式

![DeskStickyNotes 可拖动的折叠便签图标](docs/images/collapsed-icon.png)

## 开发环境

- Windows 10/11
- .NET 8 Desktop SDK
- Inno Setup 6，仅打安装包时需要

安装 SDK：

```powershell
winget install Microsoft.DotNet.SDK.8
```

## 运行

```powershell
dotnet run --project .\src\DeskStickyNotes\DeskStickyNotes.csproj
```

程序会启动到系统托盘。如果没有任何旧便签，会自动创建一个默认便签。

## 构建

```powershell
dotnet build -c Release
```

## 打包

生成小安装包和绿色版：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package.ps1
```

生成全部安装包和绿色版：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package.ps1 -AllInstallers
```

当前输出：

```text
artifacts\installer\DeskStickyNotes-portable-0.2.7-win-x64-fd.zip
artifacts\installer\DeskStickyNotes-portable-0.2.7-win-x64-runtime.zip
artifacts\installer\DeskStickyNotesSetup-0.2.7-fd.exe
artifacts\installer\DeskStickyNotesSetup-0.2.7-runtime.exe
```

如果要生成内置运行时的安装包，需要把运行时安装器放在仓库根目录：

```text
windowsdesktop-runtime-8.0.28-win-x64.exe
```

这个文件不应该提交到 Git 仓库。

更多发布步骤见 [docs/RELEASE.md](docs/RELEASE.md)。

## 项目结构

```text
DeskStickyNotes.sln   Visual Studio / dotnet 解决方案
src/DeskStickyNotes/   WPF 应用源码
  Models/             数据模型和便签颜色
  Services/           存储、托盘、自启和多语言服务
  ViewModels/         MVVM 视图模型和命令
  Views/              WPF 窗口
  Localization/       英文和简体中文资源
  Assets/             应用图标
installer/            Inno Setup 安装脚本和安装器图片
scripts/              打包脚本
docs/                 架构、发布指南、开发提示词和图片
```

## 数据和隐私

本项目没有账号、同步、广告、遥测或统计。数据只保存在本机：

```text
%AppData%\DeskStickyNotes\notes.json
%AppData%\DeskStickyNotes\settings.json
```

## 已知限制

`Win+D 后保持` 是尽力而为。Windows 的“显示桌面”属于 Shell 级行为，普通应用窗口无法在所有场景下完全模拟桌面图标或桌面组件。需要稳定保持可见时，建议使用置顶或折叠图标模式。

## 参与贡献

欢迎提 issue 和 pull request。请保持项目方向克制：原生 Windows、小体积、托盘管理、本地优先。

见 [CONTRIBUTING.md](CONTRIBUTING.md)。

## 许可证

MIT License，见 [LICENSE](LICENSE)。
