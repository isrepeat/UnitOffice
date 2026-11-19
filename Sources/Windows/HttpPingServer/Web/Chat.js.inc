R"WEB(
let cursor = 0;
let running = false;
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

		if (cursor && data.messages.length && data.messages[0].id > cursor + 1) {
			$('status').textContent = 'Часть истории уже удалена.';
		}

		if (data.latest < cursor) {
			cursor = 0;
			$('messages').replaceChildren();
			return;
		}

		for (const message of data.messages) {
			const item = document.createElement('pre');
			const sender = message.sender === 'sedo' ? 'СЕДО' : 'Локальный';
			item.textContent = '#' + message.id + ' ' + sender + '\n' + message.text;
			$('messages').append(item);
			cursor = message.id;

			while ($('messages').children.length > 200) {
				$('messages').firstChild.remove();
			}
		}

		$('status').textContent = 'Подключено. История хранится до перезапуска сервера.';
	} catch (error) {
		$('status').textContent = 'Нет связи: ' + error.message;
	}
}

$('connect').onsubmit = async event => {
	event.preventDefault();
	if (running) {
		return;
	}

	token = $('key').value;
	role = fixedRole ?? $('sender').value;

	try {
		await request('/messages?after=0');
	} catch (error) {
		$('status').textContent = 'Подключение не удалось: ' + error.message;
		return;
	}

	running = true;
	$('key').value = '';
	$('submit').disabled = false;
	for (const control of $('connect').elements) {
		control.disabled = true;
	}

	async function loop() {
		await poll();
		setTimeout(loop, 1000);
	}
	loop();
};

$('send').onsubmit = async event => {
	event.preventDefault();
	if (!running) {
		return;
	}

	$('submit').disabled = true;
	try {
		await request('/messages', {
			method: 'POST',
			body: $('text').value,
		});
		$('text').value = '';
	} catch (error) {
		$('status').textContent = 'Сообщение не отправлено: ' + error.message;
	} finally {
		$('submit').disabled = false;
	}
};
)WEB"