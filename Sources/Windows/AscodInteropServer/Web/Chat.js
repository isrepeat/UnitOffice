let cursor = 0;
let running = false;
let connecting = false;
let token = '';
let role = 'local';
let pendingFiles = [];
let readingFiles = false;
let sending = false;
let pollController;
let pollTimer;

const $ = id => document.getElementById(id);
const fixedRole = {
	'/local': 'local',
	'/sedo': 'sedo',
}[location.pathname];

if (fixedRole) {
	const label = fixedRole === 'local' ? 'Локальный' : 'СЕДО';
	document.title = label;
	document.querySelector('h1').textContent = label;
	$('sender').closest('label').hidden = true;
}

function setStatus(state, text) {
	const status = $('status');
	status.dataset.state = state;
	status.textContent = text;
}

async function copyText(value) {
	if (navigator.clipboard) {
		try {
			await navigator.clipboard.writeText(value);
			return;
		} catch {
			// Fall back to the legacy clipboard method when the chat runs over HTTP.
		}
	}

	const field = document.createElement('textarea');
	const focused = document.activeElement;
	field.value = value;
	field.readOnly = true;
	field.style.cssText = 'position:fixed;inset:0 auto auto 0;opacity:0;pointer-events:none';
	document.body.append(field);
	try {
		field.select();
		if (!document.execCommand('copy')) {
			throw new Error('Copy failed');
		}
	} finally {
		field.remove();
		focused?.focus({preventScroll: true});
	}
}

function makeCopyButton(value, label = 'Копировать сообщение', className = 'copy-message') {
	const button = document.createElement('button');
	button.type = 'button';
	button.className = className;
	button.title = label;
	button.setAttribute('aria-label', label);
	let reset;
	button.addEventListener('click', async () => {
		try {
			await copyText(typeof value === 'function' ? value() : value);
			button.dataset.result = '✓';
			button.title = 'Скопировано';
		} catch {
			button.dataset.result = '!';
			button.title = 'Не удалось скопировать';
		}
		button.setAttribute('aria-label', button.title);
		clearTimeout(reset);
		reset = setTimeout(() => {
			delete button.dataset.result;
			button.title = label;
			button.setAttribute('aria-label', label);
		}, 1400);
	});
	return button;
}

function makeImageActions(image, attachment) {
	const actions = document.createElement('div');
	actions.className = 'image-actions';
	const download = document.createElement('a');
	download.href = image.src;
	download.download = attachment.name;
	download.className = 'image-action image-action--download';
	download.title = 'Скачать изображение';
	download.setAttribute('aria-label', download.title);
	if (attachment.mime !== 'image/png') {
		actions.append(download);
		return actions;
	}
	const copy = document.createElement('button');
	copy.type = 'button';
	copy.className = 'image-action image-action--copy';
	copy.title = 'Копировать изображение';
	copy.setAttribute('aria-label', copy.title);
	const feedback = document.createElement('p');
	feedback.className = 'image-feedback';
	feedback.setAttribute('role', 'status');
	feedback.hidden = true;
	copy.addEventListener('click', async () => {
		feedback.hidden = false;
		if (!navigator.clipboard?.write || typeof ClipboardItem === 'undefined') {
			feedback.textContent = 'Нажми правой кнопкой на изображение → «Копировать изображение». Кнопка копирования требует HTTPS или localhost.';
			return;
		}
		copy.disabled = true;
		try {
			const encoded = image.src.slice(image.src.indexOf(',') + 1);
			const bytes = Uint8Array.from(atob(encoded), character => character.charCodeAt(0));
			const blob = new Blob([bytes], {type: 'image/png'});
			await navigator.clipboard.write([new ClipboardItem({'image/png': blob})]);
			feedback.textContent = 'Изображение скопировано';
		} catch {
			feedback.textContent = 'Браузер не разрешил копирование. Нажми правой кнопкой на изображение → «Копировать изображение».';
		} finally {
			copy.disabled = false;
		}
	});
	actions.append(copy, download, feedback);
	return actions;
}

function readAttachmentEntry(file) {
	if (typeof file?.name !== 'string' || typeof file.data !== 'string' || !/^[A-Za-z0-9+/]*={0,2}$/.test(file.data)) {
		return;
	}
	let name = 'attachment';
	try {
		name = decodeURIComponent(file.name) || name;
	} catch {
		// Preserve access to the attachment if the name is malformed.
	}
	const extension = name.split('.').pop().toLowerCase();
	const mime = new Map([
		['png', 'image/png'],
		['jpg', 'image/jpeg'],
		['jpeg', 'image/jpeg'],
		['gif', 'image/gif'],
		['webp', 'image/webp'],
		['bmp', 'image/bmp'],
		['avif', 'image/avif'],
		['ico', 'image/x-icon'],
	]).get(extension);
	return {name, data: file.data, mime};
}

function appendFile(item, file) {
	const {name} = file;
	const row = document.createElement('div');
	row.className = 'file-attachment';
	const label = document.createElement('span');
	label.textContent = name;
	row.append(label);
	const download = document.createElement('a');
	download.href = 'data:application/octet-stream;base64,' + file.data;
	download.download = name;
	download.className = 'image-action image-action--download';
	download.title = 'Скачать ' + name;
	download.setAttribute('aria-label', download.title);
	if (/\.(txt|md|csv|tsv|log|json|xml|ini|cfg|conf|ya?ml|toml|html?|css|js|sql|ps1|bat|cmd)$/i.test(name)) {
		const copy = makeCopyButton(() => {
			const bytes = Uint8Array.from(atob(file.data), character => character.charCodeAt(0));
			const encoding = bytes[0] === 255 && bytes[1] === 254 ? 'utf-16le'
				: (bytes[0] === 254 && bytes[1] === 255 ? 'utf-16be' : 'utf-8');
			try {
				return new TextDecoder(encoding, {fatal: true}).decode(bytes);
			} catch {
				if (encoding === 'utf-8') {
					return new TextDecoder('windows-1251').decode(bytes);
				}
				throw new Error('Invalid text encoding');
			}
		}, 'Копировать содержимое ' + name, 'image-action image-action--copy');
		row.append(copy);
	}
	row.append(download);
	item.append(row);
}

function appendImageGallery(item, images) {
	const gallery = document.createElement('div');
	gallery.className = 'image-gallery';
	gallery.classList.toggle('image-gallery--grid', images.length > 1);
	item.classList.add('message--gallery');
	for (const attachment of images) {
		const tile = document.createElement('div');
		tile.className = 'image-tile';
		const image = document.createElement('img');
		image.className = 'message-image';
		image.alt = attachment.name;
		image.src = 'data:' + attachment.mime + ';base64,' + attachment.data;
		tile.append(image, makeImageActions(image, attachment));
		gallery.append(tile);
	}
	item.append(gallery);
}

function makeAttachmentZip(attachments) {
	const crcTable = Uint32Array.from({length: 256}, (_, value) => {
		for (let bit = 0; bit < 8; bit++) {
			value = (value >>> 1) ^ (value & 1 ? 0xedb88320 : 0);
		}
		return value;
	});
	const encoder = new TextEncoder();
	const entries = [];
	const directory = [];
	const usedNames = new Set();
	let offset = 0;
	let directorySize = 0;
	for (const attachment of attachments) {
		const baseName = attachment.name.replace(/[\\/:*?"<>|\x00-\x1f]/g, '_').replace(/[. ]+$/, '') || 'attachment';
		const extensionStart = baseName.lastIndexOf('.');
		const stem = extensionStart > 0 ? baseName.slice(0, extensionStart) : baseName;
		const extension = extensionStart > 0 ? baseName.slice(extensionStart) : '';
		let name = baseName;
		for (let suffix = 2; usedNames.has(name.toLowerCase()); suffix++) {
			name = stem + ' (' + suffix + ')' + extension;
		}
		usedNames.add(name.toLowerCase());
		const nameBytes = encoder.encode(name);
		const data = Uint8Array.from(atob(attachment.data), character => character.charCodeAt(0));
		let crc = 0xffffffff;
		for (const byte of data) {
			crc = (crc >>> 8) ^ crcTable[(crc ^ byte) & 255];
		}
		crc ^= 0xffffffff;

		const local = new DataView(new ArrayBuffer(30));
		local.setUint32(0, 0x04034b50, true);
		local.setUint16(4, 20, true);
		local.setUint16(6, 0x0800, true);
		local.setUint16(12, 0x0021, true);
		local.setUint32(14, crc, true);
		local.setUint32(18, data.length, true);
		local.setUint32(22, data.length, true);
		local.setUint16(26, nameBytes.length, true);
		entries.push(local.buffer, nameBytes, data);

		const central = new DataView(new ArrayBuffer(46));
		central.setUint32(0, 0x02014b50, true);
		central.setUint16(4, 20, true);
		new Uint8Array(central.buffer, 6, 26).set(new Uint8Array(local.buffer, 4, 26));
		central.setUint32(42, offset, true);
		directory.push(central.buffer, nameBytes);
		offset += local.byteLength + nameBytes.length + data.length;
		directorySize += central.byteLength + nameBytes.length;
	}
	const end = new DataView(new ArrayBuffer(22));
	end.setUint32(0, 0x06054b50, true);
	end.setUint16(8, attachments.length, true);
	end.setUint16(10, attachments.length, true);
	end.setUint32(12, directorySize, true);
	end.setUint32(16, offset, true);
	return new Blob([...entries, ...directory, end.buffer], {type: 'application/zip'});
}

function makeDownloadAllButton(attachments, id) {
	const button = document.createElement('button');
	button.type = 'button';
	button.className = 'image-action image-action--download download-all';
	button.title = 'Скачать все вложения (ZIP)';
	button.setAttribute('aria-label', button.title);
	button.addEventListener('click', () => {
		const url = URL.createObjectURL(makeAttachmentZip(attachments));
		const link = document.createElement('a');
		link.href = url;
		link.download = 'unitoffice-attachments-' + id + '.zip';
		document.body.append(link);
		link.click();
		link.remove();
		setTimeout(() => {
			URL.revokeObjectURL(url);
		}, 60000);
	});
	return button;
}

function makeSaveToFolderButton(attachments, feedback) {
	const button = document.createElement('button');
	button.type = 'button';
	button.className = 'image-action image-action--folder';
	button.title = 'Скачать вложения в папку без архива';
	button.setAttribute('aria-label', button.title);
	button.disabled = typeof window.showDirectoryPicker !== 'function';
	if (button.disabled) {
		button.title = 'Сохранение в папку доступно в Edge через HTTPS или localhost';
	}
	button.addEventListener('click', async () => {
		button.disabled = true;
		feedback.hidden = true;
		let saved = 0;
		try {
			const folder = await window.showDirectoryPicker({
				mode: 'readwrite',
				startIn: 'downloads'
			});
			const usedNames = new Set();
			for await (const name of folder.keys()) {
				usedNames.add(name.toLowerCase());
			}
			for (const attachment of attachments) {
				const cleanName = attachment.name.replace(/[\\/:*?"<>|\x00-\x1f]/g, '_').replace(/[. ]+$/, '') || 'attachment';
				const reservedName = /^(?:con|prn|aux|nul|com[1-9\u00b9\u00b2\u00b3]|lpt[1-9\u00b9\u00b2\u00b3])(?:\.|$)/i;
				const baseName = reservedName.test(cleanName) ? '_' + cleanName : cleanName;
				const extensionStart = baseName.lastIndexOf('.');
				const stem = extensionStart > 0 ? baseName.slice(0, extensionStart) : baseName;
				const extension = extensionStart > 0 ? baseName.slice(extensionStart) : '';
				let name = baseName;
				for (let suffix = 2; usedNames.has(name.toLowerCase()); suffix++) {
					name = stem + ' (' + suffix + ')' + extension;
				}
				usedNames.add(name.toLowerCase());
				const bytes = Uint8Array.from(atob(attachment.data), character => character.charCodeAt(0));
				const file = await folder.getFileHandle(name, {create: true});
				const stream = await file.createWritable();
				try {
					await stream.write(bytes);
					await stream.close();
				} catch (error) {
					await stream.abort().catch(() => {});
					throw error;
				}
				saved++;
			}
			feedback.textContent = 'Сохранено файлов: ' + saved;
			feedback.hidden = false;
		} catch (error) {
			if (error.name !== 'AbortError' || saved > 0) {
				feedback.textContent = 'Не удалось сохранить все вложения. Сохранено файлов: ' + saved;
				feedback.hidden = false;
			}
		} finally {
			button.disabled = false;
		}
	});
	return button;
}

function updateMessageTextToggle(text) {
	const toggle = text.nextElementSibling;
	if (toggle.getAttribute('aria-expanded') === 'true') {
		return;
	}
	const clipped = text.scrollHeight > text.clientHeight;
	toggle.hidden = !clipped;
	text.classList.toggle('message-text--fade', clipped);
}

const messageTextObserver = new ResizeObserver(entries => {
	for (const {target} of entries) {
		updateMessageTextToggle(target);
	}
});

function appendMessage(message, peerAcknowledged = null) {
	const item = document.createElement('article');
	item.className = 'message';
	item.classList.toggle('message--own', message.sender === role);

	const header = document.createElement('div');
	header.className = 'message-header';
	const meta = document.createElement('p');
	meta.className = 'message-meta';
	const sender = message.sender === 'sedo' ? 'СЕДО' : 'Локальный';
	meta.textContent = sender + ' · #' + message.id;
	header.append(meta);

	item.append(header);
	const attachments = [];
	const pngPrefix = 'data:image/png;base64,';
	if (typeof message.image === 'string' && /^data:image\/png;base64,[A-Za-z0-9+/=]+$/.test(message.image)) {
		attachments.push({
			name: 'unitoffice-screenshot-' + message.id + '.png',
			data: message.image.slice(pngPrefix.length),
			mime: 'image/png',
		});
	}
	const files = Array.isArray(message.files) ? message.files : [{name: message.fileName, data: message.fileData}];
	for (const file of files) {
		const attachment = readAttachmentEntry(file);
		if (attachment) {
			attachments.push(attachment);
		}
	}
	const thumbnails = attachments.filter(attachment => attachment.mime).slice(0, 4);
	if (thumbnails.length) {
		appendImageGallery(item, thumbnails);
	}
	for (const attachment of attachments) {
		if (!thumbnails.includes(attachment)) {
			appendFile(item, attachment);
		}
	}
	const text = document.createElement('p');
	text.className = 'message-text message-text--collapsed';
	text.id = 'message-text-' + message.id;
	text.textContent = message.text;
	if (message.text) {
		header.append(makeCopyButton(message.text));
		const toggle = document.createElement('button');
		toggle.className = 'message-text-toggle';
		toggle.type = 'button';
		toggle.textContent = 'Показать полностью';
		toggle.hidden = true;
		toggle.setAttribute('aria-controls', text.id);
		toggle.setAttribute('aria-expanded', 'false');
		toggle.addEventListener('click', () => {
			const expand = toggle.getAttribute('aria-expanded') === 'false';
			text.classList.toggle('message-text--collapsed', !expand);
			text.classList.remove('message-text--fade');
			toggle.setAttribute('aria-expanded', String(expand));
			toggle.textContent = expand ? 'Свернуть' : 'Показать полностью';
			updateMessageTextToggle(text);
		});
		item.append(text, toggle);
	}
	if (attachments.length > 0) {
		const feedback = document.createElement('p');
		feedback.className = 'message-meta';
		feedback.setAttribute('role', 'status');
		feedback.hidden = true;
		header.append(makeDownloadAllButton(attachments, message.id));
		header.append(makeSaveToFolderButton(attachments, feedback));
		item.append(feedback);
	}
	if (message.sender === role && Number.isSafeInteger(peerAcknowledged) && message.id > peerAcknowledged) {
		const queued = document.createElement('span');
		queued.className = 'message-spinner';
		queued.dataset.queuedMessage = message.id;
		queued.title = 'Ожидает подтверждения получения второй стороной';
		queued.setAttribute('role', 'img');
		queued.setAttribute('aria-label', queued.title);
		header.append(queued);
	}
	$('messages').append(item);
	if (message.text) {
		updateMessageTextToggle(text);
		messageTextObserver.observe(text);
	}
}

function headers() {
	return {
		'X-Chat-Key': token,
		'X-Chat-Sender': role,
		'Content-Type': 'text/plain;charset=utf-8',
	};
}

async function request(url, options = {}) {
	const response = await fetch(url, {
		...options,
		headers: {...headers(), ...options.headers},
		cache: 'no-store',
		signal: options.signal ?? AbortSignal.timeout(10000),
	});

	if (!response.ok) {
		if (response.status === 507) {
			throw new Error('Delivery queue is full.');
		}
		throw new Error('HTTP ' + response.status);
	}

	return response;
}

async function poll(signal) {
	try {
		const response = await request('/messages?after=' + cursor, {
			signal: AbortSignal.any([signal, AbortSignal.timeout(120000)])
		});
		const data = await response.json();
		if (signal.aborted) {
			return;
		}

		const history = $('messages').parentElement;
		const followLatest = history.scrollHeight - history.scrollTop - history.clientHeight < 48;
		const historyGap = cursor > 0 && data.messages.length > 0 && data.messages[0].id > cursor + 1;

		if (data.latest < cursor) {
			cursor = 0;
			messageTextObserver.disconnect();
			$('messages').replaceChildren();
			$('empty').hidden = false;
			setStatus('online', 'Подключено · история сброшена');
			return;
		}

		for (const message of data.messages) {
			appendMessage(message, data.peerAcknowledged);
			cursor = message.id;

			while ($('messages').children.length > 200) {
				const oldest = $('messages').firstElementChild;
				const text = oldest.querySelector('.message-text');
				if (text) {
					messageTextObserver.unobserve(text);
				}
				oldest.remove();
			}
		}
		if (Number.isSafeInteger(data.peerAcknowledged)) {
			for (const queued of $('messages').querySelectorAll('[data-queued-message]')) {
				if (Number(queued.dataset.queuedMessage) <= data.peerAcknowledged) {
					queued.remove();
				}
			}
		}

		$('empty').hidden = $('messages').children.length > 0;
		if (followLatest && data.messages.length > 0) {
			history.scrollTop = history.scrollHeight;
		}

		setStatus('online', historyGap ? 'Подключено · часть истории недоступна' : 'Подключено');
	} catch {
		if (!signal.aborted) {
			setStatus('error', 'Нет связи · повторяем подключение');
		}
	}
}

const sessionKey = 'unitoffice-chat:' + location.pathname;

function restoreSession() {
	try {
		const saved = JSON.parse(sessionStorage.getItem(sessionKey));
		if (typeof saved?.token === 'string' && (saved.role === 'local' || saved.role === 'sedo')) {
			$('sender').value = saved.role;
			return saved.token;
		}
	} catch {
		// Users can still sign in manually if browser storage is unavailable.
	}
	return '';
}

function rememberSession(session) {
	try {
		if (session) {
			sessionStorage.setItem(sessionKey, JSON.stringify(session));
		} else {
			sessionStorage.removeItem(sessionKey);
		}
	} catch {
		// Sign-in works without browser storage.
	}
}

async function connect(accessKey) {
	if (running || connecting) {
		return;
	}

	token = accessKey;
	role = fixedRole ?? $('sender').value;

	connecting = true;
	const connectButton = $('connect').querySelector('button');
	connectButton.disabled = true;
	setStatus('idle', 'Подключение…');
	try {
		await request('/messages?after=0');
	} catch (error) {
		const unauthorized = error.message === 'HTTP 401';
		if (unauthorized) {
			rememberSession(null);
			$('key').value = '';
		} else {
			$('key').value = accessKey;
		}
		const message = unauthorized ? 'Неверный код доступа' : 'Сервер недоступен';
		setStatus('error', message);
		connectButton.disabled = false;
		return;
	} finally {
		connecting = false;
	}

	running = true;
	rememberSession({token, role});
	$('key').value = '';
	$('connect').hidden = true;
	$('disconnect').hidden = false;
	$('text').disabled = false;
	$('submit').disabled = false;
	$('text').focus();
	setStatus('online', 'Подключено');

	pollController = new AbortController();
	const signal = pollController.signal;
	async function loop() {
		await poll(signal);
		if (!signal.aborted) {
			pollTimer = setTimeout(loop, 1000);
		}
	}
	loop();
}

$('disconnect').onclick = () => {
	running = false;
	pollController.abort();
	clearTimeout(pollTimer);
	rememberSession(null);
	token = '';
	cursor = 0;
	messageTextObserver.disconnect();
	$('messages').replaceChildren();
	$('empty').hidden = false;
	$('send-error').hidden = true;
	$('disconnect').hidden = true;
	$('connect').hidden = false;
	$('connect').querySelector('button').disabled = false;
	$('text').disabled = true;
	$('submit').disabled = true;
	setStatus('idle', 'Отключено');
	$('key').focus();
};

$('connect').onsubmit = event => {
	event.preventDefault();
	connect($('key').value);
};

function clearAttachment() {
	pendingFiles = [];
	$('file-name').textContent = '';
	$('preview').removeAttribute('src');
	$('attachment').hidden = true;
}

$('remove-image').onclick = clearAttachment;
$('text').addEventListener('keydown', event => {
	if (event.key !== 'Enter' || event.isComposing || event.keyCode === 229) {
		return;
	}
	if (event.shiftKey) {
		return;
	}
	event.preventDefault();
	if (event.altKey) {
		const field = $('text');
		const length = field.value.length - (field.selectionEnd - field.selectionStart) + 1;
		if (length > field.maxLength) {
			return;
		}
		field.setRangeText('\n', field.selectionStart, field.selectionEnd, 'end');
		field.dispatchEvent(new Event('input', {bubbles: true}));
		return;
	}
	if (!event.repeat && !$('submit').disabled) {
		$('send').requestSubmit($('submit'));
	}
});
$('text').addEventListener('paste', event => {
	const files = event.clipboardData?.files;
	if (!running || !files?.length) {
		return;
	}
	event.preventDefault();
	attachFiles(files, true);
});

$('send').addEventListener('dragover', event => {
	if (!event.dataTransfer?.types.includes('Files')) {
		return;
	}
	event.preventDefault();
	event.dataTransfer.dropEffect = running && !sending && !readingFiles ? 'copy' : 'none';
});

$('send').addEventListener('drop', event => {
	const files = event.dataTransfer?.files;
	if (!files?.length) {
		return;
	}
	event.preventDefault();
	attachFiles(files, false);
});

async function attachFiles(files, fromClipboard) {
	if (!running || readingFiles || sending) {
		return;
	}
	$('send-error').hidden = true;
	const incoming = Array.from(files);
	if (pendingFiles.length + incoming.length > 200) {
		$('send-error').textContent = 'К сообщению можно прикрепить до 200 файлов.';
		$('send-error').hidden = false;
		return;
	}
	const total = [...pendingFiles, ...incoming].reduce((sum, file) => sum + file.size, 0);
	if (total > 50 * 1024 * 1024) {
		$('send-error').textContent = 'Общий размер файлов не должен превышать 50 МБ.';
		$('send-error').hidden = false;
		return;
	}
	readingFiles = true;
	$('submit').disabled = true;
	$('remove-image').disabled = true;
	try {
		const added = [];
		for (const file of incoming) {
			const dataUrl = await readFileDataUrl(file);
			added.push({
				name: file.name,
				size: file.size,
				data: dataUrl.slice(dataUrl.indexOf(',') + 1),
				inlineImage: fromClipboard && file.type === 'image/png' && file.size <= 4 * 1024 * 1024,
			});
		}
		pendingFiles.push(...added);
		const image = pendingScreenshot();
		$('preview').hidden = !image;
		if (image) {
			$('preview').src = image;
		} else {
			$('preview').removeAttribute('src');
		}
		$('file-name').textContent = image ? '' : pendingFiles.map(file => file.name).join(', ');
		$('attachment').hidden = false;
	} catch {
		$('send-error').textContent = 'Не удалось прочитать вложение. Попробуй ещё раз.';
		$('send-error').hidden = false;
	} finally {
		readingFiles = false;
		$('submit').disabled = false;
		$('remove-image').disabled = false;
	}
}

function readFileDataUrl(file) {
	return new Promise((resolve, reject) => {
		const reader = new FileReader();
		reader.onload = () => {
			resolve(reader.result);
		};
		reader.onerror = () => {
			reject(reader.error);
		};
		reader.onabort = () => {
			reject(new Error('Read aborted'));
		};
		reader.readAsDataURL(file);
	});
}

function pendingScreenshot() {
	if (pendingFiles.length === 1 && pendingFiles[0].inlineImage) {
		return 'data:image/png;base64,' + pendingFiles[0].data;
	}
	return '';
}

$('send').onsubmit = async event => {
	event.preventDefault();
	if (!running || sending || readingFiles || (!$('text').value.trim() && !pendingFiles.length)) {
		return;
	}

	$('submit').disabled = true;
	sending = true;
	$('disconnect').disabled = true;
	$('submit').textContent = '⏳ Отправка…';
	$('remove-image').disabled = true;
	$('send-error').hidden = true;
	const draft = $('text').value;
	try {
		const image = pendingScreenshot();
		let type = '';
		let body = draft;
		if (image) {
			type = 'image';
			body = image + '\n' + draft;
		} else if (pendingFiles.length) {
			type = pendingFiles.length === 1 ? 'file' : 'files';
			const parts = pendingFiles.map(file => encodeURIComponent(file.name) + '\n' + file.data + '\n');
			body = parts.join('') + (type === 'files' ? '\n' : '') + draft;
		}
		await request('/messages', {
			method: 'POST',
			body,
			headers: type ? {'X-Chat-Type': type} : {},
			signal: pendingFiles.length ? AbortSignal.timeout(120000) : undefined,
		});
		if ($('text').value === draft) {
			$('text').value = '';
		}
		clearAttachment();
	} catch (error) {
		$('send-error').textContent = error.message === 'Delivery queue is full.'
			? 'Очередь заполнена. Дождись подключения получателя и попробуй снова.'
			: 'Не удалось отправить. Попробуй ещё раз.';
		$('send-error').hidden = false;
	} finally {
		sending = false;
		$('disconnect').disabled = false;
		$('submit').textContent = 'Отправить';
		$('remove-image').disabled = false;
		$('submit').disabled = false;
	}
};

if (location.protocol === 'file:') {
	$('connect').hidden = true;
	setStatus('idle', 'Предпросмотр · без сервера');
	appendMessage({id: 1, sender: 'local', text: 'Привет! Как выглядит чат?'});
	appendMessage({id: 2, sender: 'sedo', text: 'Тёмная тема на месте. Текст читается хорошо.'});
	$('empty').hidden = true;
} else {
	const savedToken = restoreSession();
	if (savedToken) {
		connect(savedToken);
	}
}