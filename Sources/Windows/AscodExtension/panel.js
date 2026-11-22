const rows = document.getElementById('requests');
const endpointRows = document.getElementById('endpoints');
const endpointSummary = document.getElementById('endpoint-summary');
const toggle = document.getElementById('toggle');
const clear = document.getElementById('clear');
const exportButton = document.getElementById('export');
const reloadButton = document.getElementById('reload');
const status = document.getElementById('status');
const serverOrigin = document.getElementById('server-origin');
const contextIndicator = document.getElementById('context-indicator');
const contextStatus = document.getElementById('context-status');
const dateFilter = document.getElementById('date-filter');
const registrationDate = document.getElementById('registration-date');
const applyDate = document.getElementById('apply-date');
const dateResult = document.getElementById('date-result');
const downloadFirst = document.getElementById('download-first');
const downloadResult = document.getElementById('download-result');
const downloadCount = document.getElementById('download-count');
const downloadForm = document.getElementById('download-form');
const downloadLog = document.getElementById('download-log');
const logFolder = document.getElementById('download-log-folder');
const saveLog = document.getElementById('save-log');
const copyLog = document.getElementById('copy-log');
const logFileStatus = document.getElementById('log-file-status');
const traceFolder = document.getElementById('trace-folder');
const saveTraces = document.getElementById('save-traces');
const traceFileStatus = document.getElementById('trace-file-status');
const captureZip = document.getElementById('capture-zip');
const captureAttachment = document.getElementById('capture-attachment');
const attachmentStatus = document.getElementById('attachment-status');
const workflowEvidence = document.querySelectorAll('#workflow-evidence [data-evidence]');
const attachmentScriptIds = ['attachment-response', 'attachment-response-bridge'];
const attachmentFilename = 'ascod-download-workflow.json';
let attachmentCapture;
let attachmentTrace;
let attachmentBusy = false;
const exportZip = document.getElementById('export-zip');
const captureStatus = document.getElementById('capture-status');
const filterPayloadForm = document.getElementById('filter-payload-form');
const filterPayloadDate = document.getElementById('filter-payload-date');
const captureFilterPayload = document.getElementById('capture-filter-payload');
const clearFilterPayloads = document.getElementById('clear-filter-payloads');
const filterPayloadStatus = document.getElementById('filter-payload-status');
const operationModes = document.getElementById('operation-modes');
const requestMode = document.getElementById('request-mode');
const interfaceMode = document.getElementById('interface-mode');
const directFilter = document.getElementById('direct-filter');
const directDate = document.getElementById('direct-date');
const directResult = document.getElementById('direct-result');
const directDownloadZip = document.getElementById('direct-download-zip');
const directDownloadStatus = document.getElementById('direct-download-status');
let directSelection;
let downloadingZip = false;
const directDocuments = document.getElementById('direct-documents');
const directJson = document.getElementById('direct-json');
const directJsonDetails = document.getElementById('direct-json-details');
const saveAll = document.getElementById('save-all');
const changeOutputFolder = document.getElementById('change-output-folder');
const outputFolderName = document.getElementById('output-folder-name');
const saveAllStatus = document.getElementById('save-all-status');
let outputDirectory;
let savingAll = false;
let logDirectory;
let confirmedFolderHint;
let traceDirectory;
let confirmedTraceFolderHint;
let savingTraces = false;
let monitor = {
  recording: false,
  entries: []
};
let busy = true;
let savingExport = false;

function showOperationMode(mode) {
  const direct = mode === 'requests';
  requestMode.hidden = !direct;
  interfaceMode.hidden = direct;
  operationModes.querySelector('[value="' + (direct ? 'requests' : 'interface') + '"]').checked = true;
}

operationModes.addEventListener('change', event => {
  const mode = event.target.value;
  showOperationMode(mode);
  chrome.storage.local.set({
    operationMode: mode
  }).catch(error => {
    console.error('Ascod API:', error);
  });
});

directFilter.addEventListener('submit', async event => {
  event.preventDefault();
  if (downloadingZip) {
    return;
  }
  const button = directFilter.querySelector('button');
  button.disabled = true;
  directSelection = undefined;
  directDownloadZip.hidden = true;
  directDownloadZip.disabled = true;
  directDownloadStatus.hidden = true;
  directDocuments.replaceChildren();
  directJson.textContent = '';
  directJsonDetails.hidden = true;
  directJsonDetails.open = false;
  directResult.textContent = 'Loading the registry…';
  try {
    const server = readServerAddress();
    const date = directDate.value;
    if (server.origin !== 'http://127.0.0.1:8091') {
      if (!await requestServerPermission(server)) {
        throw new Error('Site permission was not granted.');
      }
      await chrome.storage.local.set({
        serverOrigin: server.origin
      });
      const result = await chrome.runtime.sendMessage({
        command: 'directFilter',
        origin: server.origin,
        date
      });
      if (!result?.ok) {
        throw new Error(result?.error ?? 'GetData failed.');
      }
      directJson.textContent = JSON.stringify(result.data, null, 2);
      directJsonDetails.hidden = false;
      const documents = result.documents ?? [];
      for (const record of documents) {
        const item = document.createElement('li');
        const label = document.createElement('label');
        const radio = document.createElement('input');
        radio.type = 'radio';
        radio.name = 'direct-document';
        radio.value = record.key;
        radio.disabled = !record.downloadable;
        label.append(radio, [record.number, record.subject].filter(Boolean).join(' — ') || 'Document');
        item.append(label);
        directDocuments.append(item);
      }
      directSelection = { origin: server.origin, resultId: result.resultId };
      directDownloadZip.hidden = !documents.some(record => record.downloadable);
      directResult.textContent = 'Documents on ' + date + ': ' + documents.length + '.';
      return;
    }
    const response = await fetch(new URL('/api/v1/documents', server.origin), {
      cache: 'no-store',
      redirect: 'error'
    });
    if (!response.ok) {
      throw new Error('Registry request failed: HTTP ' + response.status);
    }
    const registry = new DOMParser().parseFromString(await response.text(), 'application/xml');
    if (registry.documentElement.localName !== 'documents') {
      throw new Error('Invalid registry XML.');
    }
    const documents = [...registry.documentElement.children].filter(record =>
      record.localName === 'document' && record.getAttribute('date') === date);
    for (const record of documents) {
      const item = document.createElement('li');
      item.textContent = record.getAttribute('id') + ' — ' + record.getAttribute('subject');
      directDocuments.append(item);
    }
    directResult.textContent = 'Documents on ' + date + ': ' + documents.length + '.';
  } catch (error) {
    directResult.textContent = error.message;
  } finally {
    button.disabled = false;
  }
});

directDocuments.addEventListener('change', () => {
  directDownloadZip.disabled = downloadingZip || !directDocuments.querySelector('input:checked');
});

directDownloadZip.onclick = async () => {
  const selection = directSelection;
  const row = directDocuments.querySelector('input:checked');
  if (downloadingZip || !selection || !row || row.disabled) {
    return;
  }
  downloadingZip = true;
  const loadButton = directFilter.querySelector('button');
  loadButton.disabled = true;
  directDownloadZip.disabled = true;
  directDownloadStatus.hidden = false;
  directDownloadStatus.textContent = 'Preparing ZIP…';
  try {
    if (readServerAddress().origin !== selection.origin) {
      throw new Error('Load documents again for the selected server.');
    }
    const result = await chrome.runtime.sendMessage({
      command: 'directDownload',
      ...selection,
      rowKey: row.value
    });
    if (!result?.ok) {
      throw new Error(result?.error ?? 'ZIP download did not start.');
    }
    directDownloadStatus.textContent = 'ZIP sent to Edge Downloads.';
  } catch (error) {
    directDownloadStatus.textContent = error.message;
  } finally {
    downloadingZip = false;
    directDownloadZip.disabled = false;
    loadButton.disabled = false;
  }
};

function downloadLogText() {
  const entries = [...(monitor.downloadLog ?? [])];
  entries.sort((first, second) => first.time.localeCompare(second.time));
  const lines = entries.map(entry => entry.time + ' ' + entry.message).join('\n');
  return lines ? lines + '\n' : '';
}

async function writeFolderFile(directory, filename, text) {
  const file = await directory.getFileHandle(filename, {
    create: true
  });
  const stream = await file.createWritable();
  try {
    await stream.write(text);
    await stream.close();
  } catch (error) {
    await stream.abort().catch(abortError => {
      console.error('Ascod API:', abortError);
    });
    throw error;
  }
}

saveLog.addEventListener('click', async () => {
  const text = downloadLogText();
  if (!text) {
    logFileStatus.textContent = 'The download log is empty.';
    return;
  }
  if (typeof window.showDirectoryPicker !== 'function') {
    logFileStatus.textContent = 'Edge cannot open a folder picker here.';
    return;
  }
  const path = logFolder.value.trim();
  saveLog.disabled = true;
  logFolder.disabled = true;
  try {
    if (!logDirectory || confirmedFolderHint !== path) {
      logDirectory = await window.showDirectoryPicker({
        id: 'ascod-log-folder',
        mode: 'readwrite'
      });
      confirmedFolderHint = path;
    }
    await chrome.storage.local.set({
      preferredLogFolder: path
    });
    await writeFolderFile(logDirectory, 'AscodExtension.log', text);
    logFileStatus.textContent = 'Saved: ' + logDirectory.name + '/AscodExtension.log';
  } catch (error) {
    if (error.name !== 'AbortError') {
      logFileStatus.textContent = error.message;
    }
  } finally {
    saveLog.disabled = false;
    logFolder.disabled = false;
  }
});

function getZipTrace() {
  const capture = monitor.zipCapture;
  if (capture?.workflowId) {
    return null;
  }
  if (capture?.archive) {
    return {
      origin: capture.origin,
      preceding: capture.preceding,
      archive: capture.archive
    };
  }
  return null;
}

function getJsonTraces() {
  const files = [];
  if (attachmentTrace) {
    files.push([attachmentFilename, attachmentTrace]);
  }
  if (monitor.entries.length > 0) {
    files.push(['ascod-requests.json', monitor.entries]);
  }
  const zipTrace = getZipTrace();
  if (zipTrace) {
    files.push(['ascod-zip-trace.json', zipTrace]);
  }
  if (monitor.filterCapture?.samples?.length) {
    files.push(['ascod-filter-payloads.json', {
      origin: monitor.filterCapture.origin,
      samples: monitor.filterCapture.samples
    }]);
  }
  return files;
}

async function chooseOutputDirectory() {
  if (typeof window.showDirectoryPicker !== 'function') {
    throw new Error('Edge cannot open a folder picker here.');
  }
  const directory = await window.showDirectoryPicker({
    id: 'ascod-export-folder',
    mode: 'readwrite'
  });
  outputDirectory = directory;
  outputFolderName.textContent = 'Selected folder: ' + directory.name;
  changeOutputFolder.hidden = false;
}

changeOutputFolder.onclick = async () => {
  changeOutputFolder.disabled = true;
  try {
    await chooseOutputDirectory();
    saveAllStatus.textContent = '';
  } catch (error) {
    if (error.name !== 'AbortError') {
      saveAllStatus.textContent = error.message;
    }
  } finally {
    updateControls();
  }
};

saveAll.onclick = async () => {
  const files = getJsonTraces().map(([name, value]) => [name, JSON.stringify(value, null, 2) + '\n']);
  const log = downloadLogText();
  if (log) {
    files.unshift(['AscodExtension.log', log]);
  }
  if (!files.length || savingAll) {
    return;
  }
  savingAll = true;
  updateControls();
  saveAllStatus.textContent = '';
  let saved = 0;
  try {
    if (!outputDirectory) {
      await chooseOutputDirectory();
    }
    for (const [name, text] of files) {
      await writeFolderFile(outputDirectory, name, text);
      saved++;
    }
    saveAllStatus.textContent = 'Saved ' + saved + ' files in ' + outputDirectory.name + '.';
  } catch (error) {
    if (error.name !== 'AbortError' || saved) {
      saveAllStatus.textContent = 'Saved ' + saved + ' of ' + files.length + '. ' + error.message;
    }
  } finally {
    savingAll = false;
    updateControls();
  }
};

saveTraces.addEventListener('click', async () => {
  if (typeof window.showDirectoryPicker !== 'function') {
    traceFileStatus.textContent = 'Edge cannot open a folder picker here.';
    return;
  }
  const files = getJsonTraces();
  if (!files.length) {
    return;
  }

  const path = traceFolder.value.trim();
  savingTraces = true;
  traceFolder.disabled = true;
  updateControls();
  const saved = [];
  try {
    if (!traceDirectory || confirmedTraceFolderHint !== path) {
      traceDirectory = await window.showDirectoryPicker({
        id: 'ascod-trace-folder',
        mode: 'readwrite'
      });
      confirmedTraceFolderHint = path;
    }
    await chrome.storage.local.set({
      preferredTraceFolder: path
    });
    for (const [filename, value] of files) {
      await writeFolderFile(traceDirectory, filename, JSON.stringify(value, null, 2) + '\n');
      saved.push(filename);
    }
    traceFileStatus.textContent = 'Saved in ' + traceDirectory.name + ': ' + saved.join(', ');
  } catch (error) {
    if (error.name !== 'AbortError' || saved.length) {
      traceFileStatus.textContent = 'Saved ' + saved.length + ' of ' + files.length + '. ' + error.message;
    }
  } finally {
    savingTraces = false;
    traceFolder.disabled = false;
    updateControls();
  }
});

copyLog.addEventListener('click', () => {
  const text = downloadLogText();
  if (!text) {
    logFileStatus.textContent = 'The download log is empty.';
    return;
  }
  return saveAndCopyLink(text, 'AscodExtension.log', 'text/plain;charset=utf-8', logFileStatus);
});

function readServerAddress() {
  const address = serverOrigin.value.trim();
  const url = URL.canParse(address) ? new URL(address) : null;
  if (!url || !['http:', 'https:'].includes(url.protocol) || url.username || url.password) {
    throw new Error('Enter an HTTP or HTTPS server address.');
  }
  return url;
}

function requestServerPermission(url) {
  return chrome.permissions.request({
    origins: [url.protocol + '//' + url.hostname + '/*']
  });
}

filterPayloadForm.addEventListener('submit', async event => {
  event.preventDefault();
  captureFilterPayload.disabled = true;
  try {
    const url = readServerAddress();
    const tab = await getRegistryTab(url);
    const result = await chrome.runtime.sendMessage({
      command: 'captureFilter',
      origin: url.origin,
      date: filterPayloadDate.value,
      tabId: tab.id
    });
    if (!result?.ok) {
      throw new Error(result?.error ?? 'Could not arm the filter capture.');
    }
  } catch (error) {
    filterPayloadStatus.textContent = error.message;
  } finally {
    captureFilterPayload.disabled = false;
  }
});

clearFilterPayloads.onclick = async () => {
  try {
    const result = await chrome.runtime.sendMessage({
      command: 'clearFilters'
    });
    if (!result?.ok) {
      throw new Error(result?.error ?? 'Could not clear the filter samples.');
    }
  } catch (error) {
    filterPayloadStatus.textContent = error.message;
  }
};

async function getRegistryTab(url) {
  if (!await requestServerPermission(url)) {
    throw new Error('Site permission was not granted.');
  }
  const [tab] = await chrome.tabs.query({
    active: true,
    currentWindow: true
  });
  if (tab?.id === undefined || !tab.url || new URL(tab.url).origin !== url.origin) {
    throw new Error('Open the registry on the selected server in the active tab.');
  }
  return tab;
}

async function waitForDownloads(count) {
  const deadline = Date.now() + 10000;
  let observed = 0;
  while (Date.now() < deadline) {
    const saved = await chrome.storage.session.get('monitor');
    observed = saved.monitor?.downloadBatch?.observed ?? 0;
    if (observed >= count) {
      break;
    }
    await new Promise(resolve => setTimeout(resolve, 250));
  }
  return observed;
}

downloadForm.addEventListener('submit', async event => {
  event.preventDefault();
  const count = downloadCount.valueAsNumber;
  downloadFirst.disabled = true;
  downloadCount.disabled = true;
  applyDate.disabled = true;
  downloadResult.textContent = 'Opening documents and requesting ZIP downloads…';
  let trace = [];
  let requested = 0;
  let prepared = false;
  try {
    const url = readServerAddress();
    const tab = await getRegistryTab(url);
    const configuration = await chrome.runtime.sendMessage({
      command: 'prepareDownload',
      origin: url.origin,
      count
    });
    if (!configuration?.ok) {
      throw new Error(configuration?.error ?? 'Could not prepare the download.');
    }
    prepared = true;
    const [execution] = await chrome.scripting.executeScript({
      target: {
        tabId: tab.id
      },
      world: 'MAIN',
      func: downloadRegistryCards,
      args: [url.origin, count]
    });
    const result = execution?.result;
    if (!result) {
      throw new Error('Could not start the download.');
    }
    requested = result.requested ?? 0;
    trace = result.trace ?? [];
    const observed = requested > 0 ? await waitForDownloads(requested) : 0;
    downloadResult.textContent = 'ZIP commands sent: ' + requested + ' of ' + count +
      '. Downloads detected by Edge: ' + observed + '.';
    if (result.error) {
      downloadResult.textContent += ' ' + result.error;
    } else if (observed > 0) {
      downloadResult.textContent += ' Check Edge Downloads or its save dialogs.';
    } else {
      downloadResult.textContent += ' No download event was received.';
    }
  } catch (error) {
    downloadResult.textContent = error.message;
  } finally {
    chrome.runtime.sendMessage({
      command: 'downloadResult',
      text: downloadResult.textContent,
      trace,
      requested: prepared ? requested : undefined
    }).catch(error => {
      console.error('Ascod API:', error);
    });
    downloadFirst.disabled = false;
    downloadCount.disabled = false;
    applyDate.disabled = false;
  }
});

function applyRegistrationDate(origin, dateValue) {
  if (location.origin !== origin) {
    return 'Open the registry on the selected server.';
  }
  if (location.pathname === '/' || location.pathname === '/page1') {
    const from = document.getElementById('from');
    const to = document.getElementById('to');
    const button = document.getElementById('apply-dates');
    if (!from || !to || !button || !button.getClientRects().length) {
      return 'Open the document registry first.';
    }
    from.value = dateValue;
    to.value = dateValue;
    button.click();
    return '';
  }
  if (!/\/Defaultv4\.aspx$/i.test(location.pathname)) {
    return 'Open the document registry first.';
  }

  const fromInput = document.querySelector('input[id*="_AskodGridCards_CommonGrid_"][id$="_regDateFrom_dateInput"]');
  if (!fromInput?.getClientRects().length) {
    return 'The registration date filter is not visible.';
  }
  const prefix = fromInput.id.slice(0, -'regDateFrom_dateInput'.length);
  const toInput = document.getElementById(prefix + 'regDateTill_dateInput');
  const button = document.getElementById(prefix + 'img1');
  const fromPicker = window.$find?.(prefix + 'regDateFrom');
  const toPicker = window.$find?.(prefix + 'regDateTill');
  if (!toInput?.getClientRects().length || !button?.getClientRects().length ||
      typeof fromPicker?.set_selectedDate !== 'function' || typeof toPicker?.set_selectedDate !== 'function') {
    return 'Could not find the registration date controls.';
  }

  const [year, month, day] = dateValue.split('-').map(Number);
  fromPicker.set_selectedDate(new Date(year, month - 1, day));
  toPicker.set_selectedDate(new Date(year, month - 1, day));
  button.click();
  return '';
}

dateFilter.addEventListener('submit', async event => {
  event.preventDefault();
  applyDate.disabled = true;
  downloadFirst.disabled = true;
  dateResult.textContent = '';
  try {
    const url = readServerAddress();
    const dateValue = registrationDate.value;
    const tab = await getRegistryTab(url);
    const [execution] = await chrome.scripting.executeScript({
      target: {
        tabId: tab.id
      },
      world: 'MAIN',
      func: applyRegistrationDate,
      args: [url.origin, dateValue]
    });
    if (!execution || execution.result) {
      throw new Error(execution?.result || 'Could not run the date filter.');
    }
    dateResult.textContent = 'Date filter submitted: ' + dateValue;
  } catch (error) {
    dateResult.textContent = error.message;
  } finally {
    applyDate.disabled = false;
    downloadFirst.disabled = false;
  }
});

function updateContextIndicator() {
  const address = serverOrigin.value.trim();
  const origin = URL.canParse(address) ? new URL(address).origin : null;
  const ready = origin !== null && monitor.directContextOrigin === origin;
  contextIndicator.dataset.ready = String(ready);
  contextIndicator.title = ready ? 'userSettings ready for direct requests' : 'userSettings not captured';
  contextStatus.textContent = ready ? 'ready for direct requests' : 'not captured';
}

serverOrigin.addEventListener('input', updateContextIndicator);
serverOrigin.addEventListener('change', () => {
  try {
    const url = readServerAddress();
    chrome.storage.local.set({ serverOrigin: url.origin }).then(startRecordingOnSelectedSite).catch(error => {
      console.error('Ascod API:', error);
    });
  } catch {
    updateContextIndicator();
  }
});

function updateWorkflowEvidence() {
  const entries = attachmentCapture?.responses ?? attachmentTrace?.timeline ?? [];
  const hits = entries.flatMap(entry => entry.hits ?? []);
  const sources = hits.flatMap(hit => hit.parameterSources ?? []);
  const tSources = sources.filter(source => source.parameter === 't');
  const commandIndex = entries.findIndex(entry => entry.kind === 'download command');
  const launch = commandIndex >= 0 && entries.slice(commandIndex + 1).some(entry =>
    entry.kind === 'download launch' && entry.functions?.length &&
    ['t', 'l', 'counter'].every(name => entry.parameters?.includes(name)));
  const hasCallerCandidate = tSources.some(source => source.origin?.callerCandidates?.length) ||
    entries.some(entry => entry.kind === 'click listener');
  const states = {
    zip: attachmentTrace?.zipDetected ||
      (attachmentCapture && monitor.zipCapture?.workflowId === attachmentCapture.id && monitor.zipCapture.archive) ?
      'observed' : 'missing',
    caller: launch ? 'observed' : hasCallerCandidate ? 'candidate' : 'missing',
    argument: tSources.some(source => source.origin?.callerInputs?.some(input => input !== 'unknown') ||
      (source.origin?.input && source.origin.input !== 'unknown')) ? 'candidate' : 'missing',
    counter: sources.some(source => source.parameter === 'counter' &&
      ['local assignment', 'assignment candidate'].includes(source.origin?.kind)) ? 'candidate' : 'missing'
  };
  for (const item of workflowEvidence) {
    const state = states[item.dataset.evidence];
    item.dataset.state = state;
    item.title = { observed: 'Observed in this run', candidate: 'Candidate from script analysis', missing: 'Not found' }[state];
  }
}

function updateControls() {
  updateContextIndicator();
  updateWorkflowEvidence();
  toggle.disabled = busy;
  clear.disabled = busy;
  const hasTraces = monitor.entries.length > 0 || !!monitor.zipCapture?.archive ||
    !!monitor.filterCapture?.samples?.length || !!attachmentTrace;
  saveAll.disabled = busy || savingAll || (!hasTraces && !monitor.downloadLog?.length);
  changeOutputFolder.disabled = savingAll;
  exportButton.disabled = busy || savingExport || savingTraces || !hasTraces;
  reloadButton.disabled = busy;
  captureZip.disabled = busy || attachmentBusy || !!attachmentCapture;
  captureAttachment.disabled = busy || attachmentBusy;
  captureAttachment.textContent = attachmentCapture ? 'Stop workflow recording' : 'Record download workflow';
  exportZip.disabled = savingExport || !monitor.zipCapture?.archive;
  copyLog.disabled = savingExport;
  saveTraces.disabled = busy || savingTraces || savingExport || !hasTraces;
  const zipCapture = monitor.zipCapture?.workflowId ? undefined : monitor.zipCapture;
  const capturing = zipCapture?.active && zipCapture.deadline > Date.now();
  captureZip.textContent = capturing ? 'Cancel ZIP capture' : 'Record one manual ZIP';
  if (zipCapture?.archive) {
    captureStatus.textContent = 'ZIP request captured. Save the trace to view masked parameters.';
  } else if (capturing) {
    captureStatus.textContent = 'Use the site’s Download all files command within two minutes.';
  } else if (zipCapture) {
    captureStatus.textContent = 'Capture expired. Start a new capture to try again.';
  } else {
    captureStatus.textContent = '';
  }
  serverOrigin.disabled = busy || monitor.recording;
  toggle.textContent = monitor.recording ? 'Stop recording' : 'Start recording';
  status.textContent = (monitor.recording ? 'Recording' : 'Stopped') + ' \u00b7 ' + monitor.entries.length;
}

function renderEndpoints(entries) {
  const endpoints = new Map();
  for (const entry of entries) {
    const origin = entry.origin ?? 'http://127.0.0.1:8091';
    const key = entry.method + ' ' + origin + entry.path;
    const previous = endpoints.get(key);
    endpoints.set(key, {
      method: entry.method,
      path: origin === monitor.origin ? entry.path : origin + entry.path,
      calls: (previous?.calls ?? 0) + 1,
      result: entry.error ?? entry.status,
      contentType: entry.contentType
    });
  }

  const fragment = document.createDocumentFragment();
  for (const endpoint of endpoints.values()) {
    const row = document.createElement('tr');
    const cells = [
      endpoint.method,
      endpoint.path,
      endpoint.calls,
      endpoint.result,
      endpoint.contentType ?? ''
    ];
    for (const text of cells) {
      row.insertCell().textContent = text;
    }
    fragment.append(row);
  }
  endpointRows.replaceChildren(fragment);
  endpointSummary.textContent = endpoints.size
    ? 'Unique endpoints in the current log: ' + endpoints.size
    : 'No endpoints recorded yet.';
}

function render(value) {
  monitor = value ?? {
    recording: false,
    entries: []
  };
  if (attachmentCapture && monitor.zipCapture?.workflowId === attachmentCapture.id && monitor.zipCapture.archive) {
    void finishAttachmentCapture('ZIP detected. Save the workflow JSON.');
  }
  const fragment = document.createDocumentFragment();
  for (const entry of monitor.entries) {
    const row = document.createElement('tr');
    const cells = [
      entry.time,
      entry.tabId === -1 ? '' : entry.tabId,
      entry.method,
      entry.path,
      entry.error ?? entry.status
    ];
    for (const text of cells) {
      row.insertCell().textContent = text;
    }
    const details = document.createElement('details');
    const summary = document.createElement('summary');
    summary.textContent = 'Details';
    details.append(summary);
    const fields = [
      ['Resource', entry.resourceType],
      ['Origin', entry.origin],
      ['Content-Type', entry.contentType],
      ['Content-Length', entry.contentLength],
      ['From cache', entry.fromCache],
      ['Initiator', entry.initiator],
      ['Frame', entry.frameId],
      ['Duration (ms)', entry.durationMs?.toFixed(1)],
      ['Query keys', entry.queryKeys?.join(', ')],
      ['Direct and manual ZIP counter match', entry.matchesDirectZipCounter],
      ['Request headers', entry.requestHeaders],
      ['Request body summary', entry.requestBody],
      ['Response headers', entry.responseHeaders],
      ['Redirects', entry.redirects]
    ];
    for (const [label, value] of fields) {
      if (value !== undefined) {
        const field = document.createElement('div');
        const text = typeof value === 'object' ? JSON.stringify(value, null, 2) : value;
        field.textContent = label + ': ' + text;
        details.append(field);
      }
    }
    row.insertCell().append(details);
    fragment.append(row);
  }
  rows.replaceChildren(fragment);
  filterPayloadStatus.textContent = monitor.filterCapture?.status ?? '';
  const log = document.createDocumentFragment();
  for (const entry of monitor.downloadLog ?? []) {
    const line = document.createElement('li');
    line.textContent = new Date(entry.time).toLocaleTimeString() + ' · ' + entry.message;
    log.append(line);
  }
  downloadLog.replaceChildren(log);
  renderEndpoints(monitor.entries);
  updateControls();
}

async function sendCommand(command) {
  busy = true;
  updateControls();
  let errorMessage;
  try {
    let origin;
    if (command === 'start') {
      const url = readServerAddress();
      origin = url.origin;
      const granted = await requestServerPermission(url);
      if (!granted) {
        throw new Error('Site permission was not granted.');
      }
      serverOrigin.value = origin;
    }
    const result = await chrome.runtime.sendMessage({
      command,
      origin
    });
    if (!result?.ok) {
      throw new Error(result?.error ?? 'Monitor did not respond.');
    }
  } catch (error) {
    errorMessage = error.message;
  } finally {
    busy = false;
    updateControls();
    if (errorMessage) {
      status.textContent = errorMessage;
    }
  }
}

toggle.onclick = () => {
  return sendCommand(monitor.recording ? 'stop' : 'start');
};
clear.onclick = () => {
  return sendCommand('clear');
};
reloadButton.onclick = () => {
  const hasSession = monitor.recording || monitor.entries.length > 0;
  if (hasSession && !window.confirm('Reloading stops recording and clears the journal. Continue?')) {
    return;
  }
  chrome.runtime.reload();
};
captureZip.onclick = async () => {
  busy = true;
  updateControls();
  let errorMessage;
  try {
    let origin;
    const active = monitor.zipCapture?.active && monitor.zipCapture.deadline > Date.now();
    const command = active ? 'cancelZip' : 'captureZip';
    if (command === 'captureZip') {
      const url = readServerAddress();
      if (!await requestServerPermission(url)) {
        throw new Error('Site permission was not granted.');
      }
      origin = url.origin;
    }
    const result = await chrome.runtime.sendMessage({
      command,
      origin
    });
    if (!result?.ok) {
      throw new Error(result?.error ?? 'Could not start ZIP capture.');
    }
  } catch (error) {
    errorMessage = error.message;
  } finally {
    busy = false;
    updateControls();
    if (errorMessage) {
      captureStatus.textContent = errorMessage;
    }
  }
};
async function unregisterAttachmentScripts() {
  const scripts = await chrome.scripting.getRegisteredContentScripts({ ids: attachmentScriptIds });
  if (scripts.length > 0) {
    await chrome.scripting.unregisterContentScripts({ ids: scripts.map(script => script.id) });
  }
}

async function finishAttachmentCapture(message) {
  const capture = attachmentCapture;
  clearTimeout(capture.timer);
  attachmentCapture = undefined;
  attachmentBusy = true;
  attachmentStatus.textContent = message;
  updateControls();
  try {
    const saved = await chrome.storage.session.get('monitor');
    const network = saved.monitor?.zipCapture;
    const matched = network?.workflowId === capture.id;
    attachmentTrace = {
      startedAt: capture.startedAt,
      zipDetected: matched && !!network.archive,
      timeline: [
        ...capture.responses,
        ...(matched ? network.preceding : []),
        ...(matched && network.archive ? [{ ...network.archive, zip: true }] : [])
      ].sort((left, right) => left.time.localeCompare(right.time))
    };
    if (matched && network.active) {
      await chrome.runtime.sendMessage({ command: 'cancelZip', workflowId: capture.id });
    }
  } catch (error) {
    attachmentStatus.textContent += ' ' + error.message;
  }
  try {
    await unregisterAttachmentScripts();
  } catch (error) {
    attachmentStatus.textContent += ' ' + error.message;
  } finally {
    attachmentBusy = false;
    updateControls();
  }
}

captureAttachment.onclick = async () => {
  if (attachmentCapture) {
    await finishAttachmentCapture('Recording stopped. Save the partial workflow JSON.');
    return;
  }
  attachmentBusy = true;
  updateControls();
  try {
    const url = readServerAddress();
    if (!await requestServerPermission(url)) {
      throw new Error('Site permission was not granted.');
    }
    await unregisterAttachmentScripts();
    const matches = [url.protocol + '//' + url.hostname + '/*'];
    await chrome.scripting.registerContentScripts(attachmentScriptIds.map((id, index) => ({
      id,
      matches,
      js: index === 0 ? ['acorn.js', id + '.js'] : [id + '.js'],
      allFrames: true,
      runAt: 'document_start',
      world: index === 0 ? 'MAIN' : 'ISOLATED',
      persistAcrossSessions: false
    })));
    const id = crypto.randomUUID();
    const result = await chrome.runtime.sendMessage({ command: 'captureZip', origin: url.origin, workflowId: id });
    if (!result?.ok) {
      throw new Error(result?.error ?? 'Could not start workflow recording.');
    }
    attachmentTrace = undefined;
    attachmentCapture = {
      id,
      origin: url.origin,
      startedAt: new Date().toISOString(),
      responses: [],
      timer: setTimeout(() => {
        void finishAttachmentCapture('Recording expired. Save the partial workflow JSON.');
      }, 120000)
    };
    attachmentStatus.textContent = 'Refresh the registry, open a card, and use Download all files within two minutes.';
  } catch (error) {
    attachmentStatus.textContent = error.message;
    await unregisterAttachmentScripts().catch(console.error);
  } finally {
    attachmentBusy = false;
    updateControls();
  }
};

chrome.runtime.onMessage.addListener((message, sender) => {
  if (message?.command !== 'attachmentResponseSchema' || !attachmentCapture ||
      sender.id !== chrome.runtime.id || sender.origin !== attachmentCapture.origin ||
      !sender.tab || !message.schema) {
    return;
  }
  const scriptCandidate = message.schema.kind === 'script candidates';
  const matchingCount = attachmentCapture.responses.filter(entry =>
    (entry.kind === 'script candidates') === scriptCandidate).length;
  if (matchingCount < 30) {
    attachmentCapture.responses.push({
      time: new Date().toISOString(),
      source: scriptCandidate ? 'script inspection' :
        message.schema.kind === 'card comparisons' ? 'field comparison' :
          message.schema.kind ? 'download action' : 'response inspection',
      ...message.schema
    });
    updateWorkflowEvidence();
  }
});

window.addEventListener('pagehide', () => {
  if (attachmentCapture) {
    void unregisterAttachmentScripts().catch(console.error);
  }
});

exportZip.onclick = () => {
  const trace = getZipTrace();
  if (trace) {
    return saveJsonAndCopyLinks([['ascod-zip-trace.json', trace]]);
  }
};
exportButton.onclick = () => {
  return saveJsonAndCopyLinks(getJsonTraces());
};

function saveJsonAndCopyLinks(traces) {
  const files = traces.map(([filename, value]) => ({
    filename,
    text: JSON.stringify(value, null, 2) + '\n',
    type: 'application/json'
  }));
  return saveFilesAndCopyLinks(files, traceFileStatus);
}

function saveAndCopyLink(text, filename, type, feedback) {
  return saveFilesAndCopyLinks([{ text, filename, type }], feedback);
}

async function saveFilesAndCopyLinks(files, feedback) {
  if (savingExport || !files.length) {
    return;
  }
  savingExport = true;
  updateControls();
  const links = [];
  let saveError;
  try {
    for (const { text, filename, type } of files) {
      feedback.textContent = 'Choose where to save ' + filename + '.';
      try {
        links.push(await saveTextAndGetFileUrl(text, filename, type));
      } catch (error) {
        saveError = error.message;
        break;
      }
    }
    if (!links.length) {
      feedback.textContent = 'Could not save: ' + saveError;
      return;
    }
    try {
      await navigator.clipboard.writeText(links.join('\n'));
      feedback.textContent = 'Saved and copied ' + links.length + ' of ' + files.length + ' file links.';
    } catch {
      feedback.textContent = 'Saved, but could not copy: ' + links.join(' ');
    }
    if (saveError) {
      feedback.textContent += ' ' + saveError;
    }
  } finally {
    savingExport = false;
    updateControls();
  }
}

let receivedUpdate = false;
chrome.storage.onChanged.addListener((changes, area) => {
  if (area !== 'session' || !changes.monitor) {
    return;
  }
  receivedUpdate = true;
  render(changes.monitor.newValue);
});

async function startRecordingOnSelectedSite() {
  if (busy || monitor.recording || monitor.paused) {
    return;
  }
  try {
    const url = readServerAddress();
    const [tab] = await chrome.tabs.query({
      active: true,
      currentWindow: true
    });
    if (!URL.canParse(tab?.url) || new URL(tab.url).origin !== url.origin) {
      return;
    }
    const result = await chrome.runtime.sendMessage({
      command: 'autoStart',
      origin: url.origin
    });
    if (!result?.ok) {
      throw new Error(result?.error ?? 'Monitor did not respond.');
    }
  } catch (error) {
    status.textContent = error.message;
  }
}

chrome.tabs.onActivated.addListener(startRecordingOnSelectedSite);
chrome.tabs.onUpdated.addListener((tabId, change, tab) => {
  if (tab.active && change.status === 'loading') {
    void startRecordingOnSelectedSite();
  }
});
chrome.permissions.onAdded.addListener(startRecordingOnSelectedSite);

Promise.all([
  chrome.storage.session.get('monitor'),
  chrome.storage.local.get(['serverOrigin', 'preferredLogFolder', 'preferredTraceFolder', 'operationMode'])
]).then(([saved, settings]) => {
  showOperationMode(settings.operationMode);
  serverOrigin.value = settings.serverOrigin ?? saved.monitor?.origin ?? serverOrigin.value;
  logFolder.value = settings.preferredLogFolder ?? logFolder.value;
  traceFolder.value = settings.preferredTraceFolder ?? traceFolder.value;
  busy = false;
  if (receivedUpdate) {
    updateControls();
  } else {
    render(saved.monitor);
  }
  return startRecordingOnSelectedSite();
}).catch(error => {
  status.textContent = error.message;
});

setInterval(() => {
  if (monitor.zipCapture?.active && monitor.zipCapture.deadline <= Date.now() &&
      captureZip.textContent === 'Cancel ZIP capture') {
    updateControls();
  }
}, 1000);