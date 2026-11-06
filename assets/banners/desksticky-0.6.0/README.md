# DeskStickyNotes 0.6.0 发布素材

中英文发布图、原始界面图、产品资料及可编辑源文件。准备日期：2026-11-06。

双击 [press-kit.html](press-kit.html) 查看图库；成图都在 [exports/](exports/)，原始界面图在 [raw/](raw/)，可复制的基本字段在 [copy.json](copy.json)。渠道建议和完整稿件见项目的 [发布指南](../../../docs/PUBLISHING.md)；下载的独立 ZIP 内另附同名 `PUBLISHING.md`。

## 已交付内容

| 内容 | 数量/尺寸 | 用途 |
| --- | --- | --- |
| 中英文封面与四张功能图 | 10 张，1270 × 760 | Product Hunt 图集、社区帖子、产品介绍 |
| 中英文分享封面 | 2 张，1280 × 640 | GitHub Social preview、横向分享 |
| 中英文通用横图 | 2 张，1920 × 1080 | 文章封面、演示配图；不代表已符合商店上架审核 |
| 中文竖版海报 | 1 张，1080 × 1440 | 通用竖向分享素材，具体平台裁切需在上传时检查 |
| itch.io 英文封面 | 1 张，630 × 500 | 对应其 315:250 封面比例 |
| 正方形图标 | 240 × 240、512 × 512 各 1 张 | Product Hunt 缩略图、目录图标 |
| 原始界面图 | 24 张 | 中英文便签、折叠图标、托盘，以及七种语言的设置/快捷键界面 |
| 可编辑源文件 | HTML/CSS/JS + 导出脚本 | 改文案、调尺寸并重新导出 |

所有发布成图为 PNG，尺寸及字节数记录在 [exports/manifest.json](exports/manifest.json)。透明原始图可直接用于其他排版，`logo.png` 是原有应用图标。没有新增未经验证的用户量、排名或性能宣传。

## 推荐组合

- 中文社区：`zh-hero`、`zh-shortcuts`、`zh-tray`，按需补 `zh-languages`。
- Product Hunt：`en-hero`、`en-shortcuts`、`en-tray`、`en-languages`，缩略图用 `thumbnail-240x240.png`。
- GitHub 分享封面：`en-social-1280x640.png`，中文分享可用对应 `zh-social`。
- itch.io：`en-itch-cover-630x500.png` 加三至五张英文功能图，应用分类使用合适的工具/Other 类型。

规格依据核对自 [Product Hunt 发布指南](https://www.producthunt.com/launch/preparing-for-launch)、[GitHub 社交预览文档](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/customizing-your-repositorys-social-media-preview)、[itch.io 创建页面指南](https://itch.io/docs/creators/getting-started)。具体字段和审核以发布时的界面为准。

## 修改与再生成

`press-kit.html` 中的 `messages.zh`、`messages.en` 是宣传文案；同文件 CSS 维护排版与尺寸。带 `scene`、`lang`、`format` 参数可单独查看画面，例如 `?scene=shortcuts&lang=en`。

在仓库根目录重新生成 2× 原始 WPF 图：

```powershell
dotnet run --project tests/DeskStickyNotes.RegressionTests -c Release -- --render-press-assets assets/banners/desksticky-0.6.0/raw
```

读取当前 `ego-browser` 技能并建立本次任务的一个 TaskSpace 后，通过 [export-ego.mjs](export-ego.mjs) 的 `exportPressKit({spaceId, assetDirectory})` 导出 18 张成图。它复用调用者的 TaskSpace，不启动额外浏览器；目录须传绝对路径。导出前会等待字体和图片解码及绘制帧，并检查画布尺寸、文字横向溢出和竖图文案遮挡。

## 素材使用说明

- 这些图片来自 0.6.0 编译后的真实 WPF/WinForms 控件和示例内容，不是私人桌面的实机截屏；功能海报会裁切或组合这些界面。
- `zh` 指简体中文宣传文案，`en` 指英文宣传文案。七种界面语言的设置原图均已提供，不意味着宣传文案也已翻译为七种语言。
- 安装版和绿色版数据都位于 `%AppData%/DeskStickyNotes`。不要宣传为云同步、跨平台或数据随程序目录移动。
- 新版图标和原创排版沿用项目 MIT 许可；文字没有包含第三方评价或未授权品牌背书。
- 素材已准备，本轮未向第三方平台发帖、申请上架或创建账户。
