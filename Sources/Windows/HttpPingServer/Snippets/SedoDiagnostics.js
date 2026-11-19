(() => {
	'use strict';

	const server = 'https://192.168.202.46:8443';
	window.sedoProbe?.stop?.();
	const endpoints = new Map();
	let connection = {state: 'not-tested'};
	let observer;
	let timer;
	let stopped = false;

	function record(entry) {
		if (!['fetch', 'xmlhttprequest', 'beacon', 'eventsource'].includes(entry.initiatorType)) return;
		try {
			const url = new URL(entry.name, location.href);
			if (!['http:', 'https:'].includes(url.protocol) || url.origin === server) return;
			const parameters = [...new Set(url.searchParams.keys())].sort();
			const path = url.pathname.replace(/\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b/gi, ':id')
				.replace(/\/\d+(?=\/|$)/g, '/:id');
			const key = JSON.stringify([entry.initiatorType, url.origin, path, parameters]);
			const previous = endpoints.get(key);
			const row = {
				type: entry.initiatorType,
				origin: url.origin,
				path,
				queryKeys: parameters.join(', '),
				count: (previous?.count ?? 0) + 1,
				status: entry.responseStatus || 'unknown',
			};
			if (!previous && endpoints.size >= 500) endpoints.delete(endpoints.keys().next().value);
			endpoints.set(key, row);
			if (!previous) console.log('[SEDO API candidate]', row);
		} catch {
			// Некорректная запись не должна прерывать наблюдение.
		}
	}

	function report() {
		const result = {
			pageOrigin: location.origin,
			secureContext: window.isSecureContext,
			server,
			connection: {...connection},
			observing: !stopped,
			endpoints: Array.from(endpoints.values(), row => ({...row})),
		};
		console.table(result.endpoints);
		console.log('[SEDO diagnostics]', result);
		return result;
	}

	async function ping() {
		connection = {state: 'testing'};
		try {
			const response = await fetch(server + '/ping', {
				method: 'POST',
				mode: 'cors',
				credentials: 'omit',
				referrerPolicy: 'no-referrer',
				body: 'unitoffice-sedo-connectivity-test',
				signal: AbortSignal.timeout(8000),
			});
			const pong = (await response.text()).trim() === 'pong';
			connection = {state: response.status === 200 && pong ? 'confirmed' : 'unexpected-response', status: response.status};
			console.log('[SEDO HTTPS probe]', connection);
		} catch (error) {
			connection = {state: 'unconfirmed', error: error.name};
			console.warn('[SEDO HTTPS probe] Ответ недоступен. Проверь Network, Console и журнал сервера. CORS-ошибка не доказывает отсутствие доставки запроса.', error.name);
		}
		return {...connection};
	}

	function stop() {
		if (stopped) return;
		if (observer) {
			observer.takeRecords().forEach(record);
			observer.disconnect();
		}
		clearTimeout(timer);
		stopped = true;
		console.log('[SEDO diagnostics] Наблюдение остановлено; отчёт доступен через sedoProbe.report().');
	}

	performance.getEntriesByType('resource').forEach(record);
	try {
		observer = new PerformanceObserver(list => list.getEntries().forEach(record));
		observer.observe({type: 'resource'});
	} catch {
		console.warn('[SEDO diagnostics] Доступен только снимок уже выполненных запросов.');
		stopped = true;
	}
	window.sedoProbe = {report, ping, stop};
	if (!stopped) timer = setTimeout(stop, 5 * 60 * 1000);
	console.log('[SEDO diagnostics] Origin:', location.origin, '| secure context:', window.isSecureContext);
	console.log('Выполни обычные действия в СЕДО, затем sedoProbe.report(). Остановка: sedoProbe.stop(). Автоостановка через 5 минут.');
	report();
	void ping();
})();