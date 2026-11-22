importScripts('filter-redaction.js');

const storageKey = 'monitor';
const directContextKey = 'directFilterContext';
const directRequests = new Set();
const defaultOrigin = 'http://127.0.0.1:8091';
const requestFilter = {
  urls: ['http://*/*', 'https://*/*']
};
const maximumEntries = 1000;
const maximumBodyBytes = 4096;
const requestDetails = new Map();
const activeDirectDownloads = new Set();
const zipCountersToCompare = new Map();
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
  if (!/\.(?:ashx|aspx|svc)(?:\/|$)/i.test(url.pathname) &&
      !(capture.workflowId && ['main_frame', 'sub_frame', 'xmlhttprequest'].includes(details.type))) {
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
  const isArchive = details.statusCode >= 200 && details.statusCode < 300 &&
    (declaredZip || !capture.workflowId && archiveCandidate);
  if (capture.workflowId) {
    const staticPath = /^\/askod-ssl\/(?:[a-z]+\/)*[a-z][a-z0-9]*\.(?:ashx|aspx|svc)(?:\/[a-z][a-z0-9]*)?$/i;
    const entry = {
      time: new Date(details.timeStamp).toISOString(),
      method: details.method,
      path: staticPath.test(url.pathname) && !/\d{5,}/.test(url.pathname) ? url.pathname : '[masked path]',
      status: details.statusCode,
      contentType,
      parameters: [...new Set(url.searchParams.keys())]
        .filter(name => /^[A-Za-z][A-Za-z0-9_-]{0,23}$/.test(name)).slice(0, 32)
    };
    if (isArchive) {
      capture.archive = entry;
      capture.active = false;
    } else {
      capture.preceding.push(entry);
      if (capture.preceding.length > 200) {
        capture.preceding.shift();
      }
    }
    return true;
  }
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
  const directRequest = directRequests.has(details.tabId);
  const directDownload = details.frameId === 0 && activeDirectDownloads.has(details.tabId);
  updateMonitor(async monitor => {
    const capturedFilter = !directRequest && await captureFilterPayload(monitor, details, url);
    const capturedContext = !directRequest && !capturedFilter && await captureRegistryContext(monitor, details, url);
    if ((monitor.recording && url.origin === (monitor.origin ?? defaultOrigin)) ||
        (monitor.zipCapture?.active && url.origin === monitor.zipCapture.origin)) {
      if (requestDetails.size >= maximumEntries && !requestDetails.has(details.requestId)) {
        requestDetails.delete(requestDetails.keys().next().value);
      }
      const previous = requestDetails.get(details.requestId);
      requestDetails.set(details.requestId, {
        startedAt: previous?.startedAt ?? details.timeStamp,
        redirects: previous?.redirects ?? [],
        body: describeBody(details.requestBody),
        directDownload
      });
    }
    return capturedFilter || capturedContext;
  });
}

function readGetDataPayload(details) {
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
  return JSON.parse(new TextDecoder('utf-8', { fatal: true }).decode(data));
}

async function rememberRegistryContext(monitor, details, url, payload, dateIndex) {
  await chrome.storage.session.set({
    [directContextKey]: {
      origin: url.origin,
      tabId: details.tabId,
      documentId: details.documentId,
      path: url.pathname,
      dateIndex,
      payload
    }
  });
  monitor.directContextOrigin = url.origin;
}

async function captureRegistryContext(monitor, details, url) {
  if (details.method !== 'POST' || details.tabId < 0 || details.frameId !== 0 ||
      !/^\/askod-ssl\/services\/CardsServiceSub\.svc\/GetData$/i.test(url.pathname)) {
    return false;
  }
  const settings = await chrome.storage.local.get('serverOrigin');
  if (url.origin !== (settings.serverOrigin ?? monitor.origin ?? defaultOrigin)) {
    return false;
  }
  let payload;
  try {
    payload = readGetDataPayload(details);
  } catch {
    return false;
  }
  if (!Array.isArray(payload?.filterExpression) || !payload.userSettings?.GridParameters) {
    return false;
  }
  const dateFilters = payload.filterExpression.filter(item =>
    item?.FieldName === 'RegDate' || item?._fieldName === 'RegDate' || item?.ColumnUniqueName === 'RegDate');
  if (dateFilters.length > 1 || (dateFilters.length === 1 &&
      (dateFilters[0].FilterFunction !== 5 || dateFilters[0]._filterFunction !== 5))) {
    return false;
  }
  await rememberRegistryContext(monitor, details, url, payload, payload.filterExpression.indexOf(dateFilters[0]));
  return true;
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
    const payload = readGetDataPayload(details);
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
      await rememberRegistryContext(monitor, details, url, payload, payload.filterExpression.indexOf(matching[0]));
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
    return { error: 'Reopen the Ascod registry to refresh userSettings.' };
  }
  try {
    const registryName = window.GetCurrentEntityName?.();
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
      const data = JSON.parse(text);
      const rows = Array.isArray(data?.d) ? data.d : [];
      const resultId = crypto.randomUUID();
      const selectionKey = Symbol.for('unitoffice.ascod.direct-download');
      const cards = new Map();
      const documents = rows.map((row, index) => {
        const key = String(index);
        if (Number.isSafeInteger(row?.Counter) && row.Counter > 0 && typeof row.Token === 'string' && row.Token) {
          cards.set(key, { counter: row.Counter, token: row.Token, row, registryName });
        }
        return {
          key,
          number: row?.FullIndex ?? row?.RegIndex ?? row?.Counter,
          subject: typeof row?.Content === 'string' ? row.Content : '',
          downloadable: cards.has(key),
          openable: cards.has(key) && !!registryName && row.AllowEdit !== false
        };
      });
      const readCard = (id, key) => id === resultId ? cards.get(key) : undefined;
      window[selectionKey] = readCard;
      setTimeout(() => {
        if (window[selectionKey] === readCard) {
          delete window[selectionKey];
        }
      }, 300000);
      const redacted = JSON.parse(JSON.stringify(data, (key, value) =>
        /token|secret|password|cookie|authorization|session|credential/i.test(key) ? '[redacted]' : value));
      return { data: redacted, documents, resultId };
    } catch {
      return { error: 'GetData did not return JSON. Check that the registry session is still signed in.' };
    }
  } catch {
    return { error: 'GetData request failed. Check the connection and the current registry session.' };
  }
}

async function downloadAscodZip(origin, resultId, rowKey) {
  if (location.origin !== origin || !/\/Defaultv4\.aspx$/i.test(location.pathname)) {
    return { error: 'Reopen the Ascod registry and load the documents again.' };
  }
  const readCard = window[Symbol.for('unitoffice.ascod.direct-download')];
  const card = typeof readCard === 'function' ? readCard(resultId, rowKey) : undefined;
  if (!card) {
    return { error: 'Load the documents again to refresh the selection.' };
  }
  async function post(servicePath, payload, name) {
    const response = await fetch('/askod-ssl/Services/' + servicePath, {
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
      throw new Error(name + ' returned HTTP ' + response.status + '.');
    }
    try {
      return (await response.json()).d;
    } catch {
      throw new Error(name + ' did not return the expected JSON.');
    }
  }

  let step = 'Checking card access';
  try {
    const accessResult = await post('CardsService.svc/CheckAccessToCard', {
      cardCounter: card.counter,
      token: card.token
    }, 'CheckAccessToCard');
    const access = typeof accessResult === 'string' ? JSON.parse(accessResult) : accessResult;
    if (typeof access?.Token !== 'string' || !access.Token) {
      return { error: 'The access check did not return a card token.' };
    }
    step = 'Loading attachments';
    const attachments = await post('PdfViewerService.svc/FillEcdTreeViewAndSelect', {
      defTypeFrame: 1,
      cardCounter: card.counter,
      strParentCounter: '',
      isManResolExec: false,
      isLoadFirstFile: false,
      execCounter: null,
      accessToAddFiles: true,
      isRemovedCard: false,
      selectCounter: null,
      selectedName: null,
      token: access.Token,
      isHistoricalData: false
    }, 'FillEcdTreeViewAndSelect');
    const [selectedId, tree] = typeof attachments === 'string' ? attachments.split('#sep#') : [];
    const list = JSON.parse(tree ?? 'null');
    const files = Array.isArray(list) ? list.filter(item =>
      item?.IsFolder === false && item.IsAccess !== false &&
      typeof item.GuidClientId === 'string' && item.GuidClientId.trim()) : [];
    const file = files.find(item => String(item.Id) === selectedId) ?? files[0];
    if (!file) {
      return { error: 'No downloadable file was found in the root of the attachment tree.' };
    }
    step = 'Downloading ZIP';
    const url = new URL('/askod-ssl/Handlers/FileHandler.ashx', origin);
    url.search = new URLSearchParams({ t: 'files', l: 'yes', counter: file.GuidClientId });
    const response = await fetch(url, {
      credentials: 'same-origin',
      redirect: 'error',
      cache: 'no-store',
      signal: AbortSignal.timeout(120000)
    });
    if (!response.ok) {
      return { error: 'ZIP request returned HTTP ' + response.status + '.' };
    }
    const archive = await response.blob();
    const prefix = new Uint8Array(await archive.slice(0, 4).arrayBuffer());
    if (prefix[0] !== 0x50 || prefix[1] !== 0x4b ||
        !((prefix[2] === 3 && prefix[3] === 4) || (prefix[2] === 5 && prefix[3] === 6))) {
      if (/^text\/html(?:;|$)/i.test(response.headers.get('content-type') ?? '')) {
        const html = await archive.slice(0, 65536).text();
        // Проверяем признаки страницы в памяти. Текст и ссылки не экспортируем.
        if (/<input\b[^>]*\btype\s*=\s*(?:"password"|'password'|password(?=[\s/>]))/i.test(html)) {
          return { error: 'The file handler returned a sign-in page (HTTP 200).' };
        }
        const text = html.replace(/<[^>]*>/g, ' ');
        if (/access denied|forbidden|доступ заборонено|доступ заборонений|відмовлено в доступі|доступ запрещ[её]н/i.test(text)) {
          return { error: 'The file handler returned an access-denied page (HTTP 200).' };
        }
        if (/file not found|файл не знайдено|файл не найден/i.test(text)) {
          return { error: 'The file handler returned a file-not-found page (HTTP 200).' };
        }
        return { error: 'The file handler returned an unrecognized HTML page (HTTP 200) instead of ZIP.' };
      }
      return { error: 'The file handler did not return a ZIP archive.' };
    }
    const objectUrl = URL.createObjectURL(archive);
    const link = document.createElement('a');
    link.href = objectUrl;
    link.download = 'ascod-card-files.zip';
    link.click();
    setTimeout(() => URL.revokeObjectURL(objectUrl), 60000);
    return { ok: true };
  } catch {
    return { error: step + ' failed. Check the registry session and try again.' };
  }
}

function openAscodCard(origin, resultId, rowKey) {
  if (location.origin !== origin || !/\/Defaultv4\.aspx$/i.test(location.pathname)) {
    return { error: 'Open the registry and load the documents again.' };
  }
  const readCard = window[Symbol.for('unitoffice.ascod.direct-download')];
  const card = typeof readCard === 'function' ? readCard(resultId, rowKey) : undefined;
  if (!card || typeof window.EditRecord !== 'function' || !card.registryName ||
      card.registryName !== window.GetCurrentEntityName?.()) {
    return { error: 'Return to the original registry and load the documents again.' };
  }
  try {
    const row = card.row;
    const tab = window.GetSelectedTab();
    if (row.AllowEdit === false || !window.getGridMasterTableView() || window.IsBlockMainCard() ||
        ['OverTheSign', 'Reminder', 'ServicesMessagesInfo', 'NotificationTab'].includes(tab)) {
      return { error: 'Card opening is unavailable in this registry context.' };
    }
    if (row.CheckVirtualCard || Number(row.EntityId) === 200 || row.AdvRepId != null ||
        ['UsersOnLine', 'BlockCards', 'BlockUsers', 'AdvancedReports', 'ReportNetEditor'].includes(card.registryName)) {
      return { error: 'This row uses a specialized registry command.' };
    }
    const command = window.ReloadGridCookie('EditRecord', null);
    if (command.IsReturn || !command.EntityName) {
      return { error: 'The registry did not enable the edit command.' };
    }
    window.EditRecord(row, command.EntityName);
    window.SetBindCommandGrid();
    return { ok: true };
  } catch {
    return { error: 'The registry could not run its card-opening command.' };
  }
}

async function runDirectRowAction(message) {
  const saved = await chrome.storage.session.get(directContextKey);
  const context = saved[directContextKey];
  if (!context || context.origin !== message.origin ||
      typeof message.resultId !== 'string' || typeof message.rowKey !== 'string') {
    throw new Error('Load the documents again.');
  }
  const target = { tabId: context.tabId };
  if (context.documentId) {
    target.documentIds = [context.documentId];
  }
  if (message.command === 'openCard') {
    const [execution] = await chrome.scripting.executeScript({
      target,
      world: 'MAIN',
      func: openAscodCard,
      args: [context.origin, message.resultId, message.rowKey]
    });
    if (!execution?.result?.ok) {
      throw new Error(execution?.result?.error ?? 'The registry did not receive the open command.');
    }
    return;
  }
  if (activeDirectDownloads.has(context.tabId)) {
    throw new Error('A ZIP request is already running in this tab.');
  }
  zipCountersToCompare.delete(context.tabId);
  activeDirectDownloads.add(context.tabId);
  let execution;
  try {
    [execution] = await chrome.scripting.executeScript({
      target,
      world: 'MAIN',
      func: downloadAscodZip,
      args: [context.origin, message.resultId, message.rowKey]
    });
  } finally {
    activeDirectDownloads.delete(context.tabId);
  }
  if (!execution?.result?.ok) {
    throw new Error(execution?.result?.error ?? 'ZIP download did not start.');
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
    throw new Error('Open or refresh the Ascod registry and wait for the green userSettings indicator.');
  }
  const capture = saved[storageKey]?.filterCapture;
  if (capture?.date && capture.deadline > Date.now()) {
    throw new Error('Finish or clear the active manual capture first.');
  }
  const payload = structuredClone(context.payload);
  const date = message.date.split('-').reverse().join('.');
  if (context.dateIndex < 0) {
    payload.filterExpression.push({
      ColumnUniqueName: 'RegDate',
      DataTypeName: 'System.DateTime',
      FieldName: 'RegDate',
      FieldValue: date,
      FilterFunction: 5,
      FilterName: 'RegDate',
      _columnUniqueName: 'RegDate',
      _dataTypeName: 'System.DateTime',
      _fieldName: 'RegDate',
      _fieldValue: date,
      _filterFunction: 5
    });
  } else {
    const filter = payload.filterExpression[context.dateIndex];
    filter.FieldValue = date;
    filter._fieldValue = date;
  }
  payload.startRowIndex = 0;
  const target = { tabId: context.tabId };
  if (context.documentId) {
    target.documentIds = [context.documentId];
  }
  let execution;
  if (directRequests.has(context.tabId)) {
    throw new Error('A direct filter is already running in this tab.');
  }
  directRequests.add(context.tabId);
  try {
    [execution] = await chrome.scripting.executeScript({
      target,
      world: 'MAIN',
      func: getAscodRows,
      args: [context.origin, context.path, payload]
    });
  } catch {
    throw new Error('The registry tab changed or closed. Reopen the registry to refresh userSettings.');
  } finally {
    directRequests.delete(context.tabId);
  }
  if (!execution?.result || execution.result.error) {
    throw new Error(execution?.result?.error ?? 'GetData did not return a result.');
  }
  return execution.result;
}

function clearClosedContext(tabId) {
  updateMonitor(async monitor => {
    const saved = await chrome.storage.session.get(directContextKey);
    if (saved[directContextKey]?.tabId === tabId) {
      await chrome.storage.session.remove(directContextKey);
      delete monitor.directContextOrigin;
      return true;
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

function compareZipCounter(details, url, request, contentType) {
  if (details.method !== 'GET' || details.tabId < 0 || details.statusCode < 200 || details.statusCode >= 300 ||
      !/\/Handlers\/FileHandler\.ashx$/i.test(url.pathname) ||
      url.searchParams.get('t') !== 'files' || url.searchParams.get('l') !== 'yes') {
    return undefined;
  }
  const counter = url.searchParams.get('counter');
  if (!counter) {
    return undefined;
  }
  if (request?.directDownload && details.type === 'xmlhttprequest') {
    const previous = { origin: url.origin, counter, deadline: Date.now() + 120000 };
    zipCountersToCompare.set(details.tabId, previous);
    setTimeout(() => {
      if (zipCountersToCompare.get(details.tabId) === previous) {
        zipCountersToCompare.delete(details.tabId);
      }
    }, 120000);
    return undefined;
  }
  const previous = zipCountersToCompare.get(details.tabId);
  const isZip = /^application\/(?:zip|x-zip-compressed)(?:;|$)/i.test(contentType ?? '') ||
    (details.responseHeaders ?? []).some(header =>
      header.name.toLowerCase() === 'content-disposition' && /\.zip(?:["';\s]|$)/i.test(header.value ?? ''));
  if (details.frameId <= 0 || details.type === 'xmlhttprequest' || !isZip ||
      previous?.origin !== url.origin || previous.deadline < Date.now()) {
    return undefined;
  }
  zipCountersToCompare.delete(details.tabId);
  return previous.counter === counter;
}

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
    const matchesDirectZipCounter = !redirect && !details.error ? compareZipCounter(details, url, request, contentType) : undefined;
    if (matchesDirectZipCounter !== undefined) {
      addDownloadLog(monitor, 'Direct and manual ZIP counter match: ' + matchesDirectZipCounter, details.timeStamp);
    }
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
      matchesDirectZipCounter,
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
    runDirectFilter(message).then(result => {
      sendResponse({ ok: true, ...result });
    }).catch(error => {
      sendResponse({ ok: false, error: error.message });
    });
    return true;
  }
  if (command === 'directDownload' || command === 'openCard') {
    runDirectRowAction(message).then(() => {
      sendResponse({ ok: true });
    }).catch(error => {
      sendResponse({ ok: false, error: error.message });
    });
    return true;
  }
  if (!['start', 'autoStart', 'stop', 'clear', 'prepareDownload', 'downloadResult', 'captureZip', 'cancelZip',
        'captureFilter', 'clearFilters'].includes(command)) {
    return false;
  }
  let origin;
  if (command === 'start' || command === 'autoStart') {
    const url = URL.canParse(message.origin) ? new URL(message.origin) : null;
    if (!url || !['http:', 'https:'].includes(url.protocol) || url.origin !== message.origin) {
      sendResponse({ ok: false, error: 'Enter a valid HTTP or HTTPS server address.' });
      return false;
    }
    origin = url.origin;
  }
  updateMonitor(async monitor => {
    if (command === 'autoStart') {
      const settings = await chrome.storage.local.get('serverOrigin');
      if (monitor.recording || monitor.paused || origin !== (settings.serverOrigin ?? defaultOrigin)) {
        return false;
      }
    }
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
      delete monitor.directContextOrigin;
      await chrome.storage.session.remove(directContextKey);
    } else if (command === 'captureZip') {
      const url = URL.canParse(message.origin) ? new URL(message.origin) : null;
      if (!url || !['http:', 'https:'].includes(url.protocol) || url.origin !== message.origin) {
        throw new Error('Invalid server address.');
      }
      monitor.zipCapture = {
        active: true,
        origin: url.origin,
        workflowId: typeof message.workflowId === 'string' ? message.workflowId : undefined,
        deadline: Date.now() + 120000,
        salt: crypto.randomUUID(),
        preceding: []
      };
    } else if (command === 'cancelZip') {
      if (!message.workflowId || monitor.zipCapture?.workflowId === message.workflowId) {
        delete monitor.zipCapture;
      }
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
      zipCountersToCompare.clear();
    } else {
      monitor.recording = command !== 'stop';
      monitor.paused = !monitor.recording;
      requestDetails.clear();
      zipCountersToCompare.clear();
      if (monitor.recording) {
        monitor.origin = origin;
      }
      addDownloadLog(monitor, monitor.recording ? 'Recording started for ' + origin : 'Recording stopped.');
    }
    return true;
  }).then(async () => {
    if (command === 'start') {
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