R"WEB(<!doctype html>
<meta charset="utf-8">
<title>HttpPingServer diagnostics</title>
<h1>HttpPingServer diagnostics</h1>
<pre id="results">GET /test: page loaded
</pre>
<script>
const results = document.getElementById('results');
async function run() {
	for (const [method, path, body, expected] of [
		['GET', '/test', undefined, 200],
		['HEAD', '/test', undefined, 200],
		['GET', '/missing', undefined, 404],
		['POST', '/ping', '', 200],
		['POST', '/ping', 'diagnostic-ping', 200],
	]) {
		try {
			const response = await fetch(path, {method, body, cache: 'no-store', signal: AbortSignal.timeout(5000)});
			const valid = response.status === expected &&
				(method !== 'POST' || (await response.text()).trim() === 'pong');
			results.textContent += method + ' ' + path + (body ? ' (with body)' : '') +
				': ' + response.status + (valid ? ' PASS' : ' FAIL') + '\n';
		} catch (error) {
			results.textContent += method + ' ' + path + ': ' + error + '\n';
		}
	}
}
run();
</script>
)WEB"