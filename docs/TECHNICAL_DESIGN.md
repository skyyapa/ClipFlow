# ClipFlow 0.11.0 技术说明

本文记录当前代码的真实实现。连续粘贴、应用排除和敏感内容识别属于后续设计。

## 1. 技术栈

- C# + WPF
- Windows 自带 .NET Framework 编译器
- Windows `winsqlite3.dll` 与 SQLite FTS5
- Windows User32、DWM 和 GDI 原生 API
- PNG 文件存储图片
- Windows Forms `NotifyIcon` 提供系统托盘入口

项目直接调用 Windows 内置 SQLite C API，不需要附带第三方数据库 DLL。`build.ps1` 生成 AnyCPU GUI 程序，当前不要求 Visual Studio 或额外 .NET SDK。

## 2. 源码结构

```text
src/ClipFlow
├─ Program.cs          程序入口、单实例互斥锁、WPF 生命周期
├─ MainWindow.cs       窗口、监听、搜索、粘贴、托盘和交互
├─ HistoryStore.cs     SQLite 数据模型、搜索、迁移、去重与清理
├─ SqliteDatabase.cs   winsqlite3 P/Invoke、参数绑定和事务封装
├─ AppSettings.cs      XML 设置持久化与开机自启注册
├─ SettingsWindow.cs   最小单页设置窗口
├─ ClipboardItem.cs    历史对象、图片解码和预览字段
├─ NativeMethods.cs    User32、DWM 与 GDI 调用
└─ app.manifest        Windows 兼容性与 Per-Monitor V2 DPI
```

## 3. 运行生命周期

1. `Program.Main` 获取 `Local\ClipFlow.SingleInstance` 互斥锁。
2. 创建隐藏窗口并打开 SQLite 数据库。
3. 创建表、普通索引、FTS5 表和同步触发器。
4. 首次升级时自动导入旧 `history.xml`。
5. 注册剪贴板监听和 `Ctrl + Shift + V` 全局快捷键。
6. 退出时注销系统资源并关闭数据库连接。

数据库使用 WAL 日志、NORMAL 同步、外键检查和 3 秒 busy timeout。

## 4. SQLite 数据层

`items` 表保存：

- ID、内容类型
- 纯文本、RTF、HTML 和换行分隔的文件路径列表
- 图片路径、SHA-256、宽高
- 来源进程和窗口标题
- 创建时间、最近使用时间
- 使用次数、复制次数和收藏状态

主要索引：

- 图片 SHA-256 唯一索引
- 收藏状态与创建时间复合索引
- 来源应用索引
- `items_fts` FTS5 外部内容索引（正文、来源应用和文件路径）

FTS5 触发器在条目新增、更新和删除时同步全文索引。搜索同时使用 FTS5 前缀查询和参数化 `LIKE` 子串匹配：前者加速常规全文检索，后者保证中文片段和模糊子串仍能找到。

## 5. XML 自动迁移

如果检测到旧 `%LocalAppData%\ClipFlow\history.xml` 且尚未迁移：

1. 使用旧数据契约读取全部记录。
2. 在单个 SQLite 事务中执行 `INSERT OR IGNORE`。
3. 写入 `metadata.legacy_xml_imported` 标记，防止重复导入。
4. 执行 5,000 条容量策略。
5. 将 XML 保留为 `history.xml.migrated-backup`。

迁移失败时不会移动旧 XML；应用下次启动仍可重试。

## 6. 文件布局

```text
%LocalAppData%\ClipFlow
├─ clipflow.db
├─ clipflow.db-wal             运行时可能存在
├─ clipflow.db-shm             运行时可能存在
├─ history.xml.migrated-backup 旧版迁移备份（如有）
└─ images\<sha256>.png
```

可通过 `CLIPFLOW_DATA_DIR` 环境变量覆盖整个数据目录。

## 7. 去重与容量

- 文本按完整 Unicode 文本精确去重
- 图片转换为 Bgr32 PNG 后按 SHA-256 去重
- 重复内容更新复制时间、来源和复制次数
- 默认最多保留 5,000 条普通记录
- 超出容量时删除最早的未收藏记录
- 收藏记录不参与自动清理，因此总数可能超过 5,000
- 删除最后一个图片引用时同步删除对应 PNG

## 8. 剪贴板捕获与图片兼容

- 使用 `AddClipboardFormatListener` 接收 `WM_CLIPBOARDUPDATE`
- 延迟约 70ms 读取，剪贴板占用时最多重试 4 次
- 优先读取图片，再读取 Unicode 文本、RTF 和 HTML
- 支持 WPF 图片、PNG 流、PNG 字节、Drawing Image 和 Bitmap
- 转换为 Bgr32，修复 QQ/微信截图 Alpha 通道异常
- 写回内容后设置短期内部标记，避免复制循环

### 文件列表

- 使用 `Clipboard.ContainsFileDropList` 和 `GetFileDropList` 捕获路径
- 去除空路径和重复路径，保留原有顺序
- 路径集合按完整内容去重，并进入 FTS5 与子串搜索
- 粘贴时过滤已经移动或删除的路径，再调用 `SetFileDropList`
- 文件卡片通过 `ExtractAssociatedIcon` 显示首个文件的系统关联图标
- 提供打开首个项目和调用资源管理器 `/select` 定位的操作
- 每次展示时检查路径是否存在，区分完全失效和部分失效
- 主面板可筛选失效文件，批量清理时保留收藏记录
- 可为单个失效路径重新选择文件或文件夹并更新 FTS5 索引

### 后台存储

- Windows 剪贴板数据仍在 STA/UI 线程中快速读取
- 读取后的不可变快照进入单消费者后台队列，保持写入顺序
- 图片编码、SHA-256、PNG 写盘及 SQLite 新增/去重在后台线程执行
- 图片编码发生在数据库锁之外，搜索不会因大图编码被阻塞
- 退出程序时先排空存储队列，再关闭 SQLite 连接

## 9. 粘贴和窗口

呼出时记录原前台窗口。用户选择内容后，ClipFlow 写回目标格式、隐藏窗口、恢复原窗口并发送 `Ctrl + V`。普通粘贴保留 RTF/HTML，纯文本粘贴只写文本格式。

窗口使用暖白实体背景和 DWM 圆角，不启用亚克力。它按鼠标位置选择显示器，并使用工作区物理坐标定位到任务栏上方。应用清单启用 Per-Monitor V2 DPI；列表按像素滚动。4px 滑块位于独立的 10px 轨道，可直接用鼠标拖动，因此不会覆盖卡片内容。

## 10. 设置与图片清理

设置保存在 `%LocalAppData%\ClipFlow\settings.xml`。开机自启使用当前用户的 `Software\Microsoft\Windows\CurrentVersion\Run`，不需要管理员权限。

保存设置后，HistoryStore 在后台队列应用新的最大条数与图片策略。图片清理按以下顺序执行：

1. 删除超过保留天数的未收藏图片记录和无引用 PNG。
2. 统计剩余未收藏图片大小。
3. 若超过空间上限，从最早记录开始删除，直到回到上限以内。
4. 收藏图片不参与任何自动清理。

## 11. 当前技术债务

- FTS5 与模糊查询尚未提供可视化过滤语法
- 没有连续粘贴和文本转换模块
- 没有按来源应用排除和敏感内容规则
- 缺少安装包、代码签名、自动升级和完整自动化测试

## 12. 已验证范围

- 新数据库创建和 FTS5 索引创建
- 旧 XML 自动迁移与备份
- 重启后持久化读取
- 文本去重、来源更新和复制次数更新
- 英文全文、中文和子串模糊搜索
- 图片保存、图片关键词搜索和无引用文件清理
- 文件剪贴板监听、路径搜索、类型关键词与数据库持久化
- 滚动滑块鼠标拖动和独立轨道布局
- 设置持久化、快捷键动态重注册和开机启动开关
- 图片按天数/空间清理与收藏保护
- 收藏保护与清空未收藏记录
- 完整应用源码编译
