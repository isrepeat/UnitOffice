#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <http.h>

#include <string_view>
#include <cwchar>
#include <memory>
#include <cstdio>
#include <string>

#pragma comment(lib, "Httpapi.lib")

namespace unitoffice::windows::httppingserver::details {
	char diagnosticPage[] = R"HTML(<!doctype html>
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
)HTML";

	ULONG Serve(HANDLE requestQueue) {
		constexpr auto requestBufferSize = 32768;
		const auto requestBuffer = std::make_unique<unsigned char[]>(requestBufferSize);
		auto* request = reinterpret_cast<HTTP_REQUEST*>(requestBuffer.get());

		for (;;) {
			// Receive the next HTTP request.
			ULONG bytesReceived = 0;
			const ULONG receiveStatus = ::HttpReceiveHttpRequest(
				requestQueue,
				HTTP_NULL_ID,
				0,
				request,
				requestBufferSize,
				&bytesReceived,
				nullptr
			);
			if (receiveStatus != NO_ERROR) {
				return receiveStatus;
			}

			// Build the response for the requested route.
			const auto& url = request->CookedUrl;
			const std::wstring_view path(url.pAbsPath, url.AbsPathLength / sizeof(wchar_t));
			const bool isPing = request->Verb == HttpVerbPOST && path == L"/ping";
			const bool isTest = (request->Verb == HttpVerbGET || request->Verb == HttpVerbHEAD) && path == L"/test";
			const bool found = isPing || isTest;
			const bool sendBody = found && request->Verb != HttpVerbHEAD;

			char pong[] = "pong\n";
			HTTP_DATA_CHUNK chunk{};
			chunk.DataChunkType = HttpDataChunkFromMemory;
			chunk.FromMemory.pBuffer = isTest ? diagnosticPage : pong;
			chunk.FromMemory.BufferLength = isTest ? sizeof(diagnosticPage) - 1 : sizeof(pong) - 1;

			HTTP_RESPONSE response{};
			response.StatusCode = found ? 200 : 404;
			response.pReason = found ? "OK" : "Not Found";
			response.ReasonLength = found ? 2 : 9;
			response.EntityChunkCount = sendBody ? 1 : 0;
			response.pEntityChunks = sendBody ? &chunk : nullptr;
			response.Headers.KnownHeaders[HttpHeaderContentType] = isTest
				? HTTP_KNOWN_HEADER{9, "text/html"}
				: HTTP_KNOWN_HEADER{10, "text/plain"};

			// Send the response to the client.
			const ULONG sendStatus = ::HttpSendHttpResponse(
				requestQueue,
				request->RequestId,
				0,
				&response,
				nullptr,
				nullptr,
				nullptr,
				0,
				nullptr,
				nullptr
			);
			if (sendStatus != NO_ERROR) {
				std::fprintf(stderr, "HttpSendHttpResponse: %lu\n", sendStatus);
			} else {
				const char* method = request->Verb == HttpVerbGET ? "GET" :
					request->Verb == HttpVerbHEAD ? "HEAD" : request->Verb == HttpVerbPOST ? "POST" : "OTHER";
				std::fprintf(stderr, "%s %ls -> %u\n", method, isTest ? L"/test" : isPing ? L"/ping" : L"(unknown)", static_cast<unsigned>(response.StatusCode));
			}
		}
	}
}

int wmain(int argc, wchar_t* argv[]) {
#ifdef _DEBUG
	constexpr std::wstring_view defaultAddress = L"192.168.202.46";
#else
	constexpr std::wstring_view defaultAddress = L"127.0.0.1";
#endif
	const std::wstring_view address = argc == 2 ? argv[1] : defaultAddress;
	if (argc > 2 || address.empty() || address.find_first_not_of(L"0123456789.") != std::wstring_view::npos) {
		std::fprintf(stderr, "Usage: UnitOffice.HttpPingServer.exe [IPv4-address]\n");
		return 1;
	}

	const std::wstring url = L"http://" + std::wstring(address) + L":8080/";
	ULONG status = ::HttpInitialize(HTTPAPI_VERSION_1, HTTP_INITIALIZE_SERVER, nullptr);
	if (status != NO_ERROR) {
		std::fprintf(stderr, "HttpInitialize: %lu\n", status);
		return 1;
	}

	HANDLE queue = nullptr;
	status = ::HttpCreateHttpHandle(&queue, 0);
	if (status == NO_ERROR) {
		status = ::HttpAddUrl(queue, url.c_str(), nullptr);
		if (status == NO_ERROR) {
			std::wprintf(L"Listening on %ls\n", url.c_str());
			status = unitoffice::windows::httppingserver::details::Serve(queue);
			::HttpRemoveUrl(queue, url.c_str());
		}
		::CloseHandle(queue);
	}

	system("pause");

	::HttpTerminate(HTTP_INITIALIZE_SERVER, nullptr);
	std::fprintf(stderr, "Server stopped with Win32 error %lu\n", status);

	system("pause");

	return 1;
}