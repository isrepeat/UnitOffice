(() => {
  const endpoint = '/askod-ssl/services/pdfviewerservice.svc/fillecdtreeviewandselect';
  const viewerPath = '/askod-ssl/pdfviewerframe.aspx';
  const eventName = 'ascod-attachment-response-schema';
  const deadline = Date.now() + 120000;
  const maximumBody = 1024 * 1024;
  const fileHandler = '/askod-ssl/Handlers/FileHandler.ashx';
  const parseScript = window.acorn?.parse;
  let remaining = 500;

  function emit(result) {
    window.dispatchEvent(new CustomEvent(eventName, { detail: JSON.stringify(result) }));
  }

  const inspectedScripts = new Set();
  let inspectedExternal = 0;

  function classifyParameterExpression(expression) {
    const value = expression.trim();
    if (/^(?:['"\x60]|\d|true\b|false\b|null\b|undefined\b)/.test(value)) {
      return 'literal';
    }
    const reference = /^[A-Za-z_$][\w$]*(?:\s*(?:\?\.|\.)\s*[A-Za-z_$][\w$]*|\s*\[[^\]\r\n]{1,80}\])*/.exec(value);
    if (!reference) {
      return 'unknown';
    }
    const tail = value.slice(reference[0].length).trimStart();
    if (tail.startsWith('(')) {
      return 'function call';
    }
    return /[.\[]/.test(reference[0]) ? 'object property' : 'identifier';
  }

  function inspectIdentifierOrigin(source, offset, expression) {
    const reference = /^\s*([A-Za-z_$][\w$]*)\s*(?=$|[+},;)])/.exec(expression);
    if (!reference) {
      return undefined;
    }
    const quote = value => value.replace(/\$/g, '\\$');
    const before = source.slice(Math.max(0, offset - 2500), offset);
    const declaration = [...before.matchAll(/\bfunction\s*([A-Za-z_$][\w$]*)?\s*\(([^()]*)\)\s*\{/g)].at(-1);
    if (!declaration) {
      return undefined;
    }
    const body = before.slice(declaration.index + declaration[0].length);
    const bodyOffset = offset - body.length;
    const assignmentPattern = new RegExp(
      '(?:^|[;,]|\\b(?:var|let|const)\\s+)\\s*' + quote(reference[1]) +
      '\\s*=(?!=|>)\\s*([^;\\r\\n]{1,120})', 'g'
    );
    // Не связываем совпадения через границы блоков или вложенных функций.
    if (/[{}]/.test(body)) {
      const candidate = [...body.matchAll(assignmentPattern)].at(-1);
      return candidate ? {
        kind: 'assignment candidate',
        input: classifyParameterExpression(candidate[1]),
        offset: bodyOffset + candidate.index
      } : undefined;
    }
    const name = reference[1];
    const assignment = [...body.matchAll(assignmentPattern)].at(-1);
    if (assignment) {
      return {
        kind: 'local assignment',
        input: classifyParameterExpression(assignment[1]),
        offset: bodyOffset + assignment.index
      };
    }
    const position = declaration[2].split(',').map(item => item.trim()).indexOf(name);
    if (position < 0) {
      return undefined;
    }
    const origin = { kind: 'function argument', position: position + 1 };
    const prefix = before.slice(Math.max(0, declaration.index - 150), declaration.index);
    const member = /(?:^|[.,{])\s*([A-Za-z_$][\w$]*)\s*[:=]\s*$/.exec(prefix)?.[1];
    const callable = declaration[1] ?? member;
    if (callable) {
      const qualifier = declaration[1] ? '(?:^|[^\\w$.])' : '\\.\\s*';
      const calls = new RegExp(qualifier + quote(callable) + '\\s*\\(([^(){};]{0,220})\\)', 'g');
      const callerInputs = new Set();
      const callerCandidates = [];
      for (const call of source.matchAll(calls)) {
        if (/\bfunction\s*$/.test(source.slice(Math.max(0, call.index - 20), call.index + 1))) {
          continue;
        }
        const argument = call[1].split(',')[position];
        if (argument?.trim()) {
          const input = classifyParameterExpression(argument);
          callerInputs.add(input);
          callerCandidates.push({ offset: call.index, input });
          if (callerCandidates.length >= 12) {
            break;
          }
        }
      }
      origin.callerInputs = [...callerInputs];
      origin.callerCandidates = callerCandidates;
    }
    return origin;
  }

  function parseScriptStructure(source) {
    if (!parseScript) {
      return undefined;
    }
    let tree;
    try {
      tree = parseScript(source, { ecmaVersion: 'latest', sourceType: 'script' });
    } catch {
      try {
        tree = parseScript(source, { ecmaVersion: 'latest', sourceType: 'module' });
      } catch {
        return undefined;
      }
    }
    const functions = [];
    const writes = [];
    const calls = [];
    function visit(node, parent, scope) {
      if (/Function(?:Declaration|Expression)$/.test(node.type)) {
        const binding = node.id ?? parent?.id ?? parent?.key ?? parent?.left;
        const name = binding?.name ?? binding?.property?.name;
        const member = parent?.type === 'Property' || binding?.type === 'MemberExpression';
        scope = { node, name, member, outer: scope };
        functions.push(scope);
      }
      if (node.type === 'VariableDeclarator' && node.id.type === 'Identifier' && node.init) {
        writes.push({ name: node.id.name, value: node.init, offset: node.start, scope });
      }
      if (node.type === 'AssignmentExpression' && node.operator === '=' && node.left.type === 'Identifier') {
        writes.push({ name: node.left.name, value: node.right, offset: node.start, scope });
      }
      if (node.type === 'CallExpression') {
        calls.push(node);
      }
      for (const value of Object.values(node)) {
        for (const child of Array.isArray(value) ? value : [value]) {
          if (typeof child?.type === 'string') {
            visit(child, node, scope);
          }
        }
      }
    }
    visit(tree, undefined, undefined);
    return { functions, writes, calls };
  }

  function inspectStructuredOrigin(source, structure, offset, expression) {
    const name = /^\s*([A-Za-z_$][\w$]*)\s*(?=$|[+},;)])/.exec(expression)?.[1];
    if (!structure || !name) {
      return undefined;
    }
    const classifyNode = node => classifyParameterExpression(source.slice(node.start, node.end));
    let scope = structure.functions.filter(item => item.node.start <= offset && item.node.end > offset).at(-1);
    while (true) {
      const assignments = structure.writes.filter(item => item.scope === scope && item.name === name && item.offset < offset);
      if (assignments.length) {
        const candidate = assignments.at(-1);
        return { kind: 'assignment candidate', offset: candidate.offset, input: classifyNode(candidate.value) };
      }
      if (!scope) {
        return undefined;
      }
      const index = scope.node.params.findIndex(param => param.type === 'Identifier' && param.name === name);
      if (index >= 0) {
        const callerCandidates = scope.name ? structure.calls.filter(call => {
          const callee = call.callee;
          return call.arguments[index] && (scope.member ?
            callee.type === 'MemberExpression' && !callee.computed && callee.property.name === scope.name :
            callee.type === 'Identifier' && callee.name === scope.name);
        }).slice(0, 12).map(call => ({
          offset: call.start,
          input: classifyNode(call.arguments[index])
        })) : [];
        return {
          kind: 'function argument',
          position: index + 1,
          callerCandidates,
          callerInputs: [...new Set(callerCandidates.map(candidate => candidate.input))]
        };
      }
      scope = scope.outer;
    }
  }

  function inspectParameterSources(source, offset, structure) {
    const segment = source.slice(offset, offset + 700);
    const inspectOrigin = expression => inspectStructuredOrigin(source, structure, offset, expression) ??
      inspectIdentifierOrigin(source, offset, expression);
    return ['t', 'l', 'counter'].flatMap(parameter => {
      const query = new RegExp('[?&]' + parameter + '=\\s*', 'i').exec(segment);
      if (query) {
        const after = segment.slice(query.index + query[0].length);
        const expression = /^(?:['"]\s*\+\s*|\$\{)\s*([^\r\n]{1,120})/.exec(after);
        return [{
          parameter,
          source: expression ? classifyParameterExpression(expression[1]) :
            /^[^&'"$\x60\s]+/.test(after) ? 'literal' : 'unknown',
          origin: expression ? inspectOrigin(expression[1]) : undefined
        }];
      }
      const property = new RegExp('[{,]\\s*[\x27"]?' + parameter + '[\x27"]?\\s*:\\s*([^\\r\\n]{1,120})').exec(segment);
      return property ? [{
        parameter,
        source: classifyParameterExpression(property[1]),
        origin: inspectOrigin(property[1])
      }] : [];
    });
  }

  function inspectScript(source, scriptIndex, scriptType) {
    if (source.length > maximumBody || Date.now() > deadline) {
      return;
    }
    const hits = [];
    let structure;
    let attemptedStructure = false;
    const markers = /FileHandler\.ashx|Завантажити всі файли/gi;
    for (const match of source.matchAll(markers)) {
      const nearby = source.slice(Math.max(0, match.index - 300), match.index + 300);
      const parameters = ['t', 'l', 'counter'].filter(name =>
        new RegExp('(?:[?&]|\\b)' + name + '\\s*(?:=|:)').test(nearby));
      const isHandler = /^FileHandler/i.test(match[0]);
      if (isHandler && !attemptedStructure) {
        attemptedStructure = true;
        try {
          structure = parseScriptStructure(source);
        } catch {
          // При недоступном AST сохраняем результаты локального поиска.
        }
      }
      hits.push({
        marker: isHandler ? 'FileHandler.ashx' : 'download all label',
        offset: match.index,
        parameters,
        parameterSources: isHandler ? inspectParameterSources(source, match.index, structure) : undefined
      });
      if (hits.length === 8) {
        break;
      }
    }
    if (hits.length) {
      emit({ kind: 'script candidates', framePath: location.pathname.toLowerCase(), scriptIndex, scriptType, hits });
    }
  }

  async function inspectExternalScript(url, scriptIndex) {
    const response = await originalFetch.call(window, url.href, { cache: 'force-cache', redirect: 'error' });
    if (!response.ok || new URL(response.url).origin !== location.origin) {
      return;
    }
    const source = await readLimitedText(response);
    if (source !== undefined) {
      inspectScript(source, scriptIndex, 'external');
    }
  }

  function inspectPageScripts() {
    if (Date.now() > deadline ||
        !/^\/askod-ssl\/(?:WindowEditCard|PdfViewerFrame)\.aspx$/i.test(location.pathname)) {
      return;
    }
    Array.from(document.scripts).slice(0, 100).forEach((script, index) => {
      if (inspectedScripts.has(script)) {
        return;
      }
      inspectedScripts.add(script);
      if (!script.src) {
        inspectScript(script.textContent, index, 'inline');
        return;
      }
      const url = URL.parse(script.src);
      if (!url || url.origin !== location.origin || inspectedExternal >= 24 ||
          !/(?:\.js|\/(?:Telerik\.)?WebResource\.axd|\/ScriptResource\.axd)$/i.test(url.pathname)) {
        return;
      }
      inspectedExternal++;
      void inspectExternalScript(url, index).catch(() => {});
    });
  }

  document.addEventListener('DOMContentLoaded', inspectPageScripts, { once: true });
  window.addEventListener('load', inspectPageScripts, { once: true });

  // Фиксируем место передачи URL браузеру, сохраняя только имена функций и параметров.
  function traceDownloadLaunch(channel, address) {
    if (Date.now() > deadline || typeof address !== 'string') {
      return;
    }
    const url = URL.parse(address, location.href);
    if (url?.origin !== location.origin || url.pathname.toLowerCase() !== fileHandler.toLowerCase()) {
      return;
    }
    const stack = new Error().stack ?? '';
    const functions = stack.split('\n').slice(3, 11).flatMap(line => {
      const match = /^\s*at (?:async )?([A-Za-z_$][\w$.]{0,63}) \(/.exec(line);
      return match ? [match[1]] : [];
    });
    emit({
      kind: 'download launch',
      channel,
      path: fileHandler,
      parameters: ['t', 'l', 'counter'].filter(name => url.searchParams.has(name)),
      functions
    });
  }

  const downloadClicks = new WeakSet();
  window.addEventListener('click', event => {
    if (Date.now() > deadline || !event.isTrusted) {
      return;
    }
    for (const element of event.composedPath().slice(0, 4)) {
      const label = element.textContent?.trim().replace(/\s+/g, ' ');
      if (label === 'Завантажити всі файли') {
        downloadClicks.add(event);
        const handlers = [];
        for (const target of event.composedPath().slice(0, 8)) {
          if (!(target instanceof Element)) {
            continue;
          }
          const inline = target.getAttribute('onclick');
          const href = target.getAttribute('href');
          const scriptLink = href?.match(/^\s*javascript:\s*(.*)$/is)?.[1];
          for (const [kind, declaration] of [['onclick', inline], ['javascript href', scriptLink]]) {
            if (!declaration || declaration.length > 4096) {
              continue;
            }
            const call = /^\s*(?:return\s+)?([A-Za-z_$][\w$]*(?:\.[A-Za-z_$][\w$]*)*)\s*\(/.exec(declaration);
            handlers.push({
              kind,
              functionName: call?.[1],
              mentionsFileHandler: /\bFileHandler\.ashx\b/i.test(declaration)
            });
          }
          if (!inline && typeof target.onclick === 'function') {
            handlers.push({ kind: 'onclick property', functionName: target.onclick.name });
          }
        }
        emit({ kind: 'download command', handlers });
        break;
      }
    }
  }, true);

  if (/^\/askod-ssl\/(?:WindowEditCard|PdfViewerFrame)\.aspx$/i.test(location.pathname)) {
    observeDownloadClickListeners();
  }

  function observeDownloadClickListeners() {
    const nativeAdd = EventTarget.prototype.addEventListener;
    const nativeRemove = EventTarget.prototype.removeEventListener;
    const wrappers = new WeakMap();
    let observedCount = 0;

    EventTarget.prototype.addEventListener = function (type, listener, options) {
      if (type !== 'click' || !listener || !['function', 'object'].includes(typeof listener)) {
        return Reflect.apply(nativeAdd, this, arguments);
      }
      let wrapper = wrappers.get(listener);
      if (!wrapper) {
        if (Date.now() > deadline) {
          return Reflect.apply(nativeAdd, this, arguments);
        }
        wrapper = function (event) {
          const callback = typeof listener === 'function' ? listener : listener.handleEvent;
          if (typeof callback === 'function' && Date.now() <= deadline && downloadClicks.has(event) && observedCount < 20) {
            observedCount++;
            emit({ kind: 'click listener', functionName: callback.name });
          }
          return Reflect.apply(callback, typeof listener === 'function' ? this : listener, [event]);
        };
        wrappers.set(listener, wrapper);
      }
      return Reflect.apply(nativeAdd, this, [type, wrapper, options]);
    };

    EventTarget.prototype.removeEventListener = function (type, listener, options) {
      const wrapper = type === 'click' && wrappers.get(listener);
      return Reflect.apply(nativeRemove, this, [type, wrapper || listener, options]);
    };
  }

  const originalWindowOpen = window.open;
  window.open = function (address) {
    traceDownloadLaunch('window.open', address);
    return Reflect.apply(originalWindowOpen, this, arguments);
  };

  const originalAnchorClick = HTMLAnchorElement.prototype.click;
  HTMLAnchorElement.prototype.click = function () {
    traceDownloadLaunch('anchor.click', this.href);
    return Reflect.apply(originalAnchorClick, this, arguments);
  };

  const originalSetAttribute = Element.prototype.setAttribute;
  Element.prototype.setAttribute = function (name, value) {
    if ((this instanceof HTMLIFrameElement && name === 'src') ||
        (this instanceof HTMLAnchorElement && name === 'href')) {
      traceDownloadLaunch('setAttribute', value);
    }
    return Reflect.apply(originalSetAttribute, this, arguments);
  };

  const iframeSource = Object.getOwnPropertyDescriptor(HTMLIFrameElement.prototype, 'src');
  if (iframeSource?.configurable && iframeSource.set) {
    Object.defineProperty(HTMLIFrameElement.prototype, 'src', {
      ...iframeSource,
      set(address) {
        traceDownloadLaunch('iframe.src', address);
        Reflect.apply(iframeSource.set, this, [address]);
      }
    });
  }

  function matches(method, address) {
    if (Date.now() > deadline) {
      return false;
    }
    try {
      const url = new URL(address, location.href);
      const path = url.pathname.toLowerCase();
      const verb = method?.toUpperCase();
      if (url.origin === location.origin && (verb === 'POST' && path === endpoint ||
          (verb === 'GET' || verb === 'POST') && path === viewerPath)) {
        return { path, method: verb };
      }
      return false;
    } catch {
      return false;
    }
  }

  function schema(value, depth = 0) {
    if (remaining-- <= 0 || depth >= 10) {
      return { type: 'truncated' };
    }
    if (value === null) {
      return { type: 'null' };
    }
    if (Array.isArray(value)) {
      const samples = value.slice(0, 3).map(item => schema(item, depth + 1));
      return { type: 'array', samples };
    }
    if (typeof value === 'object') {
      const fields = Object.entries(value).slice(0, 80).map(([key, item]) => {
        const name = /^[A-Za-z_$][A-Za-z0-9_$]{0,47}$/.test(key) &&
          !/\d{5,}|[A-Za-z0-9]{30,}/.test(key) ? key : '[dynamic key]';
        const sensitive = /token|secret|password|cookie|authorization|session|credential/i.test(key);
        return {
          name,
          schema: sensitive ? { type: 'redacted' } : schema(item, depth + 1)
        };
      });
      return { type: 'object', fields };
    }
    return { type: typeof value };
  }

  function report(target, status, value) {
    if (Date.now() > deadline || status && (status < 200 || status >= 300) || value?.length > maximumBody) {
      return;
    }
    try {
      if (target.path === endpoint && typeof value === 'string') {
        value = JSON.parse(value);
      }
      const result = { ...target, status };
      if (value && typeof value === 'object') {
        remaining = 500;
        result.schema = schema(value);
        result.embedded = describeEmbeddedResponse(value.d);
      } else {
        result.embedded = describeEmbeddedResponse(value);
      }
      emit(result);
    } catch {
      // Успешный ответ с JSON может прийти в следующем запросе.
    }
  }

  function describeEmbeddedResponse(value) {
    if (typeof value !== 'string' || value.length > maximumBody) {
      return undefined;
    }
    const content = value.trim();
    if (content.startsWith('{') || content.startsWith('[')) {
      try {
        const embedded = JSON.parse(content);
        remaining = 500;
        return { format: 'json', schema: schema(embedded) };
      } catch {
        // Проверяем ссылки, если строка не является валидным JSON.
      }
    }
    const normalized = content.replace(/&amp;|&#38;|&#x26;/gi, '&');
    const links = [];
    const seen = new Set();
    const handlerUrls = /(?:https?:\/\/[^\s"'<>/]+)?(?:\/askod-ssl\/)?\/?Handlers\/FileHandler\.ashx(?:\?[^\s"'<>#]*)?/gi;
    for (const match of normalized.matchAll(handlerUrls)) {
      const url = URL.parse(match[0], location.origin + '/askod-ssl/');
      if (!url) {
        continue;
      }
      if (url.origin !== location.origin || url.pathname.toLowerCase() !== '/askod-ssl/handlers/filehandler.ashx') {
        continue;
      }
      const parameters = [...new Set(url.searchParams.keys())].filter(key => /^[A-Za-z][A-Za-z0-9_-]{0,23}$/.test(key));
      const signature = parameters.slice().sort().join(',');
      if (!seen.has(signature)) {
        seen.add(signature);
        links.push({ path: '/askod-ssl/Handlers/FileHandler.ashx', parameters });
      }
      if (links.length >= 12) {
        break;
      }
    }
    if (/<[A-Za-z!]/.test(content)) {
      return { format: 'html', links };
    }
    return links.length > 0 ? { format: 'link', links } : { format: 'unknown' };
  }

  const originalOpen = XMLHttpRequest.prototype.open;
  const originalSend = XMLHttpRequest.prototype.send;
  if (location.pathname.toLowerCase() === viewerPath) {
    // Ответ навигации виден через DOM, когда документ загружен.
    const navigation = performance.getEntriesByType('navigation')[0];
    document.addEventListener('DOMContentLoaded', () => {
      report({ method: 'NAVIGATION', path: viewerPath }, navigation?.responseStatus || undefined,
        document.documentElement.outerHTML);
    }, { once: true });
  }

  const observed = new WeakMap();
  XMLHttpRequest.prototype.open = function (method, address) {
    traceDownloadLaunch('xhr', address);
    const result = Reflect.apply(originalOpen, this, arguments);
    observed.delete(this);
    const target = matches(method, address);
    if (target) {
      observed.set(this, target);
    }
    return result;
  };
  XMLHttpRequest.prototype.send = function () {
    const target = observed.get(this);
    if (observed.delete(this)) {
      this.addEventListener('load', () => {
        if (this.responseType === 'json' || this.responseType === 'text' || !this.responseType) {
          report(target, this.status, this.response);
        }
      }, { once: true });
    }
    return Reflect.apply(originalSend, this, arguments);
  };

  const originalFetch = window.fetch;
  window.fetch = function (input, options) {
    const response = Reflect.apply(originalFetch, this, arguments);
    const address = input instanceof Request ? input.url : input;
    traceDownloadLaunch('fetch', address);
    const target = matches(options?.method ?? input?.method ?? 'GET', address);
    if (target) {
      void response.then(async result => {
        if (result.ok && Number(result.headers.get('content-length')) <= maximumBody) {
          await readFetchResponse(target, result);
        }
      }).catch(() => {});
    }
    return response;
  };

  async function readLimitedText(response) {
    const reader = response.body?.getReader();
    if (!reader) {
      return;
    }
    const decoder = new TextDecoder();
    let body = '';
    let size = 0;
    while (true) {
      const { done, value } = await reader.read();
      if (done) {
        return body + decoder.decode();
      }
      size += value.byteLength;
      if (size > maximumBody) {
        void reader.cancel().catch(() => {});
        return;
      }
      body += decoder.decode(value, { stream: true });
    }
  }

  async function readFetchResponse(target, response) {
    const body = await readLimitedText(response.clone());
    if (body !== undefined) {
      report(target, response.status, body);
    }
  }
})();