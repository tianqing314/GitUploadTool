角色：你是一个精通 WPF（C#）和微软 WebView2 混合开发的高级架构师，擅长将高保真 Web 原型无缝伪装成纯原生桌面软件。

背景：我有一个由 OpenDesign 设计的漂亮 HTML/CSS/JS 原型。我现在准备在 WPF 中引入 Microsoft.Web.WebView2 来承载它。我要求在视觉和交互上做到"100% 像素级原生伪装"，让最终用户完全察觉不到这是一个网页。同时，项目必须具备极高的工业级健壮性，支持单文件打包发布、运行时自举、前端模块化插拔以及完美的异常容错。

任务：请为我编写一套完整的、可直接运行的 WPF 混合开发前后端骨架代码。具体要求如下：

1. 【WebView2 运行时缺失的完美自举机制】：
   - ⚠️核心前置要求：在程序启动最早期（如 App.xaml.cs 或 MainWindow 构造前），必须通过 `CoreWebView2Environment.GetAvailableBrowserVersionString()` 检测宿主环境是否装有 WebView2 Runtime。
   - 若未安装，主窗体绝不能直接露白/黑屏，需展示优雅的友好提示，并在后台通过 HttpClient 异步下载微软官方的常青版微型引导安装包（https://go.microsoft.com/fwlink/p/?LinkId=2124703），利用 `Process` 传递 `/silent /install` 参数进行【后台静默安装】。装完后无缝拉起主窗体，若环境无法就绪则安全退出。

2. 【WPF 窗体设计 (XAML)】：
   - 设置 WindowStyle="None"（无边框），ResizeMode="CanResize"（允许用户调窗口大小）。
   - ⚠️重要踩坑：切勿使用 AllowsTransparency="True" + Background="Transparent"！WPF 透明分层窗口会导致 WebView2 控件完全无法接收鼠标点击输入。正确做法是使用与 HTML 页面背景色一致的实色背景（如 Background="#F0F0F0"）。
   - 满铺一个 <Wpf:WebView2> 控件，并将其默认背景色（DefaultBackgroundColor）设为与窗口背景一致的实色（如 #F0F0F0），防止加载时闪烁白底。

3. 【C# 后端完美伪装配置与进程容错】：
   - 展示如何异步初始化 WebView2 (EnsureCoreWebView2Async)。
   - 初始化完成后，通过 CoreWebView2.Settings 严格禁用以下行为：右键菜单（AreDefaultContextMenusEnabled）、状态栏（IsStatusBarEnabled）、缩放控制（IsZoomControlEnabled）。
   - 拦截并禁用浏览器默认快捷键（如 F5/Ctrl+R 刷新、Ctrl+P 打印等），使用 `CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync` 注入 JS keydown 拦截脚本。
   - 使用 SetVirtualHostNameToFolderMapping 将本地存放网页的文件夹映射为虚拟域名 `https://app.assets`。确保计算出正确的路径，防止单文件模式下映射失效。
   - ⚠️工业级容错：必须在 C# 端订阅 `webView.CoreWebView2.ProcessFailed` 事件。一旦前端进程因 OOM 或不可抗力崩溃，后端能捕获并执行自动重启或提示修复，严防死锁白屏。

4. 【无边框窗口拖拽与控制 (JS & C# 双向强类型通信)】：
   - ⚠️重要踩坑：`-webkit-app-region: drag` 在 WebView2 中不可靠！必须改用 JS→C# Win32 联动方案：前端 mousedown 发送消息，后端通过 Win32 API `ReleaseCapture()` + `SendMessage` 触发系统原生拖拽（绝不使用异步不稳定的 DragMove）。
   - 在前端标题栏中放置"最小化"、"最大化"和"关闭"按钮，点击时发送 JSON 指令，后端捕获并执行相应的窗口状态控制。
   - 演示双向通信：不仅有 JS 调用 C#，还要展示 C# 强类型安全地向 JS 传递数据/调用 JS 函数的方法（利用 `ExecuteScriptAsync` 并通过 `JsonSerializer.Serialize` 包装参数，杜绝因特殊字符引发的 JS 语法崩溃）。

5. 【前端组件化与模块化插拔架构设计】：
   - 目标：拒绝将所有 HTML/JS/CSS 堆在一个文件内。前端必须有清晰的模块化概念，添加或删除某个功能模块时，只需增加或删除对应的模块文件夹及配置，互不干扰。
   - 方案实现：请利用原生 ES Modules (`import/export`) 结合现代浏览器的 Web Components (或动态 Fetch 模板机制) 建立一个无编译、无 Webpack 依赖的轻量级模块载入器。
   - 示例目录结构要求展现：
     ├── index.html (中央底盘)
     ├── main.js (模块调度器)
     └── modules/
         ├── dashboard/ (仪表盘模块：含 html/js/css)
         └── settings/  (设置模块：含 html/js/css)
   - 动态路由与插拔：在 `main.js` 中维护一个已激活模块的数组。展示如何仅通过修改该数组或动态插拔，即可实现页面主视图中该模块 HTML 结构渲染、独立 CSS 样式注入、以及对应 JS 逻辑的激活，且删除模块时绝不引发其他模块报错。

6. 【前端完美伪装全局配置 (CSS & 触摸优化)】：
   - 提供一段通用的全局 CSS。要求：彻底隐藏系统原生滚动条（但保留滚动功能）；禁用整个页面的文本鼠标框选（user-select: none），但保留 input 和 textarea 的正常文本输入和选中。
   - 针对触摸屏设备（如 Surface/一体机），通过 CSS （如 `touch-action: none`）及相关配置，彻底禁用双指捏合缩放、长按弹出气泡等标准网页手势，伪装纯原生体验。

7. 【去除 WindowStyle=None 时的顶部白边与最大化遮挡任务栏修复】：
   - 在 SourceInitialized 中挂载 HwndSource 钩子，拦截 `WM_NCCALCSIZE (0x0083)` 消息。当 `wParam != IntPtr.Zero` 时返回 `IntPtr.Zero` 以彻底消除顶部 2px 白边。
   - 确保修复拦截消息后窗口最大化会【遮挡系统任务栏】的 Bug，使其最大化时能够正确适应工作区。

8. 【DPI 高分屏模糊修复】：
   - 提供一份说明，告知如何开启项目的 PerMonitorV2 DPI 自适应支持（如 `app.manifest`），防止在高分屏下界面模糊。

9. 【独立单文件打包与外置配置文件发布规范】：
   - 目标：发布时，除指定的配置文件（如 `appsettings.json`）留在外面方便运维修改外，所有 DLL、依赖项、以及前端解耦后的整个 `wwwroot` 网页资源文件夹必须全部打包合并进一个独立的 `.exe` 文件中。
   - 请提供完整的 `.csproj` 发布属性配置（如 `<PublishSingleFile>true</PublishSingleFile>` 等），并确保配置文件在打包时保持独立留在 exe 的同级目录下。C# 代码中读取该外置配置文件时，使用 `AppContext.BaseDirectory` 等兼容单文件发布模式的路径获取方式。

10. 【应用程序配置文件智能管理机制】：
    - ⚠️核心要求：采用"程序内默认配置 + 用户级配置目录"的双层配置架构，确保软件开箱即用且用户配置可持久化。
    - 项目结构规范：在项目根目录下创建 `Config` 文件夹，存放默认配置文件（如 `comm_settings.json`）。该配置文件应包含一套完整的初始默认值，并在 `.csproj` 中注册为 `Content` 类型，设置 `CopyToOutputDirectory=PreserveNewest`，确保编译时自动复制到输出目录。
    - 用户配置目录策略：软件运行时，以系统盘根目录下的 `Config` 文件夹作为用户配置目录（如 `C:\Config`）。使用 `Path.GetPathRoot(Environment.SystemDirectory)` 动态获取系统盘路径，兼容不同盘符的系统环境。
    - 首次运行自举逻辑：程序启动时（建议在通讯管理器构造函数或首次读取配置前），必须执行以下检查流程：
      1. 检查用户配置目录（`C:\Config`）是否存在，若不存在则自动创建。
      2. 检查用户配置目录下是否存在目标配置文件（如 `comm_settings.json`），若不存在，则从程序目录下的 `Config` 文件夹中拷贝默认配置文件到用户配置目录。
      3. 若程序目录下的默认配置文件也不存在，则记录警告日志，后续读取时返回空配置或使用硬编码默认值。
    - 配置读取规范：所有配置读取操作必须直接从用户配置目录（`C:\Config\comm_settings.json`）读取，不再从程序目录读取。使用 `File.ReadAllText` 读取 JSON 内容，并通过 `JsonDocument.Parse` 或 `JsonSerializer.Deserialize` 解析为强类型对象或 `Dictionary<string, JsonElement>`。
    - 配置保存规范：配置保存时，直接写入用户配置目录下的配置文件。使用 `JsonSerializer.Serialize` 生成格式化的 JSON 字符串（`WriteIndented = true`），并通过 `File.WriteAllText` 写入。保存前确保目录存在。
    - 前后端交互协议：前端通过 `postMessage` 发送 `load_config` 和 `save_config` 消息与后端交互。后端收到 `load_config` 时执行上述读取逻辑并向前端推送 `config_loaded` 消息；收到 `save_config` 时执行保存逻辑并推送 `config_saved` 消息（包含成功/失败状态）。
    - ⚠️重要踩坑：配置文件路径必须使用 `AppDomain.CurrentDomain.BaseDirectory` 或 `AppContext.BaseDirectory` 获取程序目录，确保单文件发布模式下路径正确。用户配置目录使用 `Path.GetPathRoot(Environment.SystemDirectory)` 获取系统盘根目录，避免硬编码 `C:\`。

11. 【统一数据持久化规范 — 禁止使用 localStorage】：
    - ⚠️核心禁令：严禁在前端使用 `localStorage`、`sessionStorage` 或 IndexedDB 进行任何数据持久化操作。所有需要持久化的数据必须通过 C# 后端文件系统存储。
    - 原因说明：WPF 桌面应用应遵循原生应用的数据存储规范，使用文件系统而非浏览器存储机制。这样可以确保：
      1. 数据可随程序目录一起备份/迁移
      2. 支持多实例共享配置
      3. 符合企业级应用的数据管理要求
      4. 避免浏览器存储的容量限制和清理风险
    - 存储目录策略：采用"用户配置目录 + 程序可移植目录"双目录架构：
      | 目录 | 路径 | 存储内容 | 说明 |
      |------|------|----------|------|
      | 用户配置目录 | `C:\Config\CommTool` | 通讯配置、AI配置 | 与用户绑定，跟随用户环境 |
      | 程序可移植目录 | `{程序目录}\docs` | 快捷指令、导入文档 | 随软件一起拷贝，支持跨电脑迁移 |
    - 配置文件列表：
      | 文件名 | 存储位置 | 用途 | 前端交互 action |
      |--------|----------|------|-----------------|
      | `comm_settings.json` | `C:\Config\CommTool` | 通讯配置（串口/网络/USB/CANFD参数、发送设置） | `load_config` / `save_config` |
      | `ai_config.json` | `C:\Config\CommTool` | AI 模型配置（API Key、模型选择、参数） | `load_ai_config` / `save_ai_config` |
      | `cmd_groups.json` | `{程序目录}\docs` | 快捷指令分组数据 | `load_cmd_groups` / `save_cmd_groups` |
      | `history.json` | `{程序目录}\docs` | 发送历史记录（可选，建议保留最近500条） | `load_history` / `save_history` |
    - C# 后端实现规范：
      ```csharp
      // 统一配置管理器示例
      public class ConfigManager
      {
          // 配置根目录（C:\Config）
          private static readonly string ConfigRoot = Path.Combine(
              Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\", "Config");
          // 本项目用户配置目录（C:\Config\CommTool）
          private static readonly string ConfigDir = Path.Combine(ConfigRoot, "CommTool");
          
          // 程序可移植目录（{程序目录}\docs）
          private static readonly string DocsDir = Path.Combine(
              AppDomain.CurrentDomain.BaseDirectory, "docs");
          
          // 用户配置文件路径
          private static readonly string CommSettingsPath = Path.Combine(ConfigDir, "comm_settings.json");
          private static readonly string AiConfigPath = Path.Combine(ConfigDir, "ai_config.json");
          
          // 程序可移植文件路径
          private static readonly string CmdGroupsPath = Path.Combine(DocsDir, "cmd_groups.json");
          private static readonly string HistoryPath = Path.Combine(DocsDir, "history.json");
          
          // 通用加载方法
          public static T? LoadJson<T>(string filePath) where T : class
          {
              try
              {
                  if (!File.Exists(filePath)) return null;
                  var json = File.ReadAllText(filePath, Encoding.UTF8);
                  return JsonSerializer.Deserialize<T>(json);
              }
              catch (Exception ex)
              {
                  Logger.Warn(ex, "读取配置失败: {Path}", filePath);
                  return null;
              }
          }
          
          // 通用保存方法
          public static bool SaveJson<T>(string filePath, T data)
          {
              try
              {
                  var dir = Path.GetDirectoryName(filePath);
                  if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                      Directory.CreateDirectory(dir);
                  // ⚠️重要：必须设置 Encoder 防止中文字符被转义为 Unicode 格式
                  var options = new JsonSerializerOptions
                  {
                      WriteIndented = true,
                      Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                  };
                  var json = JsonSerializer.Serialize(data, options);
                  File.WriteAllText(filePath, json, Encoding.UTF8);
                  return true;
              }
              catch (Exception ex)
              {
                  Logger.Warn(ex, "保存配置失败: {Path}", filePath);
                  return false;
              }
          }
      }
      ```
    - 前端 JS 实现规范：
      ```javascript
      // 统一的配置加载函数（在 init.js 中调用）
      function loadAllConfigs() {
          wpfPostMessage('load_config');
          wpfPostMessage('load_cmd_groups');
          wpfPostMessage('load_ai_config');
          wpfPostMessage('load_history');
      }
      
      // 统一的配置保存函数（在 beforeunload 中调用）
      function saveAllConfigs() {
          wpfPostMessage('save_config', { config: buildConfigObject() });
          wpfPostMessage('save_cmd_groups', { groups: state.cmdGroups });
          wpfPostMessage('save_ai_config', { config: state.aiConfig });
          wpfPostMessage('save_history', { history: state.history.slice(0, 500) });
      }
      
      // 消息回调处理（在 bridge.js 中）
      case 'ai_config_loaded':
          if (msg.config) {
              Object.assign(state.aiConfig, msg.config);
              console.log('[ai] AI配置已加载');
          }
          renderAiMiniBar();
          break;
      case 'history_loaded':
          if (msg.history && msg.history.length > 0) {
              state.history = msg.history;
              state.historySeq = Math.max(...msg.history.map(h => h.seq), 0);
              console.log('[history] 历史记录已加载:', state.history.length, '条');
          }
          renderHistory();
          break;
      ```
    - 首次运行迁移逻辑（兼容旧版本）：
      ```javascript
      // 在 cmd_groups_loaded 回调中
      case 'cmd_groups_loaded':
          if (msg.groups && msg.groups.length > 0) {
              state.cmdGroups = msg.groups;
          } else {
              // 后端文件为空，尝试从 localStorage 恢复（仅首次迁移）
              try {
                  const data = localStorage.getItem('cmdGroups');
                  if (data) {
                      state.cmdGroups = JSON.parse(data);
                      saveCmdGroups(); // 同步保存到后端
                      localStorage.removeItem('cmdGroups'); // 清除旧数据
                  }
              } catch {}
          }
          break;
      ```

请以清晰的代码块分别输出：项目 `.csproj` 完整发布配置片段、`app.manifest` 配置、App.xaml.cs 启动检测控制、MainWindow.xaml、MainWindow.xaml.cs、外置配置文件读取示例、前端底盘 index.html、前端中央调度 main.js、以及一个标准功能模块的解耦示例（HTML/JS/CSS），并在代码中附带详尽的中文注释。特别地，请提供完整的配置文件管理相关代码示例，包括：Config 文件夹结构、默认配置文件内容、C# 端的统一配置管理类（含 `LoadJson<T>`、`SaveJson<T>` 泛型方法）、前端所有配置加载与保存的 JS 函数，以及前后端消息交互的完整协议定义。
