# ClipFlow 0.7.6 技术说明

本文记录当前代码的真实实现。SQLite、连续粘贴和文件记录属于后续设计，不是 0.7.6 的现有能力。

## 1. 技术栈

- C# + WPF
- .NET Framework 系统程序集
- Windows User32、DWM 和 GDI 原生 API
- `DataContractSerializer` XML 持久化
- PNG 文件存储图片
- Windows Forms `NotifyIcon` 提供系统托盘入口

`build.ps1` 调用 Windows 自带的 64 位 C# 编译器生成 AnyCPU GUI 程序，因此当前不要求 Visual Studio 或额外 .NET SDK。

## 2. 源码结构

```text
src/ClipFlow
├─ Program.cs          程序入口、单实例互斥锁、WPF 生命周期
├─ MainWindow.cs       窗口、监听、搜索、粘贴、托盘和交互
├─ HistoryStore.cs     XML 持久化、查询、去重与容量清理
├─ ClipboardItem.cs    历史数据模型、图片解码和预览字段
├─ NativeMethods.cs    User32、DWM 与 GDI 调用
└─ app.manifest        Windows 兼容性与 Per-Monitor V2 DPI
```

## 3. 运行生命周期

1. `Program.Main` 获取 `Local\ClipFlow.SingleInstance` 互斥锁。
2. 创建隐藏的 `MainWindow`，设置显式退出模式。
3. 注册剪贴板监听和 `Ctrl + Shift + V` 全局快捷键。
4. 显示托盘图标，主窗口保持隐藏。
5. 退出时注销监听、快捷键和消息钩子，并释放托盘资源。

如果快捷键被其他程序占用，ClipFlow 会显示托盘通知，用户仍可通过托盘打开窗口。

## 4. 剪贴板捕获

- 使用 `AddClipboardFormatListener` 接收 `WM_CLIPBOARDUPDATE`
- 收到更新后延迟约 70ms 读取，避免与来源应用争用剪贴板
- 遇到剪贴板占用最多重试 4 次，并逐步增加间隔
- 优先读取图片，再读取 Unicode 文本、RTF 和 HTML
- 记录复制时的前台进程名和窗口标题
- 写回内容时设置 2 秒内部标记，防止再次记录自己的粘贴

### 图片兼容

图片读取覆盖：

- WPF `Clipboard.GetImage()`
- `PNG` 和 `image/png` 数据流
- PNG 字节数组
- `System.Drawing.Image`
- `DataFormats.Bitmap`

图片在写入磁盘前转换为 Bgr32。这样会忽略 QQ/微信截图中可能全为零的 Alpha 通道，避免缩略图显示成灰色或透明块。

## 5. 数据模型与存储

`ClipboardItem` 当前包含：

- ID、内容类型
- 纯文本、RTF、HTML
- 图片路径、SHA-256、宽高
- 来源进程与窗口标题
- 创建时间、最近使用时间
- 使用次数、复制次数和收藏状态

数据默认位于：

```text
%LocalAppData%\ClipFlow
├─ history.xml
└─ images\<sha256>.png
```

可通过 `CLIPFLOW_DATA_DIR` 环境变量覆盖数据目录。保存历史时先写入 `history.xml.tmp`，再替换正式文件，降低写入中断造成的损坏风险。

### 去重与清理

- 文本按完整 Unicode 文本精确去重
- 图片按转换后 PNG 的 SHA-256 去重
- 重复内容更新创建时间、来源和复制次数
- 超过 5,000 条时删除最早的未收藏记录
- 图片记录删除后，如果没有其他记录引用同一路径，则同时删除 PNG 文件
- 收藏记录不参与自动清理，所以总记录数可能超过 5,000

## 6. 搜索与排序

当前搜索在内存列表中执行：

- 多个空格分隔关键词采用 AND 匹配
- 匹配纯文本和来源应用名，不区分大小写
- 图片可通过“图片、截图、image、screenshot”等词匹配
- 收藏优先，其次按最近复制时间倒序
- 每次最多返回 100 条结果

当前没有全文索引。记录数量和查询复杂度进一步增加后，计划迁移到 SQLite FTS5。

## 7. 粘贴流程

1. 呼出窗口时保存此前的前台窗口句柄。
2. 选择历史记录并写回系统剪贴板。
3. 普通粘贴保留 RTF/HTML；纯文本粘贴只写文本格式。
4. 图片从 PNG 文件解码为 Bgr32 后写回剪贴板。
5. 隐藏 ClipFlow，等待约 90ms。
6. 恢复原窗口并发送 `Ctrl + V`。

普通权限程序无法保证向管理员权限窗口发送输入，这是 Windows 权限边界造成的限制。

## 8. 窗口与显示

- 无边框、暖白实体背景和 DWM 圆角
- 不启用亚克力或系统背景材质，避免 WPF 无边框窗口出现模糊和多余底板
- 每次呼出按鼠标位置选择显示器
- 使用目标屏幕工作区物理坐标，将窗口放在右下角并保留边距
- 使用 Per-Monitor V2 DPI 清单、布局取整、像素对齐和 ClearType
- 列表使用像素滚动，标准滚轮一格约 20px
- 系统滚动条隐藏，使用独立的 3px 浮动指示器

## 9. 当前技术债务

- XML 每次修改都整体保存，数据量增大后写入成本会上升
- 搜索为内存线性扫描，没有 FTS 索引
- 图片编码和历史保存仍在 UI 线程路径中
- 没有可配置的快捷键、容量和保留策略
- 没有文件列表、连续粘贴和文本转换模块
- 缺少安装包、代码签名、自动升级和完整自动化测试

## 10. 测试重点

- Windows 10/11 与常见 DPI 缩放
- 多显示器、负坐标和不同缩放组合
- 浏览器、Office、WPS、微信、QQ 和 IDE
- 大图片、大段 HTML 与剪贴板竞争
- 管理员窗口、远程桌面、休眠恢复和资源管理器重启
- 历史文件损坏、图片文件丢失和磁盘空间不足

