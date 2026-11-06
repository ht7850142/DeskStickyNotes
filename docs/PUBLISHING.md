# DeskStickyNotes 发布渠道与文案

适用版本：**0.6.0**。渠道资料核对日期：**2026-11-06**。

建议先做 **小众软件发现频道 → V2EX 分享创造 → WinGet → Product Hunt**。前两者收集真实 Windows 用户反馈，WinGet 改善长期分发，Product Hunt 使用英文材料集中介绍产品。先后顺序是针对本项目的判断，不代表平台承诺曝光或收录。V2EX 本轮访问遇到安全验证，发帖前需人工复核节点与版规。

当前官方入口为 [GitHub 项目](https://github.com/ht7850142/DeskStickyNotes) 和 [0.6.0 Release](https://github.com/ht7850142/DeskStickyNotes/releases/tag/v0.6.0)。本轮整理了素材和稿件，**没有向以下第三方平台提交**。

## 1. 渠道选择

| 优先级 | 渠道 | 适合本项目的原因 | 需要准备 / 本轮核实情况 |
| --- | --- | --- | --- |
| 已完成 | GitHub Releases | 源码、安装包、便携包与更新记录的统一入口 | 0.6.0 已发布，四种程序包和校验清单齐全。新的分享封面已准备；Social preview 需在仓库设置上传。 |
| P1 | [小众软件发现频道](https://meta.appinn.net/c/faxian/10) | 面向实际使用桌面工具的中文用户 | 按官方模板填写软件名、平台、是否收费，标明“开发者自荐”。[模板](https://meta.appinn.net/t/topic/77583)已读取，中文稿在下方。 |
| P1 候选 | [V2EX 分享创造](https://www.v2ex.com/go/create) | 适合介绍开发动机、交互细节与开源实现 | 准备原创过程和体验入口。本轮页面遇访问验证，尚未复核当前版规和账号发帖资格，不据此保证能直接发帖。 |
| P1 | [WinGet 社区仓库](https://learn.microsoft.com/en-us/windows/package-manager/package/repository) | Windows 用户可用标准包管理器发现和安装 | 提交 manifest，而非重新上传全部源码；使用固定版本下载 URL、SHA-256、静默安装参数，并做安装/卸载及 Sandbox 测试。文档要求校验和审核，当前未提交。 |
| P1/P2 | [Product Hunt](https://help.producthunt.com/en/articles/479557-how-to-post-a-product) | 七种界面语言、开源与本地保存，适合一次完整英文介绍 | 使用个人账号，新账号需有投稿权限。官网允许自己发布、无需找 hunter。已准备 240 方图及 1270×760 英文图集；名称只写产品名，tagline ≤60 字符、description ≤500 字符。 |
| P2 候选 | [AlternativeTo](https://alternativeto.net/faq/) | 接触寻找 Windows Sticky Notes 替代品的人 | 官网 FAQ 搜索摘要显示有建议新应用的入口；正文遇安全验证。本轮未完成入口及审核规则复核，先保留候选，不宣称已收录。 |
| P2 | [Scoop Extras](https://github.com/ScoopInstaller/Extras) | 适合愿意用命令行安装 GUI 工具的用户 | 用自包含 ZIP 评估 manifest；[贡献指南](https://github.com/ScoopInstaller/.github/blob/main/.github/CONTRIBUTING.md)要求先提议题并等待维护者认可，再按 manifest 流程测试、贡献。不能写成现在已可 `scoop install deskstickynotes`。 |
| P2 | [itch.io](https://itch.io/docs/creators/getting-started) | 可作为独立工具下载页，当前受众契合度低于前几项 | 官方支持非游戏内容，可用 Other 与自定义名称；封面 315:250，推荐 630×500，截图建议 3–5 张。准备免费工具说明和 Windows 自包含 ZIP。 |
| P2/P3 | [SourceForge](https://sourceforge.net/create/) | 开源目录与备用下载镜像 | 官方提供开源项目下载分发、截图和目录展示，也可从 GitHub 导入。若采用，需要维护版本与校验值，避免出现长期过期的第二下载入口。 |
| 后续 | [Microsoft Store](https://learn.microsoft.com/en-us/windows/apps/distribute-through-store/how-to-distribute-your-win32-app-through-microsoft-store) | Windows 用户更熟悉的分发入口 | 可评估 MSIX，或现有 EXE/MSI 路线。后者要求受信任 CA 证书签名、离线安装器和固定不变的版本链接；当前包未签名。MSIX 路线需另外验证自启、托盘、数据与升级行为，本轮没有准备上架包。 |
| 选择性尝试 | [Show HN](https://news.ycombinator.com/showhn.html) | 适合讨论本地软件和原生窗口行为的工程实现 | 指南要求是自己持续参与、用户可试用的作品，不适合仅发小版本更新或快速生成的一次性作品。以完整项目和层级闪烁修复的工程过程为主题，发布后能回答问题再考虑。 |

WinGet、Scoop 是分发与维护渠道，不等同于新品流量平台；社区帖需要作者参与反馈。建议先用 GitHub 承接下载，不必为了第一轮发布再搭建一个新官网。

## 2. 素材与规格

素材目录：[assets/banners/desksticky-0.6.0/](../assets/banners/desksticky-0.6.0/)，[HTML 图库](../assets/banners/desksticky-0.6.0/press-kit.html)，[全部成图](../assets/banners/desksticky-0.6.0/exports/)。

| 用途 | 已准备尺寸 | 使用建议与依据 |
| --- | --- | --- |
| GitHub Social preview | 1280×640，中英文各一张 | [GitHub 官方文档](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/customizing-your-repositorys-social-media-preview)建议此尺寸，图片需小于 1 MB。 |
| Product Hunt 图集 | 1270×760，中英文各五张 | [发布指南](https://www.producthunt.com/launch/preparing-for-launch)推荐此尺寸，至少两张才显示图集；英文建议选封面、快捷键、托盘、多语言四张。 |
| Product Hunt 缩略图 | 240×240 | 同一官方指南推荐正方形 240×240，图片小于 3 MB。 |
| itch.io 封面 | 630×500 | [创建页面指南](https://itch.io/docs/creators/getting-started)推荐的高分辨率 315:250 比例。 |
| 通用横图 / 竖图 | 1920×1080 / 1080×1440 | 用于文章和社交分享，不宣称自动满足所有平台的裁切及商店素材规范。 |

另有 24 张原始界面图：中英文便签、折叠图标与托盘，以及全部七种语言的设置和快捷键界面。WPF 图按 2× 渲染，避免把旧低分辨率截图直接放大。所有图均使用示例数据，海报有“真实控件与示例内容”标注。

## 3. 公共字段

| 字段 | 内容 |
| --- | --- |
| 产品名 | DeskStickyNotes |
| 中文一句话 | 把灵感留在手边：支持置顶、折叠和全局快捷键的开源 Windows 便签。 |
| English tagline | Quiet desktop sticky notes for Windows |
| 平台 | Windows 10/11 x64 |
| 价格与许可 | 免费，MIT 开源 |
| 官网 / 源码 | https://github.com/ht7850142/DeskStickyNotes |
| 下载 | https://github.com/ht7850142/DeskStickyNotes/releases/tag/v0.6.0 |
| 推荐包 | DeskStickyNotesSetup-0.6.0-runtime.exe；免安装可选自包含 runtime ZIP |
| 界面语言 | 简体中文、繁體中文、English、Français、Español、Português、日本語 |

这些字段和限制说明也存为可复制的 [copy.json](../assets/banners/desksticky-0.6.0/copy.json)。不要将 Auto 算作第八种翻译。

## 4. 小众软件发现频道稿

**标题：**【开发者自荐】DeskStickyNotes：支持全局快捷键的开源 Windows 桌面便签

**软件名称：** DeskStickyNotes

**一句简介：** 把提醒留在眼前，用键盘快速新建与切换的 Windows 桌面便签。

**应用平台：** Windows 10/11 x64

**推荐类型：** 开发者自荐

**是否收费：** 免费，MIT 开源

**应用简介：**

想保留 Windows 7 便签那种随手记、留在桌面上的感觉，所以做了 DeskStickyNotes。它用原生 WPF 实现，没有账号、云同步、广告或遥测，便签保存在本机。

- 每张便签可单独置顶，也可折叠成一个可拖动的小图标。
- `Ctrl+Alt+N` 快速新建，`Ctrl+Alt+PageDown` 循环切换；快捷键可以自定义，隐藏、最小化和折叠的便签也能唤出。
- 支持富文本、待办事项、图片和长文本切片，可拖入文本文件保存快照。
- 从右下角托盘管理每张便签，支持七种界面语言。
- 提供安装包与自包含绿色版。

最近一轮主要优化了多个重叠便签争抢置顶造成的闪烁，以及设置、托盘和键盘操作。欢迎试用后反馈快捷键、窗口切换和小尺寸编辑是否顺手。

说明：Win+D 后恢复仍是尽力而为；安装版和绿色版的数据都在 `%AppData%/DeskStickyNotes`。开发中有 AI 辅助，源码和维护中的开发规格均公开。

**配图顺序：** `zh-hero` → `zh-shortcuts` → `zh-tray`，按需加 `zh-languages`。

**官方网站：** [GitHub 项目](https://github.com/ht7850142/DeskStickyNotes)

**下载：** [0.6.0 Release](https://github.com/ht7850142/DeskStickyNotes/releases/tag/v0.6.0)

## 5. V2EX 分享创造稿（待人工复核节点规则）

**标题：** 我做了一个开源 Windows 桌面便签，支持自定义快捷键和折叠图标

希望有个不用账号、留在桌面上就能随手记录的小工具，于是做了 DeskStickyNotes，C# / WPF / .NET 8，MIT 开源，Windows 10/11 x64。

普通便签不占任务栏，可单独置顶，或折叠成可拖动的图标。默认 `Ctrl+Alt+N` 新建、`Ctrl+Alt+PageDown` 依次切换，组合键可以改。编辑器支持待办、富文本、图片和长文本切片，界面目前有七种语言。

这次比较花心思的是窗口层级：之前多个便签各自尝试在显示桌面后恢复，容易反复互相置顶。现在改成应用级协调，合并通知、按激活顺序恢复并限制重试，桌面空闲时不反复写层级。另有原生窗口与快捷键回归检查；真实 Win+D 的完整行为仍按尽力而为处理。

项目没有账号、广告、同步和遥测，提供安装包与自包含 ZIP。开发有 AI 协助，源码和开发规格可查看。欢迎反馈实际使用中的窗口切换、DPI 和快捷键体验。

源码：https://github.com/ht7850142/DeskStickyNotes

下载：https://github.com/ht7850142/DeskStickyNotes/releases/tag/v0.6.0

## 6. Product Hunt 英文稿

**Name:** DeskStickyNotes

**Tagline:** Quiet desktop sticky notes for Windows

**Description:**

Free, open-source sticky notes for Windows 10/11. Keep reminders visible with per-note pinning and compact icons, capture rich text and text snippets, and create or cycle notes with customizable global shortcuts. Seven UI languages. Local storage, no account, and no cloud sync. Installer and portable builds.

**Maker first comment draft:**

Hi everyone! I wanted a small place for reminders that could stay on the Windows desktop without becoming another full workspace. That became DeskStickyNotes.

You can pin individual notes, fold them into small draggable reminders, and create or cycle notes with customizable global shortcuts. Hidden and collapsed notes can be brought back from the keyboard. The editor supports rich text, to-do items, images, and text snippets, and the interface is available in seven languages.

It is free and MIT-licensed, stores notes locally, and needs no account. There are installer and self-contained portable builds. The project was developed with AI assistance; its source and maintained development specification are public.

One area I worked on was overlapping windows repeatedly competing for the top position. Recovery is now coordinated across notes, with bounded retries and no continuous idle reordering. Windows Show Desktop recovery remains best effort.

I'd love feedback on keyboard ergonomics, tray behavior, and how the notes fit into your Windows workflow.

**URL:** https://github.com/ht7850142/DeskStickyNotes

**Pricing:** Free

**Candidate tags:** Productivity / Open Source / Note-taking（按实际表单中可选标签选择，最多三项）

**Gallery:** `en-hero` → `en-shortcuts` → `en-tray` → `en-languages`

## 7. Show HN 备选稿

**Title:** Show HN: DeskStickyNotes – Local Windows sticky notes with global shortcuts

I built a small native Windows sticky-note app in C# and WPF. Notes can stay pinned, collapse into draggable reminders, and be created or cycled with customizable global hotkeys. It is MIT-licensed, stores data locally, and has no account or cloud-sync requirement.

The window-management work may be interesting: independent Show Desktop recovery timers could make overlapping notes repeatedly raise themselves. The app now uses one coordinator, remembered activation order, coalesced notifications, and bounded retries. There are native HWND and hotkey registration/conflict checks, while full Explorer/Win+D behavior is still best effort.

The project uses AI assistance, and its source and development specification are public. Windows 10/11 x64 builds, including a self-contained ZIP, are available in Releases. I'm interested in feedback on the window behavior and keyboard workflow.

Link: https://github.com/ht7850142/DeskStickyNotes

此稿仅适合首次完整介绍项目或有实质工程内容的分享；不要把每个小版本更新都当成新的 Show HN。

## 8. 发布顺序与维护

1. **基础入口：** 使用现有 GitHub 介绍和 Release；选 `en-social` 作为仓库分享封面，保留可直接下载的入口。
2. **中文反馈：** 小众软件先发一篇认真介绍，留时间回复；V2EX 先核对版规再决定。根据反馈修正文案和常见问题。
3. **稳定分发：** 准备 WinGet manifest，测试静默安装、卸载和依赖。Scoop 走先提议题、再贡献的流程；两者都要等待审核，不能提前宣传为已上架。
4. **英文发布：** 用已准备的英文图集和字段创建 Product Hunt 草稿，确认作者当天有时间参与讨论后再排期。
5. **后续：** 按真实下载需求选择 itch.io / SourceForge，评估 Microsoft Store 的签名或 MSIX 路线。不需要第一天铺满所有平台。

发布后记录渠道 URL、日期、使用版本、反馈问题及下一次更新责任。所有镜像的版本与 SHA-256 必须对应官方包。后续修复发新版本，避免替换同一版本 URL 下的程序文件。

## 9. 对外说明边界

- 当前支持 Windows 10/11 x64，不宣传 macOS、Linux、手机或网页版。
- 七种语言指界面翻译；宣传图和文案本轮为中英文。
- “本地保存”不等于加密保险箱，也不等于云备份。绿色版与安装版共用 AppData 数据位置。
- 不承诺所有情况下 Win+D 永不隐藏、所有杀毒软件零误报，或已获商店安全认证。
- 已发布程序当前未签名；Microsoft Store 上架要按所选分发路线另行准备。
- 不编造用户量、排名、编辑推荐、测评结论或性能对比，不要求他人刷赞。
