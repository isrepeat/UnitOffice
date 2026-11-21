(() => {
	'use strict';

	// This runs in the page before disable-devtool initializes and never opens
	// DevTools, so the SEDO DevtoolOpened handler is not triggered. All traffic to
	// the local server is relayed through the service worker, which carries the
	// host_permissions CORS exemption.

	const endpoints = new Map();
	let observer;
	let reportTimer;

	function relay(message) {
		try {
			chrome.runtime.sendMessage(message);
		} catch (error) {
			// The service worker may be asleep or the context invalidated on reload.
		}
	}

	function record(entry) {
		if (!['fetch', 'xmlhttprequest', 'beacon', 'eventsource'].includes(entry.initiatorType)) {
			return;
		}
		try {
			const url = new URL(entry.name, location.href);
			if (!['http:', 'https:'].includes(url.protocol)) {
				return;
			}
			const parameters = [...new Set(url.searchParams.keys())].sort();
			const path = url.pathname
				.replace(/\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b/gi, ':id')
				.replace(/\/\d+(?=\/|$)/g, '/:id');
			const key = JSON.stringify([entry.initiatorType, url.origin, path, parameters]);
			const previous = endpoints.get(key);
			if (!previous && endpoints.size >= 500) {
				endpoints.delete(endpoints.keys().next().value);
			}
			endpoints.set(key, {
				type: entry.initiatorType,
				origin: url.origin,
				path,
				queryKeys: parameters.join(', '),
				count: (previous?.count ?? 0) + 1,
				status: entry.responseStatus || 'unknown',
			});
		} catch {
			// Skip malformed entries so monitoring can continue.
		}
	}

	function sendReport() {
		relay({
			kind: 'probe-report',
			pageOrigin: location.origin,
			secureContext: window.isSecureContext,
			endpoints: Array.from(endpoints.values()),
		});
	}

	try {
		performance.getEntriesByType('resource').forEach(record);
		observer = new PerformanceObserver(list => list.getEntries().forEach(record));
		observer.observe({type: 'resource'});
	} catch {
		// Only the snapshot of already completed requests is available.
	}

	relay({
		kind: 'probe-hello',
		pageOrigin: location.origin,
		secureContext: window.isSecureContext,
		userAgent: navigator.userAgent,
	});

	reportTimer = setInterval(sendReport, 15000);
	window.addEventListener('pagehide', () => {
		clearInterval(reportTimer);
		if (observer) {
			observer.disconnect();
		}
		sendReport();
	});
})();