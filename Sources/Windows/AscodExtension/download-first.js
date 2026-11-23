async function downloadRegistryCards(origin, count, selection) {
  const visible = element => !!element?.getClientRects().length &&
    getComputedStyle(element).visibility === 'visible' &&
    getComputedStyle(element).display !== 'none';

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
        for (const item of menu.querySelectorAll('[id="downloadFiles"], [key="downloadFiles"], .context-menu-item.context-menu-icon-downloadFiles')) {
          const parent = item.parentElement?.closest('.context-menu-submenu');
          const caption = item.querySelector(':scope > span') ?? item;
          if (enabled(item) && (item.id === 'downloadFiles' || item.getAttribute('key') === 'downloadFiles' ||
              menuLabel(caption) === label) && (visible(item) || visible(parent))) {
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
  const trace = [];
  if (selection) {
    const readCard = window[Symbol.for('unitoffice.ascod.direct-download')];
    const card = typeof readCard === 'function' && readCard(selection.resultId, selection.rowKey);
    const opened = card && await waitFor(() => {
      const current = getViewer();
      const address = current?.frame.contentDocument?.location.href;
      return address && new URL(address).searchParams.get('counter') === String(card.counter) && current;
    }, 20000);
    if (!opened) {
      return { error: 'Open the selected card and wait for its files panel.' };
    }
    const result = await downloadFromViewer(opened);
    return { ok: !result.error, ...result };
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
    return downloadFromViewer(opened);
  }

  async function downloadFromViewer(opened) {
    const { frame, viewer } = opened;
    log('Files viewer opened.');
    if (selection) {
      const jquery = viewer.defaultView.jQuery;
      if (typeof jquery !== 'function') {
        return { error: 'The attachment menu is unavailable in this files panel.' };
      }
      // Меню привязано и к самой строке дерева, даже когда кнопка видна только при наведении.
      const targets = await waitFor(() => {
        const rows = Array.from(viewer.querySelectorAll('#EcdTreeView li > span[id]')).filter(visible);
        const actions = rows.map(row => {
          const button = row.querySelector(':scope > .additionalBtn');
          return visible(button) ? button : row;
        });
        return actions.length && actions;
      }, 8000);
      if (!targets) {
        return { error: 'No visible attachment rows in the card tree.' };
      }
      for (const action of targets) {
        if (getViewer()?.viewer !== viewer || !visible(action)) {
          return { error: 'The active attachment tree changed.' };
        }
        if (action.matches('.additionalBtn')) {
          action.click();
        } else {
          action.dispatchEvent(new viewer.defaultView.MouseEvent('contextmenu', { bubbles: true, cancelable: true, button: 2 }));
        }
        const root = await waitFor(() => jquery(action).data('contextMenu'), 700);
        if (!root || root.$trigger?.[0] !== action || !visible(root.$menu?.[0])) {
          continue;
        }
        const group = root.items?.downloadMain;
        const item = group?.items?.downloadFiles;
        const node = item?.$node?.[0];
        const groupNode = group?.$node?.[0];
        if (!node || !groupNode || !root.$menu[0].contains(groupNode) || !groupNode.contains(node) ||
            !visible(groupNode) || !enabled(node) || viewer.defaultView.getComputedStyle(node).display === 'none' ||
            node.classList.contains(root.classNames.notSelectable) || !enabled(groupNode)) {
          root.$menu.trigger('contextmenu:hide');
          continue;
        }
        const data = jquery(node).data();
        if (data.contextMenuKey !== 'downloadFiles' || data.contextMenuRoot !== root ||
            data.contextMenu?.items?.downloadFiles !== item) {
          return { error: 'Could not verify the download command in the attachment menu.' };
        }
        const callback = data.contextMenu.callbacks?.downloadFiles ?? root.callback;
        if (typeof callback !== 'function') {
          return { error: 'The attachment menu has no download action.' };
        }
        if (callback.call(root.$trigger, 'downloadFiles', root) !== false) {
          root.$menu.trigger('contextmenu:hide');
        }
        return {};
      }
      return { error: 'No visible attachment row offers Download all files.' };
    }

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
      for (const page of pages) {
        const parents = page.querySelectorAll('.context-menu-list [id="downloadMain"], .context-menu-list [key="downloadMain"]');
        const parent = Array.from(parents).find(item => visible(item) && enabled(item));
        if (parent) {
          return { parent };
        }
      }
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
    const parent = menu.parent?.closest('.context-menu-submenu') ??
      menu.direct?.parentElement?.closest('.context-menu-submenu') ?? menu.parent;
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
      const jquery = view.jQuery;
      if (typeof jquery === 'function' && visible(parent) && enabled(parent) &&
          parent.matches('.context-menu-submenu') && jquery(parent).data('contextMenuRoot')) {
        jquery(parent).trigger('contextmenu:focus');
      }
      log('Download submenu hovered.');
    }
    const downloadAll = await waitFor(() => {
      const command = findMenuItem(pages, downloadAllLabel);
      if (visible(command) && enabled(command)) {
        return command;
      }
      const archive = findArchiveCommand(pages, downloadAllLabel);
      return visible(archive) && enabled(archive) ? archive : null;
    }, 3500);
    if (!downloadAll || !visible(frame)) {
      const childExists = pages.some(page => page.querySelector('[id="downloadFiles"], [key="downloadFiles"]'));
      return { error: childExists ?
        'The site created the ZIP menu item, but it remained hidden or disabled.' :
        'The open menu did not expose the downloadFiles command.' };
    }
    await new Promise(resolve => setTimeout(resolve, 350));
    if (!visible(downloadAll) || !enabled(downloadAll) || !visible(frame)) {
      return { error: 'The ZIP command closed or became unavailable before selection.' };
    }
    log('Visible ZIP command found. Sending selection event.');
    if (downloadAll.matches('.context-menu-item:not([key])')) {
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