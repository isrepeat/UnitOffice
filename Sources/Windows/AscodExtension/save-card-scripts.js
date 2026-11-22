(() => {
  const collectButton = document.getElementById('collect-card-scripts');
  const writeButton = document.getElementById('write-card-scripts');
  const status = document.getElementById('card-scripts-status');
  let selection;

  async function readCardScripts(origin) {
    if (location.origin !== origin || !/^\/askod-ssl\/(?:Defaultv4|WindowEditCard|PdfViewerFrame)\.aspx$/i.test(location.pathname)) {
      return null;
    }
    const scripts = [];
    for (const [index, script] of Array.from(document.scripts).entries()) {
      if (script.type && !/^(?:module|(?:text|application)\/(?:javascript|ecmascript))$/i.test(script.type)) {
        continue;
      }
      const entry = { index, page: location.pathname };
      if (!script.src) {
        scripts.push({ ...entry, source: script.textContent });
        continue;
      }
      const url = new URL(script.src);
      entry.path = url.pathname;
      if (url.origin !== origin) {
        scripts.push({ ...entry, skipped: 'Different origin' });
        continue;
      }
      try {
        const response = await fetch(url.href, { cache: 'force-cache', redirect: 'error' });
        if (!response.ok || !/javascript|ecmascript|application\/octet-stream/i.test(response.headers.get('content-type') ?? '')) {
          scripts.push({ ...entry, skipped: 'Script response unavailable' });
          continue;
        }
        scripts.push({ ...entry, source: await response.text() });
      } catch {
        scripts.push({ ...entry, skipped: 'Script could not be read' });
      }
    }
    return scripts;
  }

  collectButton.onclick = async () => {
    collectButton.disabled = true;
    writeButton.disabled = true;
    selection = undefined;
    try {
      const server = readServerAddress();
      if (!await requestServerPermission(server)) {
        throw new Error('Site permission was not granted.');
      }
      const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
      if (!tab?.id || !URL.canParse(tab.url) || new URL(tab.url).origin !== server.origin) {
        throw new Error('Open the Ascod registry or a card on the selected server first.');
      }
      selection = { tabId: tab.id, origin: server.origin };
      status.textContent = 'Choose Save to folder to export scripts from the current tab.';
    } catch (error) {
      status.textContent = error.message;
    } finally {
      collectButton.disabled = false;
      writeButton.disabled = !selection;
    }
  };

  writeButton.onclick = async () => {
    if (!selection) {
      return;
    }
    writeButton.disabled = true;
    collectButton.disabled = true;
    try {
      const folder = await window.showDirectoryPicker({ id: 'ascod-card-scripts', mode: 'readwrite' });
      status.textContent = 'Reading page scripts…';
      const frames = await chrome.scripting.executeScript({
        target: { tabId: selection.tabId, allFrames: true },
        func: readCardScripts,
        args: [selection.origin]
      });
      if (!frames.some(frame => Array.isArray(frame.result))) {
        throw new Error('No Ascod registry, card, or attachment viewer found.');
      }
      const stamp = new Date().toISOString().replace(/[:.]/g, '-');
      const directory = await folder.getDirectoryHandle('ascod-card-scripts-' + stamp, { create: true });
      const index = [];
      const files = new Map();
      for (const frame of frames) {
        for (const { source, ...metadata } of frame.result ?? []) {
          if (source === undefined) {
            index.push({ frameId: frame.frameId, ...metadata });
            continue;
          }
          const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(source));
          const sha256 = Array.from(new Uint8Array(digest), byte => byte.toString(16).padStart(2, '0')).join('');
          if (!files.has(sha256)) {
            const filename = 'script-' + (files.size + 1) + '.js';
            await writeFolderFile(directory, filename, source);
            files.set(sha256, filename);
          }
          index.push({ frameId: frame.frameId, ...metadata, sha256, file: files.get(sha256) });
        }
      }
      await writeFolderFile(directory, 'index.json', JSON.stringify(index, null, 2));
      status.textContent = 'Saved ' + files.size + ' scripts to ' + directory.name + '.';
    } catch (error) {
      status.textContent = error.name === 'AbortError' ? 'Folder selection cancelled.' : error.message;
    } finally {
      collectButton.disabled = false;
      writeButton.disabled = false;
    }
  };
})();