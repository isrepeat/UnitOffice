const captureForm = document.getElementById('capture-filter');
const dateInput = document.getElementById('filter-date');
const cancelButton = document.getElementById('cancel-filter');
const clearButton = document.getElementById('clear-filters');
const saveButton = document.getElementById('save-filters');
const status = document.getElementById('filter-status');
const sampleList = document.getElementById('filter-samples');
const samples = new Map();
let armed;
let saving = false;

function updateControls(message) {
  status.textContent = message;
  cancelButton.disabled = !armed;
  saveButton.disabled = saving || samples.size === 0;
  sampleList.replaceChildren();
  for (const sample of samples.values()) {
    const item = document.createElement('li');
    item.textContent = sample.date + ' · HTTP ' + sample.response.status + ' · ' + sample.response.format;
    sampleList.append(item);
  }
}

function responseShape(value, depth = 0) {
  if (value === null) {
    return 'null';
  }
  if (typeof value === 'string' && depth < 7 && value.length <= 1000000 && /^[\s]*[\[{]/.test(value)) {
    try {
      return {
        type: 'json-string',
        shape: responseShape(JSON.parse(value), depth + 1)
      };
    } catch {
      return 'string';
    }
  }
  if (depth >= 7 || typeof value !== 'object') {
    return Array.isArray(value) ? 'array' : typeof value;
  }
  if (Array.isArray(value)) {
    return {
      type: 'array',
      length: value.length,
      item: value.length ? responseShape(value[0], depth + 1) : null
    };
  }
  return Object.fromEntries(Object.entries(value).slice(0, 80).map(([key, child], index) =>
    [schemaKey(key, index), responseShape(child, depth + 1)]));
}

function readResponse(entry) {
  return new Promise(resolve => {
    entry.getContent((content, encoding) => {
      try {
        const text = encoding === 'base64'
          ? new TextDecoder().decode(Uint8Array.from(atob(content), byte => byte.charCodeAt(0)))
          : content;
        const json = JSON.parse(text);
        resolve({
          format: 'JSON',
          shape: responseShape(json)
        });
      } catch {
        resolve({
          format: 'JSON body unavailable',
          shape: null
        });
      }
    });
  });
}

chrome.devtools.network.onRequestFinished.addListener(async entry => {
  const capture = armed;
  if (!capture || capture.processing || entry.request.method !== 'POST') {
    return;
  }
  const url = new URL(entry.request.url);
  if (url.origin !== capture.origin || !/\/CardsServiceSub\.svc\/GetData$/i.test(url.pathname) ||
      entry.response.status < 200 || entry.response.status >= 300) {
    return;
  }
  const raw = entry.request.postData?.text;
  if (!raw || raw.length > 1000000) {
    updateControls('GetData was seen, but its JSON payload was unavailable.');
    return;
  }
  let payload;
  try {
    payload = JSON.parse(raw);
  } catch {
    updateControls('GetData was seen, but its payload was not JSON.');
    return;
  }
  if (!payload || !Object.hasOwn(payload, 'filterExpression')) {
    return;
  }
  capture.processing = true;
  const response = await readResponse(entry);
  if (armed !== capture) {
    return;
  }
  samples.set(capture.date, {
    date: capture.date,
    request: {
      method: entry.request.method,
      path: url.pathname,
      contentType: entry.request.postData.mimeType,
      payload: redactPayload(payload, capture.date)
    },
    response: {
      status: entry.response.status,
      ...response
    }
  });
  armed = undefined;
  updateControls('Captured ' + samples.size + ' of 2 dates.');
});

captureForm.addEventListener('submit', event => {
  event.preventDefault();
  const date = dateInput.value;
  if (!samples.has(date) && samples.size >= 2) {
    updateControls('Two dates are already captured. Clear the samples to start again.');
    return;
  }
  chrome.devtools.inspectedWindow.eval('location.origin', (origin, error) => {
    if (error || !/^https?:\/\//i.test(origin)) {
      updateControls('Open the Ascod registry in the inspected tab.');
      return;
    }
    armed = {
      date,
      origin,
      processing: false
    };
    updateControls('Recording the next GetData for ' + date + '. Apply the filter in the registry now.');
  });
});

cancelButton.onclick = () => {
  armed = undefined;
  updateControls('Capture cancelled.');
};

clearButton.onclick = () => {
  armed = undefined;
  samples.clear();
  updateControls('Samples cleared.');
};

saveButton.onclick = async () => {
  saving = true;
  updateControls('Choose where to save the sanitized trace.');
  const text = JSON.stringify({ samples: [...samples.values()] }, null, 2) + '\n';
  try {
    const fileUrl = await saveTextAndGetFileUrl(text, 'ascod-filter-trace.json', 'application/json');
    try {
      await navigator.clipboard.writeText(fileUrl);
      updateControls('Saved and copied: ' + fileUrl);
    } catch {
      updateControls('Saved, but could not copy the link: ' + fileUrl);
    }
  } catch (error) {
    updateControls('Could not save: ' + error.message);
  } finally {
    saving = false;
    saveButton.disabled = samples.size === 0;
  }
};