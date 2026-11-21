const rows = document.getElementById('requests');
const endpointRows = document.getElementById('endpoints');
const endpointSummary = document.getElementById('endpoint-summary');
const toggle = document.getElementById('toggle');
const clear = document.getElementById('clear');
const exportButton = document.getElementById('export');
const reloadButton = document.getElementById('reload');
const status = document.getElementById('status');
const serverOrigin = document.getElementById('server-origin');
let monitor = {
  recording: false,
  entries: []
};
let busy = true;

function updateControls() {
  toggle.disabled = busy;
  clear.disabled = busy;
  exportButton.disabled = busy || monitor.entries.length === 0;
  reloadButton.disabled = busy;
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
      const address = serverOrigin.value.trim();
      const url = URL.canParse(address) ? new URL(address) : null;
      if (!url || !['http:', 'https:'].includes(url.protocol) || url.username || url.password) {
        throw new Error('Enter an HTTP or HTTPS server address.');
      }
      origin = url.origin;
      const granted = await chrome.permissions.request({
        origins: [url.protocol + '//' + url.hostname + '/*']
      });
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
exportButton.onclick = () => {
  const data = new Blob([JSON.stringify(monitor.entries, null, 2)], { type: 'application/json' });
  const url = URL.createObjectURL(data);
  const link = document.createElement('a');
  link.href = url;
  link.download = 'ascod-requests.json';
  link.click();
  setTimeout(() => {
    URL.revokeObjectURL(url);
  }, 1000);
};

let receivedUpdate = false;
chrome.storage.onChanged.addListener((changes, area) => {
  if (area !== 'session' || !changes.monitor) {
    return;
  }
  receivedUpdate = true;
  render(changes.monitor.newValue);
});

Promise.all([
  chrome.storage.session.get('monitor'),
  chrome.storage.local.get('serverOrigin')
]).then(([saved, settings]) => {
  serverOrigin.value = monitor.origin ?? saved.monitor?.origin ?? settings.serverOrigin ?? serverOrigin.value;
  busy = false;
  if (receivedUpdate) {
    updateControls();
  } else {
    render(saved.monitor);
  }
}).catch(error => {
  status.textContent = error.message;
});