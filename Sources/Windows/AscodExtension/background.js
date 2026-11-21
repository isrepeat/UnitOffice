importScripts('filter-redaction.js');

const storageKey = 'monitor';
const directContextKey = 'directFilterContext';
const defaultOrigin = 'http://127.0.0.1:8091';
const requestFilter = {
  urls: ['http://*/*', 'https://*/*']
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
    if (await update(monitor)) {
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

async function captureZipRequest(monitor, details, url) {
  const capture = monitor.zipCapture;
  if (!capture?.active || capture.origin !== url.origin) {
    return false;
  }
  if (capture.deadline < Date.now()) {
    capture.active = false;
    delete capture.salt;
    return true;
  }
  if (!/\.(?:ashx|aspx|svc)(?:\/|$)/i.test(url.pathname)) {
    return false;
  }
  const headers = details.responseHeaders ?? [];
  const header = name => headers.find(item => item.name.toLowerCase() === name)?.value ?? '';
  const contentType = header('content-type').split(';')[0].trim().toLowerCase();
  const disposition = header('content-disposition');
  const zipDisposition = /\.zip(?:["';\s]|$)/i.test(disposition);
  const declaredZip = ['application/zip', 'application/x-zip-compressed'].includes(contentType) || zipDisposition;
  const archiveCandidate = contentType === 'application/octet-stream' && !disposition &&
    /\/Handlers\/FileHandler\.ashx$/i.test(url.pathname);
  const isArchive = details.statusCode >= 200 && details.statusCode < 300 && (declaredZip || archiveCandidate);
  const parameters = [];
  for (const [name, value] of url.searchParams) {
    if (parameters.length >= 32) {
      break;
    }
    const safeValue = name.toLowerCase() === 't' && /^(?:zip|pdf|archive)$/i.test(value);
    let masked = value;
    if (value && !safeValue) {
      const bytes = new TextEncoder().encode(capture.salt + '\n' + value);
      const digest = await crypto.subtle.digest('SHA-256', bytes);
      const alias = Array.from(new Uint8Array(digest).slice(0, 8), byte => byte.toString(16).padStart(2, '0')).join('');
      masked = '<value-' + alias + '>';
    }
    parameters.push({
      name: name.slice(0, 120),
      value: masked
    });
  }
  const request = requestDetails.get(details.requestId);
  const headerNames = new Set(['accept', 'content-type', 'content-length', 'content-encoding', 'cache-control']);
  const responseHeaders = selectHeaders(headers).filter(item => headerNames.has(item.name.toLowerCase()));
  if (disposition) {
    const kind = disposition.split(';')[0].trim().toLowerCase();
    responseHeaders.push({
      name: 'Content-Disposition',
      value: (['attachment', 'inline'].includes(kind) ? kind : '[masked]') +
        (zipDisposition ? '; filename="[masked].zip"' : '')
    });
  }
  const entry = {
    time: new Date(details.timeStamp).toISOString(),
    method: details.method,
    path: url.pathname.replace(/[0-9a-f]{8}-[0-9a-f-]{27,}|\d{5,}/gi, '[id]'),
    parameters,
    requestHeaders: (request?.headers ?? []).filter(item => headerNames.has(item.name.toLowerCase())),
    requestBodySummary: request?.body,
    status: details.statusCode,
    responseHeaders
  };
  if (isArchive) {
    entry.archiveDetection = declaredZip ? 'ZIP response headers' : 'Binary response from the Ascod file handler';
    capture.archive = entry;
    capture.active = false;
    delete capture.salt;
  } else {
    capture.preceding.push(entry);
    if (capture.preceding.length > 20) {
      capture.preceding.shift();
    }
  }
  return true;
}

function addDownloadLog(monitor, message, time = Date.now()) {
  monitor.downloadLog ??= [];
  monitor.downloadLog.push({
    time: new Date(time).toISOString(),
    message
  });
  if (monitor.downloadLog.length > 100) {
    monitor.downloadLog.shift();
  }
}

chrome.downloads.onCreated.addListener(item => {
  const filename = item.filename.split(/[/\\]/).pop();
  const url = URL.canParse(item.finalUrl || item.url) ? new URL(item.finalUrl || item.url) : null;
  if (!url || !/\/Handlers\/FileHandler\.ashx$/i.test(url.pathname) ||
      (filename && !/^card_files_\d+\.zip$/i.test(filename))) {
    return;
  }
  updateMonitor(monitor => {
    const batch = monitor.downloadBatch;
    if (!batch || batch.origin !== url.origin || batch.expiresAt < Date.now() || batch.remaining <= 0) {
      return false;
    }
    batch.remaining--;
    batch.observed++;
    monitor.downloads ??= {};
    monitor.downloads[item.id] = filename || 'download #' + item.id;
    addDownloadLog(monitor, 'Edge started download: ' + monitor.downloads[item.id]);
    return true;
  });
});

chrome.downloads.onChanged.addListener(delta => {
  const state = delta.state?.current;
  if (state !== 'complete' && state !== 'interrupted') {
    return;
  }
  updateMonitor(monitor => {
    const destination = monitor.downloads?.[delta.id];
    if (!destination) {
      return false;
    }
    delete monitor.downloads[delta.id];
    const result = state === 'complete' ? 'Saved' : 'Interrupted';
    const filename = delta.filename?.current ?? destination;
    addDownloadLog(monitor, result + ': ' + filename + (delta.error?.current ? ' (' + delta.error.current + ')' : ''));
    return true;
  });
});

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
  const url = new URL(details.url);
  updateMonitor(async monitor => {
    const capturedFilter = await captureFilterPayload(monitor, details, url);
    if ((monitor.recording && url.origin === (monitor.origin ?? defaultOrigin)) ||
        (monitor.zipCapture?.active && url.origin === monitor.zipCapture.origin)) {
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
    return capturedFilter;
  });
}

async function captureFilterPayload(monitor, details, url) {
  const capture = monitor.filterCapture;
  if (!capture?.date || capture.tabId !== details.tabId || capture.origin !== url.origin || details.method !== 'POST' ||
      !/\/CardsServiceSub\.svc\/GetData$/i.test(url.pathname)) {
    return false;
  }
  const date = capture.date;
  delete capture.date;
  if (Date.now() > capture.deadline) {
    capture.status = 'Capture expired.';
    return true;
  }
  try {
    const chunks = details.requestBody?.raw ?? [];
    const size = chunks.reduce((sum, chunk) => sum + (chunk.bytes?.byteLength ?? 0), 0);
    if (!size || size > 65536 || chunks.some(chunk => !chunk.bytes)) {
      throw new Error('GetData body is unavailable or too large.');
    }
    const data = new Uint8Array(size);
    let offset = 0;
    for (const chunk of chunks) {
      const bytes = new Uint8Array(chunk.bytes);
      data.set(bytes, offset);
      offset += bytes.length;
    }
    const payload = JSON.parse(new TextDecoder('utf-8', { fatal: true }).decode(data));
    if (!payload || !Object.hasOwn(payload, 'filterExpression')) {
      throw new Error('GetData body does not contain filterExpression.');
    }
    const matching = Array.isArray(payload.filterExpression)
      ? payload.filterExpression.filter(item => item?.FieldName === 'RegDate')
      : [];
    const expectedDate = date.split('-').reverse().join('.');
    if (matching.length === 1 && matching[0].FieldValue === expectedDate &&
        matching[0]._fieldValue === expectedDate && matching[0].FilterFunction === 5 &&
        matching[0]._filterFunction === 5 && payload.userSettings && typeof payload.userSettings === 'object') {
      await chrome.storage.session.set({
        [directContextKey]: {
          origin: url.origin,
          tabId: details.tabId,
          documentId: details.documentId,
          path: url.pathname,
          dateIndex: payload.filterExpression.indexOf(matching[0]),
          payload
        }
      });
    } else {
      throw new Error('Apply exactly the selected registration date in the registry and recapture.');
    }
    const sample = {
      date,
      method: details.method,
      path: url.pathname,
      payload: redactPayload(payload, date)
    };
    const index = capture.samples.findIndex(entry => entry.date === date);
    if (index === -1) {
      capture.samples.push(sample);
    } else {
      capture.samples[index] = sample;
    }
    capture.status = 'Captured ' + capture.samples.length + ' of 2 dates.';
  } catch (error) {
    capture.status = error instanceof SyntaxError ? 'GetData body is not valid JSON.' : error.message;
  }
  return true;
}

async function getAscodRows(origin, path, payload) {
  if (location.origin !== origin || !/\/Defaultv4\.aspx$/i.test(location.pathname)) {
    return { error: 'Open the Ascod registry and capture its filter again.' };
  }
  try {
    const response = await fetch(path, {
      method: 'POST',
      credentials: 'same-origin',
      redirect: 'error',
      cache: 'no-store',
      headers: {
        'Content-Type': 'application/json; charset=utf-8',
        Accept: 'application/json'
      },
      body: JSON.stringify(payload),
      signal: AbortSignal.timeout(15000)
    });
    if (!response.ok) {
      return { error: 'GetData returned HTTP ' + response.status + '.' };
    }
    const text = await response.text();
    if (text.length > 2000000) {
      return { error: 'GetData response is too large to display.' };
    }
    try {
      return { data: JSON.parse(text) };
    } catch {
      return { error: 'GetData did not return JSON. Check that the registry session is still signed in.' };
    }
  } catch {
    return { error: 'GetData request failed. Check the connection and the current registry session.' };
  }
}

async function runDirectFilter(message) {
  if (typeof message.date !== 'string' || !/^\d{4}-\d{2}-\d{2}$/.test(message.date)) {
    throw new Error('Choose a registration date.');
  }
  await pending;
  const saved = await chrome.storage.session.get([directContextKey, storageKey]);
  const context = saved[directContextKey];
  if (!context || context.origin !== message.origin) {
    throw new Error('Record one manual date filter on this server first.');
  }
  const capture = saved[storageKey]?.filterCapture;
  if (capture?.date && capture.deadline > Date.now()) {
    throw new Error('Finish or clear the active manual capture first.');
  }
  const payload = structuredClone(context.payload);
  const filter = payload.filterExpression[context.dateIndex];
  const date = message.date.split('-').reverse().join('.');
  filter.FieldValue = date;
  filter._fieldValue = date;
  payload.startRowIndex = 0;
  const target = { tabId: context.tabId };
  if (context.documentId) {
    target.documentIds = [context.documentId];
  }
  let execution;
  try {
    [execution] = await chrome.scripting.executeScript({
      target,
      world: 'MAIN',
      func: getAscodRows,
      args: [context.origin, context.path, payload]
    });
  } catch {
    throw new Error('The captured registry tab changed or closed. Record one manual filter again.');
  }
  if (!execution?.result || execution.result.error) {
    throw new Error(execution?.result?.error ?? 'GetData did not return a result.');
  }
  return execution.result.data;
}

function clearClosedContext(tabId) {
  updateMonitor(async () => {
    const saved = await chrome.storage.session.get(directContextKey);
    if (saved[directContextKey]?.tabId === tabId) {
      await chrome.storage.session.remove(directContextKey);
    }
    return false;
  });
}

chrome.tabs.onRemoved.addListener(clearClosedContext);
chrome.tabs.onUpdated.addListener((tabId, change) => {
  if (change.status === 'loading') {
    clearClosedContext(tabId);
  }
});

function recordRequest(details, redirect = false) {
  const url = new URL(details.url);
  updateMonitor(async monitor => {
    const captured = !redirect && !details.error ? await captureZipRequest(monitor, details, url) : false;
    const recording = monitor.recording && url.origin === (monitor.origin ?? defaultOrigin);
    const request = requestDetails.get(details.requestId);
    if (redirect && request && request.redirects.length < 10) {
      const target = new URL(details.redirectUrl);
      request.redirects.push({
        status: details.statusCode,
        path: target.origin === url.origin ? target.pathname : '[external]'
      });
    } else if (!redirect) {
      requestDetails.delete(details.requestId);
    }
    if (!recording) {
      return captured;
    }
    const headers = details.responseHeaders ?? [];
    const contentType = headers.find(header => header.name.toLowerCase() === 'content-type')?.value;
    const contentLength = headers.find(header => header.name.toLowerCase() === 'content-length')?.value;
    monitor.entries.push({
      time: new Date(details.timeStamp).toISOString(),
      tabId: details.tabId,
      method: details.method,
      origin: url.origin,
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
  const url = new URL(details.url);
  updateMonitor(monitor => {
    if (url.origin !== (monitor.origin ?? defaultOrigin) && url.origin !== monitor.zipCapture?.origin) {
      return false;
    }
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
  if (command === 'directFilter') {
    runDirectFilter(message).then(data => {
      sendResponse({ ok: true, data });
    }).catch(error => {
      sendResponse({ ok: false, error: error.message });
    });
    return true;
  }
  if (!['start', 'stop', 'clear', 'prepareDownload', 'downloadResult', 'captureZip', 'cancelZip',
        'captureFilter', 'clearFilters'].includes(command)) {
    return false;
  }
  let origin;
  if (command === 'start') {
    const url = URL.canParse(message.origin) ? new URL(message.origin) : null;
    if (!url || !['http:', 'https:'].includes(url.protocol) || url.origin !== message.origin) {
      sendResponse({ ok: false, error: 'Enter a valid HTTP or HTTPS server address.' });
      return false;
    }
    origin = url.origin;
  }
  updateMonitor(async monitor => {
    if (command === 'captureFilter') {
      const url = URL.canParse(message.origin) ? new URL(message.origin) : null;
      if (!url || !['http:', 'https:'].includes(url.protocol) || url.origin !== message.origin ||
          typeof message.date !== 'string' || !/^\d{4}-\d{2}-\d{2}$/.test(message.date) ||
          !Number.isInteger(message.tabId) || message.tabId < 0) {
        throw new Error('Choose a server and registration date.');
      }
      const previous = monitor.filterCapture;
      const samples = previous?.origin === url.origin ? previous.samples : [];
      if (samples.length >= 2 && !samples.some(entry => entry.date === message.date)) {
        throw new Error('Clear the two saved samples before recording another date.');
      }
      monitor.filterCapture = {
        origin: url.origin,
        tabId: message.tabId,
        date: message.date,
        deadline: Date.now() + 120000,
        samples,
        status: 'Apply the date manually in the registry within two minutes.'
      };
    } else if (command === 'clearFilters') {
      delete monitor.filterCapture;
      await chrome.storage.session.remove(directContextKey);
    } else if (command === 'captureZip') {
      const url = URL.canParse(message.origin) ? new URL(message.origin) : null;
      if (!url || !['http:', 'https:'].includes(url.protocol) || url.origin !== message.origin) {
        throw new Error('Invalid server address.');
      }
      monitor.zipCapture = {
        active: true,
        origin: url.origin,
        deadline: Date.now() + 120000,
        salt: crypto.randomUUID(),
        preceding: []
      };
    } else if (command === 'cancelZip') {
      delete monitor.zipCapture;
    } else if (command === 'prepareDownload') {
      const url = URL.canParse(message.origin) ? new URL(message.origin) : null;
      if (!url || !['http:', 'https:'].includes(url.protocol) || url.origin !== message.origin ||
          !Number.isInteger(message.count) || message.count < 1 || message.count > 20) {
        throw new Error('Invalid ZIP download settings.');
      }
      monitor.downloadBatch = {
        origin: url.origin,
        expected: message.count,
        remaining: message.count,
        observed: 0,
        expiresAt: Date.now() + 10 * 60 * 1000
      };
      addDownloadLog(monitor, 'Starting batch of ' + message.count + ' at ' + url.origin);
    } else if (command === 'downloadResult') {
      for (const entry of (Array.isArray(message.trace) ? message.trace.slice(0, 80) : [])) {
        if (Number.isFinite(entry?.time) && Math.abs(entry.time) <= 8640000000000000) {
          addDownloadLog(monitor, String(entry.message).slice(0, 200), entry.time);
        }
      }
      addDownloadLog(monitor, String(message.text).slice(0, 500));
      const batch = monitor.downloadBatch;
      if (batch && Number.isInteger(message.requested)) {
        batch.remaining = Math.max(0, message.requested - (batch.expected - batch.remaining));
      }
    } else if (command === 'clear') {
      monitor.entries = [];
      requestDetails.clear();
    } else {
      monitor.recording = command === 'start';
      requestDetails.clear();
      if (monitor.recording) {
        monitor.origin = origin;
      }
    }
    return true;
  }).then(async () => {
    if (origin) {
      await chrome.storage.local.set({ serverOrigin: origin });
    }
    sendResponse({ ok: true });
  }).catch(error => {
    sendResponse({
      ok: false,
      error: error.message
    });
  });
  return true;
});