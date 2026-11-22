const attachmentSchemaTypes = new Set(['array', 'object', 'string', 'number', 'boolean', 'null', 'redacted', 'truncated']);
const parameterSourceTypes = new Set(['literal', 'identifier', 'object property', 'function call', 'unknown']);

function readParameterOrigin(origin) {
  if (['local assignment', 'assignment candidate'].includes(origin?.kind) && parameterSourceTypes.has(origin.input)) {
    return {
      kind: origin.kind,
      input: origin.input,
      offset: Number.isInteger(origin.offset) && origin.offset >= 0 && origin.offset < 1024 * 1024 ?
        origin.offset : undefined
    };
  }
  if (origin?.kind === 'function argument' && Number.isInteger(origin.position) &&
      origin.position > 0 && origin.position <= 128) {
    return {
      kind: origin.kind,
      position: origin.position,
      callerInputs: Array.isArray(origin.callerInputs) ?
        origin.callerInputs.filter(input => parameterSourceTypes.has(input)).slice(0, 5) : undefined,
      callerCandidates: Array.isArray(origin.callerCandidates) ? origin.callerCandidates.slice(0, 12)
        .filter(candidate => Number.isInteger(candidate.offset) && candidate.offset >= 0 &&
          candidate.offset < 1024 * 1024 && parameterSourceTypes.has(candidate.input))
        .map(candidate => ({ offset: candidate.offset, input: candidate.input })) : undefined
    };
  }
  return undefined;
}

function readAttachmentSchema(node, depth = 0) {
  if (depth > 10 || !attachmentSchemaTypes.has(node?.type)) {
    throw new Error('Invalid attachment schema.');
  }
  if (node.type === 'object') {
    return {
      type: node.type,
      fields: node.fields.slice(0, 80).map(field => ({
        name: /^[A-Za-z_$][A-Za-z0-9_$]{0,47}$/.test(field.name) &&
          !/\d{5,}|[A-Za-z0-9]{30,}/.test(field.name) ? field.name : '[dynamic key]',
        schema: readAttachmentSchema(field.schema, depth + 1)
      }))
    };
  }
  if (node.type === 'array') {
    return {
      type: node.type,
      samples: node.samples.slice(0, 3).map(sample => readAttachmentSchema(sample, depth + 1))
    };
  }
  return { type: node.type };
}

function readEmbeddedResponse(embedded) {
  if (embedded?.format === 'json') {
    return { format: 'json', schema: readAttachmentSchema(embedded.schema) };
  }
  if (embedded?.format === 'html' || embedded?.format === 'link') {
    return {
      format: embedded.format,
      links: embedded.links.slice(0, 12).filter(link =>
        link.path === '/askod-ssl/Handlers/FileHandler.ashx').map(link => ({
        path: link.path,
        parameters: link.parameters.filter(key =>
          typeof key === 'string' && /^[A-Za-z][A-Za-z0-9_-]{0,23}$/.test(key)).slice(0, 32)
      }))
    };
  }
  return embedded?.format === 'unknown' ? { format: 'unknown' } : undefined;
}

window.addEventListener('ascod-attachment-response-schema', event => {
  if (typeof event.detail !== 'string' || event.detail.length > 100000) {
    return;
  }
  try {
    const result = JSON.parse(event.detail);
    if (result.kind === 'card comparisons') {
      const comparisons = {};
      for (const field of [
        'getDataCounterMatchesCheckAccess',
        'getDataTokenMatchesCheckAccess',
        'checkAccessTokenMatchesFillEcd',
        'cardUrlTokenMatchesGetData',
        'cardUrlTokenMatchesCheckAccessResponse'
      ]) {
        if (typeof result[field] === 'boolean' || result[field] === null) {
          comparisons[field] = result[field];
        }
      }
      if (Object.keys(comparisons).length) {
        void chrome.runtime.sendMessage({
          command: 'attachmentResponseSchema',
          schema: { kind: result.kind, ...comparisons }
        }).catch(() => {});
      }
      return;
    }
    if (result.kind === 'click listener') {
      const name = result.functionName;
      const functionName = typeof name === 'string' && /^[A-Za-z_$][\w$]{0,47}$/.test(name) &&
        !/\d{5,}|[A-Za-z0-9]{30,}/.test(name) ? name : undefined;
      void chrome.runtime.sendMessage({
        command: 'attachmentResponseSchema',
        schema: { kind: result.kind, functionName }
      }).catch(() => {});
      return;
    }
    if (result.kind === 'script candidates') {
      if (!Number.isInteger(result.scriptIndex) || result.scriptIndex < 0 || result.scriptIndex >= 100 ||
          !['inline', 'external'].includes(result.scriptType) ||
          !['/askod-ssl/windoweditcard.aspx', '/askod-ssl/pdfviewerframe.aspx'].includes(result.framePath)) {
        return;
      }
      const hits = result.hits.slice(0, 8).filter(hit =>
        ['FileHandler.ashx', 'download all label'].includes(hit.marker) &&
        Number.isInteger(hit.offset) && hit.offset >= 0 && hit.offset < 1024 * 1024
      ).map(hit => ({
        marker: hit.marker,
        offset: hit.offset,
        parameters: ['t', 'l', 'counter'].filter(name => hit.parameters.includes(name)),
        parameterSources: (hit.parameterSources ?? []).slice(0, 3).filter(item =>
          ['t', 'l', 'counter'].includes(item.parameter) && parameterSourceTypes.has(item.source)
        ).map(item => ({
          parameter: item.parameter,
          source: item.source,
          origin: readParameterOrigin(item.origin)
        }))
      }));
      void chrome.runtime.sendMessage({
        command: 'attachmentResponseSchema',
        schema: {
          kind: result.kind,
          framePath: result.framePath,
          scriptIndex: result.scriptIndex,
          scriptType: result.scriptType,
          hits
        }
      }).catch(() => {});
      return;
    }
    if (result.kind === 'download command' || result.kind === 'download launch') {
      const entry = { kind: result.kind };
      const isFunctionName = name => typeof name === 'string' &&
        /^[A-Za-z_$][\w$.]{0,63}$/.test(name) && !/\d{5,}|[A-Za-z0-9]{30,}/.test(name);
      if (result.kind === 'download command') {
        entry.handlers = (result.handlers ?? []).slice(0, 12).filter(handler =>
          ['onclick', 'javascript href', 'onclick property'].includes(handler.kind)).map(handler => ({
          kind: handler.kind,
          functionName: isFunctionName(handler.functionName) ? handler.functionName : undefined,
          mentionsFileHandler: handler.mentionsFileHandler === true
        }));
      }
      if (result.kind === 'download launch') {
        if (result.path !== '/askod-ssl/Handlers/FileHandler.ashx' ||
            !['window.open', 'anchor.click', 'iframe.src', 'setAttribute', 'xhr', 'fetch'].includes(result.channel)) {
          return;
        }
        entry.path = result.path;
        entry.channel = result.channel;
        entry.parameters = ['t', 'l', 'counter'].filter(name => result.parameters.includes(name));
        entry.functions = result.functions.filter(isFunctionName).slice(0, 8);
      }
      void chrome.runtime.sendMessage({ command: 'attachmentResponseSchema', schema: entry }).catch(() => {});
      return;
    }
    const isViewer = result.path === '/askod-ssl/pdfviewerframe.aspx' &&
      ['GET', 'POST', 'NAVIGATION'].includes(result.method);
    const isService = result.method === 'POST' && [
      '/askod-ssl/services/pdfviewerservice.svc/fillecdtreeviewandselect',
      '/askod-ssl/services/cardsservicesub.svc/getdata',
      '/askod-ssl/services/cardsservice.svc/checkaccesstocard'
    ].includes(result.path);
    const goodStatus = Number.isInteger(result.status) && result.status >= 200 && result.status < 300;
    if ((!isViewer && !isService) || (!goodStatus && result.method !== 'NAVIGATION')) {
      return;
    }
    void chrome.runtime.sendMessage({
      command: 'attachmentResponseSchema',
      schema: {
        path: result.path,
        method: result.method,
        status: goodStatus ? result.status : undefined,
        schema: result.schema ? readAttachmentSchema(result.schema) : undefined,
        embedded: readEmbeddedResponse(result.embedded)
      }
    }).catch(() => {});
  } catch {
    // Отбрасываем события, которые не содержат схему.
  }
});