const storageKey = 'monitor';
const requestFilter = {
  urls: ['http://127.0.0.1/*']
};
const maximumEntries = 1000;
const maximumBodyBytes = 4096;
const requestDetails = new Map();
const safeHeaders = new Set([
  'accept', 'accept-encoding', 'accept-language', 'cache-control', 'content-encoding',
  'content-language', 'content-length', 'content-type', 'date', 'etag', 'expires',
  'if-modified-since', 'if-none-match', 'last-modified', 'pragma', 'range', 'vary'
]);
let pending = Promise.resolve();

chrome.sidePanel.setPanelBehavior({
  openPanelOnActionClick: true
}).catch(error => {
  console.error('Ascod API:', error);
});

function updateMonitor(update) {
  const operation = pending.then(async () => {
    const saved = await chrome.storage.session.get(storageKey);
    const monitor = saved[storageKey] ?? {
      recording: false,
      entries: []
    };
    if (update(monitor)) {
      await chrome.storage.session.set({
        [storageKey]: monitor
      });
    }
    return monitor;
  });
  pending = operation.catch(error => {
    console.error('Ascod API:', error);
  });
  return operation;
}

function localUrl(value) {
  const url = new URL(value);
  return url.origin === 'http://127.0.0.1:8091' ? url : null;
}

function selectHeaders(headers = []) {
  return headers.filter(header => safeHeaders.has(header.name.toLowerCase()))
    .map(header => {
      return { name: header.name, value: header.value?.slice(0, 512) };
    });
}

function describeBody(body) {
  if (!body) {
    return undefined;
  }
  if (body.error) {
    return { kind: 'Unavailable' };
  }
  if (body.formData) {
    return { kind: 'Form', fields: Object.keys(body.formData).slice(0, 32) };
  }
  const chunks = body.raw ?? [];
  const bytes = chunks.reduce((size, chunk) => size + (chunk.bytes?.byteLength ?? 0), 0);
  if (!bytes || bytes > maximumBodyBytes || chunks.some(chunk => !chunk.bytes)) {
    return { kind: 'Upload', availableBytes: bytes };
  }
  const buffer = new Uint8Array(bytes);
  let offset = 0;
  for (const chunk of chunks) {
    const data = new Uint8Array(chunk.bytes);
    buffer.set(data, offset);
    offset += data.length;
  }
  try {
    const json = JSON.parse(new TextDecoder('utf-8', { fatal: true }).decode(buffer));
    const fields = json && typeof json === 'object' && !Array.isArray(json) ? Object.keys(json).slice(0, 32) : [];
    return { kind: 'JSON', availableBytes: bytes, fields };
  } catch {
    return { kind: 'Raw upload', availableBytes: bytes };
  }
}

function startRequest(details) {
  const url = localUrl(details.url);
  if (!url) {
    return;
  }
  updateMonitor(monitor => {
    if (monitor.recording) {
      if (requestDetails.size >= maximumEntries && !requestDetails.has(details.requestId)) {
        requestDetails.delete(requestDetails.keys().next().value);
      }
      const previous = requestDetails.get(details.requestId);
      requestDetails.set(details.requestId, {
        startedAt: previous?.startedAt ?? details.timeStamp,
        redirects: previous?.redirects ?? [],
        body: describeBody(details.requestBody)
      });
    }
    return false;
  });
}

function recordRequest(details, redirect = false) {
  const url = new URL(details.url);
  if (url.origin !== 'http://127.0.0.1:8091') {
    return;
  }
  updateMonitor(monitor => {
    const request = requestDetails.get(details.requestId);
    if (redirect && request && request.redirects.length < 10) {
      request.redirects.push({
        status: details.statusCode,
        path: localUrl(details.redirectUrl)?.pathname ?? '[external]'
      });
    } else if (!redirect) {
      requestDetails.delete(details.requestId);
    }
    if (!monitor.recording) {
      return false;
    }
    const headers = details.responseHeaders ?? [];
    const contentType = headers.find(header => header.name.toLowerCase() === 'content-type')?.value;
    const contentLength = headers.find(header => header.name.toLowerCase() === 'content-length')?.value;
    monitor.entries.push({
      time: new Date(details.timeStamp).toISOString(),
      tabId: details.tabId,
      method: details.method,
      path: url.pathname,
      resourceType: details.type,
      queryKeys: [...new Set(url.searchParams.keys())].slice(0, 32),
      initiator: details.initiator,
      frameId: details.frameId,
      durationMs: request ? Math.max(0, details.timeStamp - request.startedAt) : undefined,
      requestHeaders: request?.headers,
      requestBody: request?.body,
      responseHeaders: selectHeaders(headers),
      redirects: request?.redirects.slice(),
      contentType,
      contentLength,
      fromCache: details.fromCache,
      ...(details.error ? { error: details.error } : { status: details.statusCode })
    });
    if (monitor.entries.length > maximumEntries) {
      monitor.entries.shift();
    }
    return true;
  });
}

chrome.webRequest.onBeforeRequest.addListener(startRequest, requestFilter, ['requestBody']);
chrome.webRequest.onSendHeaders.addListener(details => {
  if (!localUrl(details.url)) {
    return;
  }
  updateMonitor(() => {
    const request = requestDetails.get(details.requestId);
    if (request) {
      request.headers = selectHeaders(details.requestHeaders);
    }
    return false;
  });
}, requestFilter, ['requestHeaders']);
chrome.webRequest.onBeforeRedirect.addListener(details => {
  recordRequest(details, true);
}, requestFilter, ['responseHeaders']);
chrome.webRequest.onCompleted.addListener(recordRequest, requestFilter, ['responseHeaders']);
chrome.webRequest.onErrorOccurred.addListener(recordRequest, requestFilter);
chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (sender.id !== chrome.runtime.id || sender.url !== chrome.runtime.getURL('panel.html')) {
    return false;
  }
  const command = message?.command;
  if (!['start', 'stop', 'clear'].includes(command)) {
    return false;
  }
  updateMonitor(monitor => {
    if (command === 'clear') {
      monitor.entries = [];
      requestDetails.clear();
    } else {
      monitor.recording = command === 'start';
      if (!monitor.recording) {
        requestDetails.clear();
      }
    }
    return true;
  }).then(() => {
    sendResponse({ ok: true });
  }, error => {
    sendResponse({
      ok: false,
      error: error.message
    });
  });
  return true;
});