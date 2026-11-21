'use strict';

// Service worker: performs every cross-origin request to the local HttpPingServer.
// host_permissions grant a CORS exemption here, so requests succeed even when the
// SEDO page tightens its own connect-src or CORS policy.

const config = {
	server: 'https://192.168.202.46:8443',
	// Optional visible heartbeat posted into the chat page. Fill accessKey with the
	// value passed to UnitOffice.AscodInteropServer.exe to enable it; leave empty to skip.
	chat: {
		accessKey: '',
		sender: 'sedo',
	},
};

async function ping(origin) {
	try {
		const response = await fetch(config.server + '/ping', {
			method: 'POST',
			body: 'unitoffice-sedo-extension:' + origin,
			cache: 'no-store',
			signal: AbortSignal.timeout(8000),
		});
		const pong = (await response.text()).trim() === 'pong';
		const state = response.status === 200 && pong ? 'confirmed' : 'unexpected-response';
		console.log('[SEDO bridge] ping', state, '| status', response.status, '| origin', origin);
		return state;
	} catch (error) {
		console.warn('[SEDO bridge] ping failed:', error.name, '- is the server running and its certificate trusted on this machine?');
		return 'unconfirmed';
	}
}

async function chatHeartbeat(text) {
	if (!config.chat.accessKey) {
		return;
	}
	try {
		const response = await fetch(config.server + '/messages', {
			method: 'POST',
			headers: {
				'X-Chat-Key': config.chat.accessKey,
				'X-Chat-Sender': config.chat.sender,
			},
			body: text,
			cache: 'no-store',
			signal: AbortSignal.timeout(8000),
		});
		console.log('[SEDO bridge] chat heartbeat | status', response.status);
	} catch (error) {
		console.warn('[SEDO bridge] chat heartbeat failed:', error.name);
	}
}

chrome.runtime.onMessage.addListener((message, sender) => {
	const origin = message?.pageOrigin ?? sender?.origin ?? 'unknown';
	if (message?.kind === 'probe-hello') {
		console.log('[SEDO bridge] content script active on', origin, '| secure context:', message.secureContext);
		ping(origin).then(state => {
			if (state === 'confirmed') {
				chatHeartbeat('SEDO diagnostics bridge connected from ' + origin);
			}
		});
	} else if (message?.kind === 'probe-report') {
		console.log('[SEDO bridge] report from', origin, '|', message.endpoints.length, 'endpoints');
		console.table(message.endpoints);
	}
});