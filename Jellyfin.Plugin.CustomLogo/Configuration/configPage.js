const pluginId = 'b7e8dce1-44df-4b37-91a3-15fcf9f3b76a';

function authHeader() {
    return 'MediaBrowser Token="' + ApiClient.accessToken() + '"';
}

async function request(method, path, body, contentType) {
    const headers = { Authorization: authHeader() };
    if (contentType) {
        headers['Content-Type'] = contentType;
    }

    const response = await fetch(ApiClient.getUrl(path), { method, headers, body });
    if (!response.ok) {
        let message = response.status + ' ' + response.statusText;
        try {
            const text = await response.text();
            if (text) {
                try {
                    const json = JSON.parse(text);
                    message = json.detail || json.title || json.Message || (typeof json === 'string' ? json : text);
                } catch {
                    message = text;
                }
            }
        } catch {
            // keep status text
        }

        throw new Error(message);
    }

    return response;
}

function pick(obj, name) {
    // The server may serialize PascalCase or camelCase depending on version/settings.
    if (!obj) return undefined;
    if (name in obj) return obj[name];
    const camel = name.charAt(0).toLowerCase() + name.slice(1);
    return obj[camel];
}

function formatSize(bytes) {
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(0) + ' KB';
    return (bytes / 1024 / 1024).toFixed(1) + ' MB';
}

function renderSlot(row, info) {
    const slot = row.getAttribute('data-slot');
    const doc = row.ownerDocument;
    const preview = row.querySelector('.clPreview');
    const removeButton = row.querySelector('.btnRemove');
    const exists = !!pick(info, 'Exists');

    preview.innerHTML = '';
    if (exists) {
        const img = doc.createElement('img');
        const stamp = encodeURIComponent(pick(info, 'LastModified') || Date.now());
        img.src = ApiClient.getUrl('CustomLogo/Logos/' + slot) + '?v=' + stamp;
        img.alt = slot;
        img.title = (pick(info, 'ContentType') || '') + ', ' + formatSize(pick(info, 'Size') || 0);
        preview.appendChild(img);
    } else {
        const empty = doc.createElement('div');
        empty.className = 'clEmpty';
        empty.textContent = 'Jellyfin default';
        preview.appendChild(empty);
    }

    removeButton.disabled = !exists;
}

async function loadStatus(view) {
    const response = await request('GET', 'CustomLogo/Logos');
    const status = await response.json();
    view.querySelectorAll('.clSlot').forEach((row) => {
        renderSlot(row, pick(status, row.getAttribute('data-slot')));
    });
}

async function loadConfig(view) {
    const config = await ApiClient.getPluginConfiguration(pluginId);
    view.querySelector('#chkEnabled').checked = pick(config, 'Enabled') !== false;
    view.querySelector('#chkIconAsBanner').checked = !!pick(config, 'UseIconAsBannerFallback');
    view.querySelector('#chkIconAsFavicon').checked = pick(config, 'UseIconAsFaviconFallback') !== false;
}

async function refresh(view) {
    Dashboard.showLoadingMsg();
    try {
        await Promise.all([loadStatus(view), loadConfig(view)]);
    } catch (err) {
        Dashboard.alert({ title: 'Custom Logo', message: 'Could not load settings: ' + err.message });
    } finally {
        Dashboard.hideLoadingMsg();
    }
}

async function upload(view, row, file) {
    const slot = row.getAttribute('data-slot');
    if (file.size > 10 * 1024 * 1024) {
        Dashboard.alert({ title: 'Custom Logo', message: 'The file is larger than 10 MB.' });
        return;
    }

    Dashboard.showLoadingMsg();
    try {
        const response = await request('POST', 'CustomLogo/Logos/' + slot, file, file.type || 'application/octet-stream');
        renderSlot(row, await response.json());
    } catch (err) {
        Dashboard.alert({ title: 'Custom Logo', message: 'Upload failed: ' + err.message });
    } finally {
        Dashboard.hideLoadingMsg();
    }
}

async function remove(view, row) {
    const slot = row.getAttribute('data-slot');
    Dashboard.showLoadingMsg();
    try {
        await request('DELETE', 'CustomLogo/Logos/' + slot);
        renderSlot(row, null);
    } catch (err) {
        Dashboard.alert({ title: 'Custom Logo', message: 'Could not remove the logo: ' + err.message });
    } finally {
        Dashboard.hideLoadingMsg();
    }
}

export default function (view) {
    view.querySelectorAll('.clSlot').forEach((row) => {
        const input = row.querySelector('input[type=file]');
        row.querySelector('.btnUpload').addEventListener('click', () => input.click());
        input.addEventListener('change', () => {
            const file = input.files && input.files[0];
            input.value = '';
            if (file) {
                upload(view, row, file);
            }
        });
        row.querySelector('.btnRemove').addEventListener('click', () => remove(view, row));
    });

    view.querySelector('.customLogoConfigForm').addEventListener('submit', async (e) => {
        e.preventDefault();
        Dashboard.showLoadingMsg();
        try {
            const config = await ApiClient.getPluginConfiguration(pluginId);
            config.Enabled = view.querySelector('#chkEnabled').checked;
            config.UseIconAsBannerFallback = view.querySelector('#chkIconAsBanner').checked;
            config.UseIconAsFaviconFallback = view.querySelector('#chkIconAsFavicon').checked;
            const result = await ApiClient.updatePluginConfiguration(pluginId, config);
            Dashboard.processPluginConfigurationUpdateResult(result);
        } catch (err) {
            Dashboard.hideLoadingMsg();
            Dashboard.alert({ title: 'Custom Logo', message: 'Could not save: ' + err.message });
        }

        return false;
    });

    view.addEventListener('viewshow', () => refresh(view));
}
