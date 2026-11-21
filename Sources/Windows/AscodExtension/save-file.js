function localFileUrl(path) {
  const normalized = path.replaceAll('\\', '/');
  const encodePath = value => value.split('/').map(encodeURIComponent).join('/');
  if (/^[a-z]:\//i.test(normalized)) {
    return 'file:///' + normalized.slice(0, 2) + encodePath(normalized.slice(2));
  }
  if (normalized.startsWith('//')) {
    const separator = normalized.indexOf('/', 2);
    if (separator > 2) {
      const host = normalized.slice(2, separator);
      return new URL('file://' + host + encodePath(normalized.slice(separator))).href;
    }
  } else if (normalized.startsWith('/')) {
    return 'file://' + encodePath(normalized);
  }
  throw new Error('Edge did not report an absolute saved file path.');
}

async function waitForSavedFile(id) {
  const deadline = Date.now() + 120000;
  while (Date.now() < deadline) {
    const [item] = await chrome.downloads.search({ id });
    if (item?.state === 'complete') {
      return item.filename;
    }
    if (item?.state === 'interrupted') {
      throw new Error('Save interrupted: ' + (item.error ?? 'unknown reason'));
    }
    await new Promise(resolve => setTimeout(resolve, 250));
  }
  throw new Error('Edge did not confirm the save in time.');
}

async function saveTextAndGetFileUrl(text, filename, type) {
  const url = URL.createObjectURL(new Blob([text], { type }));
  try {
    const id = await chrome.downloads.download({
      url,
      filename,
      saveAs: true,
      conflictAction: 'uniquify'
    });
    return localFileUrl(await waitForSavedFile(id));
  } finally {
    URL.revokeObjectURL(url);
  }
}