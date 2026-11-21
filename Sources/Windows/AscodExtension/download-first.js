async function downloadRegistryCards(origin, count) {
  const visible = element => !!element?.getClientRects().length &&
    getComputedStyle(element).visibility === 'visible';

  async function waitFor(find, timeoutMs) {
    const deadline = performance.now() + timeoutMs;
    while (performance.now() < deadline) {
      const result = find();
      if (result) {
        return result;
      }
      await new Promise(resolve => setTimeout(resolve, 150));
    }
    return null;
  }

  function getViewer() {
    const frames = document.querySelectorAll('iframe[id^="cardframe_"]');
    for (const frame of frames) {
      if (!visible(frame)) {
        continue;
      }
      const card = frame.contentDocument;
      const viewerFrame = card?.getElementById('pdfViewerInFrame');
      if (!viewerFrame || !visible(viewerFrame)) {
        continue;
      }
      const viewer = viewerFrame.contentDocument;
      if (viewer?.getElementById('EcdTreeView')) {
        return { frame, viewer };
      }
    }
    return null;
  }

  function menuLabel(element) {
    return element.textContent.trim().replace(/\s+/g, ' ');
  }

  function enabled(item) {
    return !item.closest('.context-menu-disabled, [aria-disabled="true"], [disabled]');
  }

  function findArchiveCommand(documents, label) {
    for (const page of documents) {
      const menus = page.querySelectorAll('.context-menu-list');
      for (const menu of menus) {
        if (menu.parentElement?.closest('.context-menu-list') || !visible(menu)) {
          continue;
        }
        for (const item of menu.querySelectorAll('.context-menu-item.context-menu-icon-downloadFiles')) {
          const parent = item.parentElement?.closest('.context-menu-submenu');
          const caption = item.querySelector(':scope > span') ?? item;
          if (enabled(item) && menuLabel(caption) === label && (visible(item) || visible(parent))) {
            return item;
          }
        }
      }
    }
    return null;
  }

  function findMenuItem(documents, label) {
    for (const page of documents) {
      const menus = page.querySelectorAll('#panelContextMenu, .context-menu-list, [role="menu"]');
      for (const menu of menus) {
        if (!visible(menu)) {
          continue;
        }
        const items = Array.from(menu.querySelectorAll('a, button, [role="menuitem"], li, div, span'));
        const text = items.reverse().find(item => visible(item) && menuLabel(item) === label);
        if (text) {
          const control = text.closest('.context-menu-item, a, button, [role="menuitem"], li');
          const item = control && menu.contains(control) ? control : text;
          if (enabled(item)) {
            return item;
          }
        }
      }
    }
    return null;
  }

  if (location.origin !== origin || !/\/Defaultv4\.aspx$/i.test(location.pathname)) {
    return { error: 'Open the Ascod registry on the selected server.' };
  }
  if (!Number.isInteger(count) || count < 1 || count > 20) {
    return { error: 'Choose a document count from 1 to 20.' };
  }
  const grid = document.querySelector('[id$="_AskodGridCards_CommonGrid"]');
  if (!visible(grid)) {
    return { error: 'Open the filtered document registry first.' };
  }
  const rows = Array.from(grid.querySelectorAll('tr.rgRow, tr.rgAltRow'))
    .filter(item => visible(item) && item.cells.length > 2)
    .slice(0, count);
  if (!rows.length) {
    return { error: 'There are no visible documents in the registry.' };
  }
  const activeTab = document.querySelector('#tabPanels .activeTab');
  const incomingTab = document.getElementById('askodtab_ReceivedCard');
  const registryTab = [activeTab, incomingTab].find(tab => visible(tab) && !tab.id.startsWith('askodtab_card_'));

  let requested = 0;
  const trace = [];
  function log(message) {
    trace.push({
      time: Date.now(),
      message
    });
  }

  for (const row of rows) {
    log('Opening registry row ' + (requested + 1) + '.');
    if (requested > 0) {
      if (!visible(registryTab)) {
        return { requested, trace, error: 'Could not find the registry tab to open the next document.' };
      }
      registryTab.click();
      const returned = await waitFor(() => row.isConnected && visible(row), 8000);
      if (!returned) {
        return { requested, trace, error: 'Could not return to the same filtered registry.' };
      }
    }
    const result = await downloadCard(row);
    if (result.error) {
      return { requested, trace, error: result.error };
    }
    requested++;
  }
  return { requested, trace };

  async function downloadCard(row) {
    const cell = Array.from(row.cells).find(item => visible(item) &&
      item.textContent.trim() && !item.querySelector('button, input, a, img'));
    if (!cell) {
      return { error: 'Could not find a document cell in the selected row.' };
    }

    const previousViewer = getViewer();
    cell.dispatchEvent(new MouseEvent('dblclick', {
      bubbles: true,
      cancelable: true,
      button: 0,
      detail: 2,
      view: window
    }));

    const opened = await waitFor(() => {
      const current = getViewer();
      if (!current || current.viewer === previousViewer?.viewer) {
        return null;
      }
      return current;
    }, 12000);
    if (!opened) {
      return { error: 'The selected document did not open its files viewer.' };
    }
    const { frame, viewer } = opened;
    log('Files viewer opened.');
    const action = await waitFor(() => {
      const buttons = viewer.querySelectorAll('#EcdTreeView .file .additionalBtn');
      return Array.from(buttons).find(visible);
    }, 8000);
    if (!action || !visible(frame)) {
      return { error: 'Could not find the attachments menu in the opened document.' };
    }

    const pages = [viewer, frame.contentDocument, document].filter(Boolean);
    const downloadAllLabel = '\u0417\u0430\u0432\u0430\u043d\u0442\u0430\u0436\u0438\u0442\u0438 \u0432\u0441\u0456 \u0444\u0430\u0439\u043b\u0438';
    const downloadLabel = '\u0417\u0430\u0432\u0430\u043d\u0442\u0430\u0436\u0438\u0442\u0438';
    if (findArchiveCommand(pages, downloadAllLabel) ||
        findMenuItem(pages, downloadAllLabel) || findMenuItem(pages, downloadLabel)) {
      return { error: 'Close the open context menu and try again.' };
    }
    action.click();
    log('File menu triggered.');
    const menu = await waitFor(() => {
      const direct = findArchiveCommand(pages, downloadAllLabel) ?? findMenuItem(pages, downloadAllLabel);
      if (direct) {
        return { direct };
      }
      const parent = findMenuItem(pages, downloadLabel);
      return parent ? { parent } : null;
    }, 3000);
    if (!menu) {
      return { error: 'The file menu did not expose a visible download item.' };
    }
    const parent = menu.parent ?? menu.direct?.parentElement?.closest('.context-menu-submenu');
    if (parent) {
      const view = parent.ownerDocument.defaultView;
      const bounds = parent.getBoundingClientRect();
      for (const type of ['mouseover', 'mouseenter']) {
        parent.dispatchEvent(new view.MouseEvent(type, {
          bubbles: type === 'mouseover',
          clientX: Math.floor(bounds.left + bounds.width / 2),
          clientY: Math.floor(bounds.top + bounds.height / 2),
          view
        }));
      }
      log('Download submenu hovered.');
    }
    const downloadAll = await waitFor(() => {
      const command = findArchiveCommand(pages, downloadAllLabel) ?? findMenuItem(pages, downloadAllLabel);
      return visible(command) && enabled(command) ? command : null;
    }, 3500);
    if (!downloadAll || !visible(frame)) {
      return { error: 'The ZIP command did not become visible in the download submenu.' };
    }
    await new Promise(resolve => setTimeout(resolve, 350));
    if (!visible(downloadAll) || !enabled(downloadAll) || !visible(frame)) {
      return { error: 'The ZIP command closed or became unavailable before selection.' };
    }
    log('Visible ZIP command found. Sending selection event.');
    if (downloadAll.matches('.context-menu-item')) {
      const view = downloadAll.ownerDocument.defaultView;
      const bounds = downloadAll.getBoundingClientRect();
      downloadAll.dispatchEvent(new view.MouseEvent('mouseup', {
        bubbles: true,
        cancelable: true,
        button: 0,
        clientX: Math.floor(bounds.left + bounds.width / 2),
        clientY: Math.floor(bounds.top + bounds.height / 2),
        view
      }));
    } else {
      downloadAll.click();
    }
    const closed = await waitFor(() => !visible(downloadAll), 2500);
    if (!closed) {
      return { error: 'The download menu did not react to selection. No ZIP was confirmed.' };
    }
    log('Menu closed after ZIP selection.');
    return {};
  }
}