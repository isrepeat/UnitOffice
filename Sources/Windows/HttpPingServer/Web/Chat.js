let cursor = 0;
let running = false;
let connecting = false;
let token = '';
let role = 'local';
let pendingImage = '';
let readingImage = false;
let sending = false;

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
			// Старый способ копирования нужен для чата по HTTP.
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

function makeCopyButton(value) {
	const button = document.createElement('button');
	button.type = 'button';
	button.className = 'copy-message';
	const label = 'Копировать сообщение';
	button.title = label;
	button.setAttribute('aria-label', label);
	let reset;
	button.addEventListener('click', async () => {
		try {
			await copyText(value);
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

function makeImageActions(image, id) {
	const actions = document.createElement('div');
	actions.className = 'image-actions';
	const download = document.createElement('a');
	download.href = image.src;
	download.download = 'unitoffice-screenshot-' + id + '.png';
	download.className = 'image-action image-action--download';
	download.title = 'Скачать изображение';
	download.setAttribute('aria-label', download.title);
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

function appendMessage(message) {
	const item = document.createElement('article');
	item.className = 'message';
	item.classList.toggle('message--own', message.sender === role);

	const meta = document.createElement('p');
	meta.className = 'message-meta';
	const sender = message.sender === 'sedo' ? 'СЕДО' : 'Локальный';
	meta.textContent = sender + ' · #' + message.id;

	const text = document.createElement('p');
	text.className = 'message-text';
	text.textContent = message.text;
	item.append(meta, text);
	if (message.text) item.append(makeCopyButton(message.text));
	if (typeof message.image === 'string' && /^data:image\/png;base64,[A-Za-z0-9+/=]+$/.test(message.image)) {
		const image = document.createElement('img');
		image.className = 'message-image';
		image.alt = 'Скриншот';
		image.src = message.image;
		item.append(image, makeImageActions(image, message.id));
	}
	$('messages').append(item);
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
		signal: AbortSignal.timeout(10000),
	});

	if (!response.ok) {
		throw new Error('HTTP ' + response.status);
	}

	return response;
}

async function poll() {
	try {
		const response = await request('/messages?after=' + cursor);
		const data = await response.json();

		const history = $('messages').parentElement;
		const followLatest = history.scrollHeight - history.scrollTop - history.clientHeight < 48;
		const historyGap = cursor > 0 && data.messages.length > 0 && data.messages[0].id > cursor + 1;

		if (data.latest < cursor) {
			cursor = 0;
			$('messages').replaceChildren();
			$('empty').hidden = false;
			setStatus('online', 'Подключено · история сброшена');
			return;
		}

		for (const message of data.messages) {
			appendMessage(message);
			cursor = message.id;

			while ($('messages').children.length > 200) {
				$('messages').firstChild.remove();
			}
		}

		$('empty').hidden = $('messages').children.length > 0;
		if (followLatest && data.messages.length > 0) {
			history.scrollTop = history.scrollHeight;
		}

		setStatus('online', historyGap ? 'Подключено · часть истории недоступна' : 'Подключено');
	} catch {
		setStatus('error', 'Нет связи · повторяем подключение');
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
		// Если хранилище недоступно, остаётся вход вручную.
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
		// Вход работает и без браузерного хранилища.
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
	$('text').disabled = false;
	$('submit').disabled = false;
	$('text').focus();
	setStatus('online', 'Подключено');

	async function loop() {
		await poll();
		setTimeout(loop, 1000);
	}
	loop();
}

$('connect').onsubmit = event => {
	event.preventDefault();
	connect($('key').value);
};

function clearImage() {
	pendingImage = '';
	$('preview').removeAttribute('src');
	$('attachment').hidden = true;
}

$('remove-image').onclick = clearImage;
$('text').addEventListener('paste', async event => {
	const items = Array.from(event.clipboardData?.items ?? []);
	const item = items.find(value => value.type.startsWith('image/'));
	if (!item || !running) return;
	event.preventDefault();
	if (readingImage || sending) return;
	const file = item.getAsFile();
	$('send-error').hidden = true;
	if (!file || file.type !== 'image/png' || file.size > 4 * 1024 * 1024) {
		$('send-error').textContent = 'Вставь PNG-изображение размером до 4 МБ.';
		$('send-error').hidden = false;
		return;
	}
	readingImage = true;
	$('submit').disabled = true;
	$('remove-image').disabled = true;
	try {
		const image = await new Promise((resolve, reject) => {
			const reader = new FileReader();
			reader.onload = () => resolve(reader.result);
			reader.onerror = () => reject(reader.error);
			reader.onabort = () => reject(new Error('Read aborted'));
			reader.readAsDataURL(file);
		});
		pendingImage = image;
		$('preview').src = image;
		$('attachment').hidden = false;
	} catch {
		$('send-error').textContent = 'Не удалось прочитать изображение. Вставь его ещё раз.';
		$('send-error').hidden = false;
	} finally {
		readingImage = false;
		$('submit').disabled = false;
		$('remove-image').disabled = false;
	}
});

$('send').onsubmit = async event => {
	event.preventDefault();
	if (!running || sending || readingImage || (!$('text').value.trim() && !pendingImage)) {
		return;
	}

	$('submit').disabled = true;
	sending = true;
	$('remove-image').disabled = true;
	$('send-error').hidden = true;
	const draft = $('text').value;
	const image = pendingImage;
	try {
		await request('/messages', {
			method: 'POST',
			body: image ? image + '\n' + draft : draft,
			headers: image ? {'X-Chat-Type': 'image'} : {},
		});
		if ($('text').value === draft) {
			$('text').value = '';
		}
		if (image === pendingImage) clearImage();
	} catch {
		$('send-error').textContent = 'Не удалось отправить. Попробуй ещё раз.';
		$('send-error').hidden = false;
	} finally {
		sending = false;
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