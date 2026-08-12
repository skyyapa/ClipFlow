# ClipFlow

> 剪贴板领域的 Everything——呼出即用，用完即走。

ClipFlow 是一款面向 Windows 的轻量级本地剪贴板管理器。它在后台记录复制过的文字、富文本和图片，并提供即时搜索、收藏、纯文本粘贴与快捷键呼出。

当前版本：**0.10.0 原型版**

## 功能特点

- 记录纯文本、HTML 和 RTF 内容
- 支持 QQ、微信截图及常见 PNG/Bitmap 图片
- 支持 Windows 文件和文件夹列表，可搜索并再次粘贴
- 文件卡片显示关联图标，可打开文件或所在位置
- 图片以缩略图显示，并可再次粘贴
- 使用 `Ctrl + Shift + V` 全局呼出
- 输入关键词即时搜索
- 收藏、删除和清空未收藏记录
- 相同内容自动去重并更新时间
- 使用 SQLite + FTS5 存储和检索历史
- 兼顾全文索引与中文子串模糊搜索
- 首次启动自动迁移旧版 XML 历史
- 历史记录持久化，重启后不会清空
- 默认最多保留 5,000 条，收藏内容不会被自动清理
- 托盘运行、暂停记录和单实例保护
- 自动定位到当前屏幕右下角任务栏上方
- 支持多显示器和 Per-Monitor V2 高 DPI
- 接近 Windows 11 原生剪贴板的紧凑界面
- 右侧滚动条可用鼠标拖动，且使用独立轨道避免遮挡卡片
- 托盘菜单提供单页设置窗口
- 支持开机自启、自定义快捷键和历史容量设置
- 支持图片保留天数和图片空间上限
- 自动清理没有历史记录引用的孤立 PNG 文件
- 文字卡片提供鼠标可点的纯文本粘贴按钮

## 下载与启动

### 直接运行

1. 下载 [`dist/ClipFlow.exe`](dist/ClipFlow.exe)。
2. 双击运行。程序启动后会驻留在系统托盘。
3. 按 `Ctrl + Shift + V` 呼出剪贴板面板。

也可以下载整个仓库后双击 `start-clipflow.cmd`。

> ClipFlow 当前没有代码签名。Windows 首次运行时可能显示 SmartScreen 提示，请只从本仓库下载。

### 从源码构建

在 Windows PowerShell 中运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

构建结果位于 `dist\ClipFlow.exe`。项目使用 Windows 自带的 .NET Framework C# 编译器，目前不需要安装 Visual Studio 或额外 SDK。

## 使用方式

| 操作 | 快捷键 |
| --- | --- |
| 呼出 ClipFlow | `Ctrl + Shift + V` |
| 选择上一条或下一条 | `↑` / `↓` |
| 保留格式粘贴 | `Enter` |
| 纯文本粘贴 | `Shift + Enter` |
| 收藏或取消收藏 | `Ctrl + S` |
| 删除当前记录 | `Delete` |
| 隐藏窗口 | `Esc` |

窗口顶部空白区域支持鼠标拖动。托盘菜单可暂停记录、清空未收藏历史或退出程序。

## 数据与隐私

ClipFlow 默认完全在本地运行，不上传剪贴板内容，也不需要账户。

- 历史数据库：`%LocalAppData%\ClipFlow\clipflow.db`
- 图片文件：`%LocalAppData%\ClipFlow\images`
- 旧版迁移备份：`%LocalAppData%\ClipFlow\history.xml.migrated-backup`

界面一次最多展示 100 条搜索结果。历史记录默认最多保留 5,000 条；收藏内容不会因容量限制被自动删除，因此收藏较多时总数可能超过 5,000 条。

## 当前限制

- 仅支持 Windows
- 暂无连续粘贴队列
- 暂无多行文本转 Excel/TSV 预览
- 暂无按应用排除和敏感内容识别
- 当前版本未进行代码签名

## 路线图

- 连续粘贴队列
- 多行文本转 TSV 的表格预览
- 来源应用和内容类型筛选
- 敏感内容规则与应用排除

## 项目文档

- [产品规格](docs/PRODUCT_SPEC.md)
- [MVP 与路线图](docs/MVP.md)
- [技术方案](docs/TECHNICAL_DESIGN.md)
- [版本说明](版本说明.md)

## 反馈

如果遇到图片不显示、快捷键冲突或多屏定位问题，请在 [Issues](https://github.com/skyyapa/ClipFlow/issues) 中提交问题，并附上 Windows 版本、显示缩放比例和复现步骤。
