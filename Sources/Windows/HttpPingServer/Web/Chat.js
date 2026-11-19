let cursor = 0;
let running = false;
let connecting = false;
let token = '';
let role = 'local';

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
	item.append(meta, text, makeCopyButton(message.text));
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
		headers: headers(),
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

$('send').onsubmit = async event => {
	event.preventDefault();
	if (!running) {
		return;
	}

	$('submit').disabled = true;
	$('send-error').hidden = true;
	const draft = $('text').value;
	try {
		await request('/messages', {
			method: 'POST',
			body: draft,
		});
		if ($('text').value === draft) {
			$('text').value = '';
		}
	} catch {
		$('send-error').textContent = 'Не удалось отправить. Попробуй ещё раз.';
		$('send-error').hidden = false;
	} finally {
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