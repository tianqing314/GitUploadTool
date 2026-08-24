// ============ WebView2 Bridge Communication ============

// 向 C# 发送消息
function sendToCSharp(action, data = {}) {
    const message = { action, ...data };
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(JSON.stringify(message));
    } else {
        console.log('[bridge] (no webview) Message to C#:', message);
    }
}

// 接收 C# 消息
if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener('message', (event) => {
        try {
            // 后端用 PostWebMessageAsJson 推送时 event.data 已是对象，无需再 parse
            const data = typeof event.data === 'string' ? JSON.parse(event.data) : event.data;
            handleCSharpMessage(data);
        } catch (e) {
            console.error('[bridge] Error parsing message:', e, event.data);
        }
    });
}

// 处理 C# 消息（后端格式: {evt, data}）
function handleCSharpMessage(msg) {
    console.log('[bridge] Message from C#:', msg);

    // 兼容 {evt,data} 与平铺两种格式
    const evt = msg.evt || msg.action;
    const data = msg.data !== undefined ? msg.data : msg;

    switch (evt) {
        case 'tokenLoginSuccess':
        case 'loginSuccess':
            showPage('main');
            updateAuthStatus(true, data.user);
            showToast('登录成功：' + (data.user?.login || ''), 'success');
            sendToCSharp('repositories');
            sendToCSharp('loadHistory');
            break;
        case 'tokenLoginFailed':
        case 'loginFailed':
            showLoginError(data.message || '登录失败');
            break;
        case 'authStatus':
            if (data.isAuthenticated && data.user) {
                showPage('main');
                updateAuthStatus(true, data.user);
                sendToCSharp('repositories');
                sendToCSharp('loadHistory');
            }
            break;
        case 'logoutSuccess':
            showPage('login');
            document.getElementById('tokenInput').value = '';
            break;
        case 'projectSelected':
            updateSelectedProject(data.path, data.name);
            break;
        case 'scanResult':
            updateScanResult(data);
            break;
        case 'uploadProgress':
            updateUploadProgress(data);
            break;
        case 'uploadSuccess':
            showUploadSuccess(data);
            // 上传成功后刷新历史记录与统计
            sendToCSharp('loadHistory');
            break;
        case 'uploadResult':
            if (!data.success) showUploadError(data.error || '未知错误');
            break;
        case 'history':
            updateHistory(data.items || []);
            break;
        case 'settings':
            loadSettings(data);
            break;
        case 'settingsSaved':
            showToast('设置已保存', 'success');
            break;
        case 'repositories':
            renderRepositories(data.items || []);
            break;
        case 'updateRepoVisibilitySuccess':
        case 'deleteRepositorySuccess':
            sendToCSharp('repositories');
            showToast('操作成功', 'success');
            break;
        case 'error':
            showErrorToast(data.message || '操作失败', data.owner, data.repo);
            break;
    }
}

// ============ Page Navigation ============

function showPage(pageName) {
    document.querySelectorAll('.page').forEach(p => p.classList.remove('active'));
    document.getElementById(`page-${pageName}`).classList.add('active');
}

function showTab(tabName) {
    document.querySelectorAll('.tab-pane').forEach(p => p.classList.remove('active'));
    document.querySelectorAll('.nav-tab').forEach(t => t.classList.remove('active'));

    document.getElementById(`tab-${tabName}`).classList.add('active');
    document.querySelector(`[data-tab="${tabName}"]`).classList.add('active');

    // 切换到历史记录标签时刷新数据
    if (tabName === 'history') {
        sendToCSharp('loadHistory');
    }
}

// ============ Auth & Login ============

function updateAuthStatus(isAuthenticated, user = null) {
    const authStatusEl = document.getElementById('authStatus');
    const userAvatarEl = document.getElementById('userAvatar');
    const userNameEl = document.getElementById('userName');

    if (isAuthenticated && user) {
        if (userAvatarEl) {
            userAvatarEl.textContent = (user.login || '?').charAt(0).toUpperCase();
            userAvatarEl.style.background = '';
        }
        if (userNameEl) userNameEl.textContent = user.login || user.name || 'Unknown';
        if (authStatusEl) authStatusEl.textContent = '✅ 已登录';
    } else {
        if (userAvatarEl) userAvatarEl.textContent = '?';
        if (userNameEl) userNameEl.textContent = '未登录';
        if (authStatusEl) authStatusEl.textContent = '❌ 未登录';
    }
}

function showLoginError(message) {
    const errorEl = document.getElementById('loginError');
    if (errorEl) {
        errorEl.textContent = message;
        errorEl.style.display = 'block';
        setTimeout(() => { errorEl.style.display = 'none'; }, 5000);
    }
}

// ============ Upload Steps ============

let currentStep = 1;
let selectedProject = null;
let repoConfig = {
    name: '',
    description: '',
    visibility: 'public',
    branch: 'main',
    gitignore: '',
    useLfs: false,
    excludeLarge: false,
};

function goToStep(step) {
    document.querySelectorAll('.step').forEach((s, i) => {
        s.classList.remove('active', 'completed');
        if (i + 1 < step) s.classList.add('completed');
        if (i + 1 === step) s.classList.add('active');
    });

    document.querySelectorAll('.step-content').forEach((s, i) => {
        s.style.display = (i + 1 === step) ? 'block' : 'none';
    });

    currentStep = step;
}

function updateSelectedProject(path, name) {
    selectedProject = { path, name };
    document.getElementById('dropZone').style.display = 'none';

    const selectedProjectEl = document.getElementById('selectedProject');
    if (selectedProjectEl) {
        selectedProjectEl.style.display = 'block';
        document.getElementById('projectName').textContent = name;
        document.getElementById('projectPath').textContent = path;
        document.getElementById('repoName') && (document.getElementById('repoName').placeholder = name);
    }

    showToast('已选择项目：' + name, 'success');
}

function updateScanResult(data) {
    if (!data.success) {
        showToast(data.error || '扫描失败', 'error');
        return;
    }

    document.getElementById('totalFiles').textContent = data.totalFiles || 0;
    document.getElementById('totalSize').textContent = formatFileSize(data.totalSize || 0);
    document.getElementById('largeFilesCount').textContent =
        (data.largeFiles && data.largeFiles.length) || 0;

    if (data.largeFiles && data.largeFiles.length > 0) {
        document.getElementById('largeFilesSection').style.display = 'block';

        if (data.hasOver100MB) {
            document.getElementById('largeFileDecision').style.display = 'block';
        }

        // 渲染带复选框的大文件列表（默认全选 = 排除该文件）
        document.getElementById('largeFilesList').innerHTML = data.largeFiles.map((file, i) => `
            <div class="file-item ${file.size > 100 * 1024 * 1024 ? 'error' : 'warning'}">
                <label style="display: flex; align-items: center; gap: 10px; cursor: pointer; flex: 1;">
                    <input type="checkbox" class="large-file-check" data-index="${i}" data-path="${escapeHtml(file.path || file.name)}" checked>
                    <div class="file-info">
                        <div class="file-name">${escapeHtml(file.name)}</div>
                        <div class="file-size">${formatFileSize(file.size)}${file.size > 100 * 1024 * 1024 ? ' · 超过 GitHub 限制' : ''}</div>
                    </div>
                </label>
            </div>
        `).join('');

        // 全选/取消全选控件
        const listEl = document.getElementById('largeFilesList');
        const header = document.createElement('div');
        header.style.cssText = 'display:flex; align-items:center; gap:10px; padding:4px 0; color:var(--text-2); font-size:12px;';
        header.innerHTML = `
            <label style="display:flex; align-items:center; gap:8px; cursor:pointer;">
                <input type="checkbox" id="largeFileCheckAll" checked>
                勾选的文件将被排除上传（默认全选）
            </label>`;
        listEl.parentElement.insertBefore(header, listEl);

        document.getElementById('largeFileCheckAll')?.addEventListener('change', (e) => {
            document.querySelectorAll('.large-file-check').forEach(cb => cb.checked = e.target.checked);
        });
    } else {
        document.getElementById('largeFilesSection').style.display = 'none';
    }
}

// 收集被勾选排除的大文件路径列表
function getExcludedLargeFiles() {
    return Array.from(document.querySelectorAll('.large-file-check'))
        .filter(cb => cb.checked)
        .map(cb => cb.dataset.path)
        .filter(p => !!p);
}

function updateUploadProgress(data) {
    document.getElementById('uploadStatus').textContent = data.status || '正在上传...';
    document.getElementById('uploadSubStatus').textContent = data.subStatus || '';
    document.getElementById('progressFill').style.width = `${data.progress || 0}%`;
    document.getElementById('progressText').textContent = `${data.progress || 0}%`;
}

function showUploadSuccess(data) {
    document.getElementById('uploadProgressArea').style.display = 'none';
    document.getElementById('uploadError').style.display = 'none';
    document.getElementById('uploadSuccess').style.display = 'block';

    const viewRepoBtn = document.getElementById('btnViewRepo');
    if (viewRepoBtn && data.repoUrl) {
        viewRepoBtn.onclick = () => sendToCSharp('openUrl', { url: data.repoUrl });
    }
}

function showUploadError(message) {
    document.getElementById('uploadProgressArea').style.display = 'none';
    document.getElementById('uploadSuccess').style.display = 'none';
    document.getElementById('uploadError').style.display = 'block';
    document.getElementById('errorMessage').textContent = message;
}

function updateStats(items = []) {
    const list = items || [];
    const uniquePaths = new Set(list.map(it => it.path).filter(Boolean));
    const count = list.length;
    const totalSize = list.reduce((sum, it) => sum + (Number(it.size) || 0), 0);
    const elProjects = document.getElementById('statProjects');
    const elUploads = document.getElementById('statUploads');
    const elSize = document.getElementById('statSize');
    if (elProjects) elProjects.textContent = String(uniquePaths.size || count);
    if (elUploads) elUploads.textContent = String(count);
    if (elSize) elSize.textContent = formatFileSize(totalSize);
}

function updateHistory(items) {
    const historyListEl = document.getElementById('historyList');
    if (!historyListEl) return;

    if (!items || items.length === 0) {
        historyListEl.innerHTML = '<div style="color: var(--text-3); text-align: center; padding: 40px;">暂无上传记录</div>';
        updateStats([]);
        return;
    }

    historyListEl.innerHTML = items.map((item, idx) => `
        <div class="file-item" style="cursor: pointer;" data-url="${escapeHtml(item.repoUrl || '')}" data-idx="${idx}">
            <div class="file-info">
                <div class="file-name" style="font-weight: 600;">${escapeHtml(item.name)}</div>
                <div class="file-size">${escapeHtml(item.path || '')} • ${formatDate(item.uploadTime)}</div>
            </div>
        </div>
    `).join('');

    updateStats(items);
}

function openHistoryRepo(url) {
    if (url) sendToCSharp('openUrl', { url });
}

// 绑定历史记录点击（事件委托）
document.addEventListener('click', (ev) => {
    const item = ev.target.closest('.file-item[data-url]');
    if (!item) return;
    const url = item.getAttribute('data-url') || '';
    if (url) sendToCSharp('openUrl', { url });
});

function loadSettings(data) {
    const branchEl = document.getElementById('settingDefaultBranch');
    const commitEl = document.getElementById('settingDefaultCommitMessage');
    const proxyAddrEl = document.getElementById('settingProxyAddress');
    const proxyPortEl = document.getElementById('settingProxyPort');
    const gitignoreEl = document.getElementById('settingDefaultGitignore');
    const platformEl = document.getElementById('settingPlatform');
    const gitlabUrlEl = document.getElementById('settingGitLabUrl');

    if (branchEl) branchEl.value = data.defaultBranch || 'main';
    if (commitEl) commitEl.value = data.defaultCommitMessage || 'Update from GitUploadTool';
    if (proxyAddrEl) proxyAddrEl.value = data.proxyAddress || '';
    if (proxyPortEl) proxyPortEl.value = data.proxyPort ?? '';
    if (gitignoreEl) gitignoreEl.value = data.defaultGitignore || '';
    if (platformEl) platformEl.value = data.platform || 'github';
    if (gitlabUrlEl) gitlabUrlEl.value = data.gitLabInstanceUrl || 'https://gitlab.com';
}

// ============ Event Listeners ============

document.addEventListener('DOMContentLoaded', () => {
    // Login page
    document.getElementById('btnOAuthLogin')?.addEventListener('click', () => {
        sendToCSharp('login');
    });

    document.getElementById('btnTokenLogin')?.addEventListener('click', onTokenLoginClick);
    document.getElementById('tokenInput')?.addEventListener('keydown', (e) => {
        if (e.key === 'Enter') onTokenLoginClick();
    });



    // 切换平台时显示/隐藏 GitLab URL 输入框
    const loginPlatformEl = document.getElementById('loginPlatform');
    const gitlabUrlInput = document.getElementById('gitlabUrlInput');
    if (loginPlatformEl && gitlabUrlInput) {
        function updateGitlabUrlVisibility() {
            if (loginPlatformEl.value === 'gitlab') {
                gitlabUrlInput.style.display = 'block';
            } else {
                gitlabUrlInput.style.display = 'none';
            }
        }
        loginPlatformEl.addEventListener('change', updateGitlabUrlVisibility);
        updateGitlabUrlVisibility();
    }

    function onTokenLoginClick() {
        const token = document.getElementById('tokenInput')?.value.trim();
        if (!token) {
            showLoginError('请输入 Token');
            return;
        }
        showLoginError('');   // clear old errors
        sendToCSharp('tokenLogin', {
            token,
            platform: document.getElementById('loginPlatform')?.value || 'github',
            gitlabUrl: document.getElementById('gitlabUrlInput')?.value || '',
        });
    }

    // Main page navigation
    document.querySelectorAll('.nav-tab').forEach(tab => {
        tab.addEventListener('click', () => showTab(tab.dataset.tab));
    });

    // Start upload
    document.getElementById('btnStartUpload')?.addEventListener('click', () => {
        showTab('upload');
        goToStep(1);
    });

    // Step 1
    const dropZone = document.getElementById('dropZone');
    if (dropZone) {
        dropZone.addEventListener('click', () => sendToCSharp('select_folder'));

        dropZone.addEventListener('dragover', (e) => {
            e.preventDefault();
            dropZone.style.borderColor = 'var(--accent)';
            dropZone.style.background = 'rgba(108,123,255,0.05)';
        });

        dropZone.addEventListener('dragleave', () => {
            dropZone.style.borderColor = 'var(--border)';
            dropZone.style.background = '';
        });

        dropZone.addEventListener('drop', (e) => {
            e.preventDefault();
            dropZone.style.borderColor = 'var(--border)';
            dropZone.style.background = '';
            sendToCSharp('select_folder');
        });
    }

    document.getElementById('btnChangeProject')?.addEventListener('click', () =>
        sendToCSharp('select_folder'));

    document.getElementById('btnNext1')?.addEventListener('click', () => {
        if (!selectedProject) {
            showToast('请选择项目文件夹', 'error');
            return;
        }
        goToStep(2);
    });

    document.getElementById('btnCancel')?.addEventListener('click', () => showTab('home'));

    // Step 2
    document.getElementById('btnPrev2')?.addEventListener('click', () => goToStep(1));
    document.getElementById('btnNext2')?.addEventListener('click', () => {
        repoConfig.name = document.getElementById('repoName')?.value.trim()
            || selectedProject.name;
        repoConfig.description = document.getElementById('repoDescription')?.value || '';
        repoConfig.visibility = document.querySelector('input[name="repoVisibility"]:checked')?.value || 'public';
        repoConfig.branch = document.getElementById('branchName')?.value.trim() || 'main';
        repoConfig.gitignore = document.getElementById('gitignoreTemplate')?.value || '';

        sendToCSharp('scanProject', { path: selectedProject.path, repoConfig });
        goToStep(3);
    });

    // Step 3
    document.getElementById('btnPrev3')?.addEventListener('click', () => goToStep(2));
    document.getElementById('btnRescan')?.addEventListener('click', () => {
        sendToCSharp('scanProject', { path: selectedProject.path, repoConfig });
    });

    document.getElementById('btnUseLFS')?.addEventListener('click', () => {
        repoConfig.useLfs = true;
        repoConfig.excludeLarge = false;
        document.getElementById('largeFileDecision').style.display = 'none';
        showToast('已选择 Git LFS 方案', 'success');
    });

    document.getElementById('btnExcludeLarge')?.addEventListener('click', () => {
        repoConfig.useLfs = false;
        repoConfig.excludeLarge = true;
        document.getElementById('largeFileDecision').style.display = 'none';
        showToast('将排除大文件后上传', 'success');
    });

    document.getElementById('btnCancelUpload')?.addEventListener('click', () => showTab('home'));

    document.getElementById('btnNext3')?.addEventListener('click', () => {
        sendToCSharp('startUpload', { path: selectedProject.path, repoConfig });
        goToStep(4);
    });

    // Step 4
    document.getElementById('btnPrev4')?.addEventListener('click', () => goToStep(3));
    document.getElementById('btnResetUpload')?.addEventListener('click', () => {
        showTab('upload');
        goToStep(1);
    });
    document.getElementById('btnRetry')?.addEventListener('click', () => {
        sendToCSharp('startUpload', { path: selectedProject.path, repoConfig });
        goToStep(4);
    });
    document.getElementById('btnHome')?.addEventListener('click', () => showTab('home'));

    // Settings / Logout
    document.getElementById('btnSettings')?.addEventListener('click', () => showTab('settings'));
    document.getElementById('btnLogout')?.addEventListener('click', () => sendToCSharp('logout'));
    document.getElementById('btnLogoutSettings')?.addEventListener('click', () => sendToCSharp('logout'));

    document.getElementById('btnSaveSettings')?.addEventListener('click', () => {
        sendToCSharp('saveSettings', {
            settings: {
                defaultBranch: document.getElementById('settingDefaultBranch')?.value || 'main',
                defaultCommitMessage: document.getElementById('settingDefaultCommitMessage')?.value || '',
                proxyAddress: document.getElementById('settingProxyAddress')?.value || '',
                proxyPort: parseInt(document.getElementById('settingProxyPort')?.value || '0', 10) || null,
                platform: document.getElementById('settingPlatform')?.value || 'github',
                gitLabInstanceUrl: document.getElementById('settingGitLabUrl')?.value || 'https://gitlab.com',
                autoPush: true,
            },
        });
    });

    document.getElementById('btnSaveGitignore')?.addEventListener('click', () => {
        showToast('模板将在上传时应用', 'info');
    });

    // 初始化：检查登录状态并加载基础数据
    sendToCSharp('check_auth');
});

// ============ Utility Functions ============

function showErrorToast(message, owner, repo) {
    // 识别权限不足场景
    const needsAdmin = /admin|403/i.test(message);
    const settingsUrl = (owner && repo)
        ? `https://github.com/${owner}/${repo}/settings`
        : null;

    const toast = document.createElement('div');
    toast.style.cssText = `
        position: fixed;
        bottom: 24px;
        right: 24px;
        max-width: 420px;
        padding: 14px 18px;
        background: var(--red);
        color: white;
        border-radius: var(--radius);
        font-size: 13px;
        font-weight: 500;
        z-index: 10000;
        animation: slideIn 0.3s ease;
        box-shadow: var(--shadow);
        line-height: 1.5;
        white-space: pre-wrap;
        word-break: break-word;
    `;

    const textNode = document.createElement('div');
    textNode.textContent = message;
    toast.appendChild(textNode);

    if (needsAdmin && settingsUrl) {
        const hint = document.createElement('div');
        hint.style.cssText = 'margin-top: 8px; font-size: 12px; opacity: 0.95;';
        hint.textContent = '当前 Token 对该仓库没有 admin 权限（可能是协作者身份）。';
        toast.appendChild(hint);

        const btnWrap = document.createElement('div');
        btnWrap.style.cssText = 'margin-top: 10px; display: flex; gap: 8px; flex-wrap: wrap; align-items: center;';

        const openBtn = document.createElement('button');
        openBtn.textContent = '打开仓库设置';
        openBtn.style.cssText = `
            padding: 6px 12px;
            border: 1px solid rgba(255,255,255,0.6);
            background: transparent;
            color: white;
            border-radius: 6px;
            font-size: 12px;
            cursor: pointer;
        `;
        openBtn.onclick = () => {
            sendToCSharp('openUrl', { url: settingsUrl });
            dismiss();
        };
        btnWrap.appendChild(openBtn);

        const transferHint = document.createElement('span');
        transferHint.style.cssText = 'font-size: 11px; opacity: 0.85;';
        transferHint.textContent = '在 Danger Zone 中可转让所有权';
        btnWrap.appendChild(transferHint);

        toast.appendChild(btnWrap);
    }

    document.body.appendChild(toast);

    const dismiss = () => {
        toast.style.animation = 'slideOut 0.3s ease';
        setTimeout(() => toast.remove(), 300);
    };
    setTimeout(dismiss, 8000);
}

function showToast(message, type = 'info') {
    const toast = document.createElement('div');
    toast.style.cssText = `
        position: fixed;
        bottom: 24px;
        right: 24px;
        padding: 16px 24px;
        background: ${type === 'error' ? 'var(--red)' : type === 'success' ? 'var(--green)' : 'var(--bg-3)'};
        color: white;
        border-radius: var(--radius);
        font-size: 14px;
        font-weight: 500;
        z-index: 10000;
        animation: slideIn 0.3s ease;
        box-shadow: var(--shadow);
    `;
    toast.textContent = message;
    document.body.appendChild(toast);

    setTimeout(() => {
        toast.style.animation = 'slideOut 0.3s ease';
        setTimeout(() => toast.remove(), 300);
    }, 3000);
}

function formatFileSize(bytes) {
    if (!bytes || bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
}

function formatDate(timestamp) {
    if (!timestamp) return '';
    const date = new Date(timestamp);
    if (isNaN(date.getTime())) return String(timestamp);
    return date.toLocaleDateString('zh-CN', {
        year: 'numeric',
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
    });
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text == null ? '' : String(text);
    return div.innerHTML;
}

// ============ Repository Management ============

function renderRepositories(repos) {
    const repoListEl = document.getElementById('repoList');
    if (!repoListEl) return;

    if (!repos || repos.length === 0) {
        repoListEl.innerHTML = '<p style="color: var(--text-3);">暂无仓库，点击“新建仓库”创建一个</p>';
        return;
    }

    const owner = (document.getElementById('userName')?.textContent || '').trim();

    let html = '';
    repos.forEach(repo => {
        const isPrivate = !!repo.IsPrivate;
        const visibilityIcon = isPrivate ? '🔒' : '🌐';
        const visibilityText = isPrivate ? '私有' : '公开';
        const repoName = escapeHtml(repo.Name || '');
        const description = escapeHtml((repo.Description || '').trim());
        const repoUrl = repo.HtmlUrl || repo.html_url || repo.HtmlUrl || '';
        const updatedAt = formatDate(repo.UpdatedAt);
        const createdAt = formatDate(repo.CreatedAt);

        html += `
            <div class="card" style="flex: 1 1 200px; min-width: 180px; padding: 14px; display: flex; flex-direction: column; gap: 8px;">
                <div style="display: flex; align-items: center; justify-content: space-between; gap: 8px;">
                    <div style="font-weight: 600; font-size: 14px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;" title="${repoName}">${repoName}</div>
                    <div style="font-size: 12px; color: var(--text-3); white-space: nowrap;">${visibilityIcon}${visibilityText}</div>
                </div>
                ${description ? `<div style="color: var(--text-2); font-size: 12px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;" title="${description}">${description}</div>` : '<div style="color: var(--text-2); font-size: 12px; opacity: .6;">暂无描述</div>'}
                <div style="color: var(--text-3); font-size: 11px;">创建时间：${createdAt || updatedAt || '未知'}</div>
                <div style="display: flex; align-items: center; justify-content: flex-end; gap: 8px; margin-top: auto;">
                    <button class="btn btn-secondary" style="padding: 0; width: 32px; height: 32px; font-size: 15px; line-height: 1;" title="${isPrivate ? '转为公开' : '转为私有'}" onclick="sendToCSharp('updateRepoVisibility', {owner: '${escapeHtml(owner || '')}', repo: '${repoName}', isPrivate: ${!isPrivate}})">
                        ${isPrivate ? '🌐' : '🔒'}
                    </button>
                    <button class="btn btn-danger" style="padding: 0; width: 32px; height: 32px; font-size: 15px; line-height: 1;" title="删除仓库" onclick="if(confirm('确定删除仓库「${repoName}」吗？此操作不可恢复。')) sendToCSharp('deleteRepository', {owner: '${escapeHtml(owner || '')}', repo: '${repoName}'})">
                        🗑️
                    </button>
                    <button class="btn btn-primary" style="padding: 0; width: 32px; height: 32px; font-size: 15px; line-height: 1;" title="查看仓库" onclick="sendToCSharp('openUrl', {url: '${repoUrl.replace(/'/g, "\\'")}'})">
                        🔗
                    </button>
                </div>
            </div>
        `;
    });
    repoListEl.innerHTML = html;
}

// 事件监听：刷新仓库列表
if (document.getElementById('btnRefreshRepos')) {
    document.getElementById('btnRefreshRepos').addEventListener('click', () => {
        sendToCSharp('repositories');
    });
}

// 切换平台时显示/隐藏 GitLab URL 输入框
const platformEl = document.getElementById('settingPlatform');
const gitlabUrlRow = document.getElementById('gitlabUrlRow');
if (platformEl && gitlabUrlRow) {
    function updateGitlabUrlVisibility() {
        if (platformEl.value === 'gitlab') {
            gitlabUrlRow.style.display = 'block';
        } else {
            gitlabUrlRow.style.display = 'none';
        }
    }
    platformEl.addEventListener('change', updateGitlabUrlVisibility);
    updateGitlabUrlVisibility();
}
