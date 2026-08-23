# 三个功能实现计划

## 现状速览（来自代码探索）

| 功能 | 现状 | 缺失点 |
|------|------|--------|
| `.gitignore` 模板 | 4 个硬编码模板；`ApplyTemplateAsync` **覆盖** 写入 `.gitignore` | 无法追加到现有文件；默认模板选择 UI 无效；模板在 C# 代码内而非外部文件 |
| 大文件选择上传 | Step-3 仅显示大文件列表；两个按钮 `useLfs`/`excludeLarge` | 不能逐文件勾选排除；排除逻辑依赖过期缓存；扫描结果与 100MB 限制不一致（50MB vs 100MB） |
| Git LFS 支持 | 调用 `git lfs track "*<ext>"` | 不检查 `git-lfs` 是否安装；按扩展名全局跟踪；无 `.gitattributes` 提交保障；`git lfs install` 缺失 |

---

## 功能 1：自动 `.gitignore` 从外部 templates 读取

**目标**：`.gitignore` 模板改为外部文件，支持追加写入并持久化默认选择。

**文件改动**：
1. **新增** `wwwroot/templates/gitignore/` 目录，将 4 个模板改为独立文件：
   - `dotnet.gitignore`, `node.gitignore`, `python.gitignore`, `java.gitignore`
2. **`Services/GitIgnoreService.cs`**：
   - `GetTemplates()` 改为从 `wwwroot/templates/gitignore/` 目录读取所有 `.gitignore` 文件，按文件名解析模板元数据
   - `ApplyTemplateAsync(path, language)` 改为：若 `.gitignore` 已存在则 **追加**（避免重复），否则新建
3. **`Models/AppSettings.cs`**：新增 `DefaultGitignore` 字段
4. **`Bridge/WebViewBridge.cs`**：读取 `AppSettings.DefaultGitignore`，在 `UploadProjectAsync` 中自动应用默认模板
5. **前端 `app.js`**：Settings 页的“保存”按钮真正持久化 `DefaultGitignore` 到后端

---

## 功能 2：大文件选择上传（逐文件排除）

**目标**：Step-3 大文件面板支持勾选文件，被勾选的文件写入 `.gitignore` 排除。

**文件改动**：
1. **前端 `index.html`（Step-3 区域）**：
   - 大文件列表每个条目增加 `<input type="checkbox">`
   - 默认全选（用户可取消勾选保留的文件）
2. **前端 `app.js`**：
   - `updateScanResult(data)` 中渲染带复选框的文件列表
   - `btnNext3` / `btnExcludeLarge` 点击时收集 `checked=false` 的文件列表，通过 `sendToCSharp('excludeFiles', { files: [...] })` 发送
3. **后端 `Bridge/WebViewBridge.cs`**：
   - 新增 `exclude_files` action，接收文件列表，将其相对路径追加到 `.gitignore`
   - 清理 `_largeFileCache` 中已排除的文件，避免后续 LFS 误跟踪
4. **`Services/GitService.cs` `UploadProjectAsync`**：
   - 扫描 100MB 文件时，**跳过已被 `.gitignore` 匹配的文件**，减少误报

---

## 功能 3：Git LFS 支持

**目标**：提供可靠的 Git LFS 工作流：检查安装 → 安装/初始化 → 跟踪文件 → 提交 `.gitattributes`。

**文件改动**：
1. **`Services/GitService.cs`**：
   - 新增 `EnsureGitLfsInstalledAsync(path)`：检查 `git lfs version`，失败则尝试 `git lfs install`
   - 新增 `TrackLargeFilesWithLfsAsync(path, files)`：对指定文件运行 `git lfs track "相对路径"`（精确到文件而非扩展名）
   - 在 `UploadProjectAsync` 的 push 前确保 `.gitattributes` 已暂存并提交
2. **`Bridge/WebViewBridge.cs`**：
   - `enableLFS` 分支调用 `EnsureGitLfsInstalledAsync` + `TrackLargeFilesWithLfsAsync`
   - 向 `_largeFileCache` 中已 LFS 跟踪的文件添加标记，避免被 `excludeLarge` 分支重复处理
3. **前端 `app.js`**：
   - `btnUseLFS` 发送 `enableLFS` 前先发送当前大文件列表，让后端精确跟踪

---

## 执行顺序建议

1. 功能 1（外部 `.gitignore` 模板）——基础改动，模板文件与读取逻辑
2. 功能 2（大文件选择上传）——依赖功能 1 的 `.gitignore` 追加能力
3. 功能 3（Git LFS）——依赖功能 1 + 2 的文件筛选结果

预计改动文件 8–10 个，新增外部模板文件 4 个，无破坏性 API 变更。