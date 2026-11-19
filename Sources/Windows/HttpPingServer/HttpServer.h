#pragma once
#include "Router.h"

#include <system_error>
#include <cstdio>
#include <cwchar>
#include <memory>

#pragma comment(lib, "Httpapi.lib")

namespace unitoffice::windows::httppingserver::details {

	class HttpServer {
	public:
		static void Run(const std::wstring& url, Router& router) {
			HttpServer httpServer;
			Check(::HttpCreateHttpHandle(&httpServer.requestQueue, 0), "HttpCreateHttpHandle");
			Check(::HttpAddUrl(httpServer.requestQueue, url.c_str(), nullptr), "HttpAddUrl");
			httpServer.registeredUrl = url.c_str();
			std::wprintf(L"Listening on %ls\n", url.c_str());
			Serve(httpServer.requestQueue, router);
		}

	private:
		HttpServer() {
			Check(::HttpInitialize(HTTPAPI_VERSION_1, HTTP_INITIALIZE_SERVER, nullptr), "HttpInitialize");
		}

		~HttpServer() {
			if (registeredUrl) {
				::HttpRemoveUrl(requestQueue, registeredUrl);
			}
			if (requestQueue) {
				::CloseHandle(requestQueue);
			}
			::HttpTerminate(HTTP_INITIALIZE_SERVER, nullptr);
		}

		static void Check(ULONG status, const char* operation) {
			if (status != NO_ERROR) {
				const auto context = std::string(operation) + " (Win32 error " + std::to_string(status) + ")";
				throw std::system_error(static_cast<int>(status), std::system_category(), context);
			}
		}

		HANDLE requestQueue = nullptr;
		const wchar_t* registeredUrl = nullptr;

		static void Serve(HANDLE requestQueue, Router& router) {
			constexpr ULONG requestBufferSize = 32768;
			const auto requestBuffer = std::make_unique<unsigned char[]>(requestBufferSize);
			auto* request = reinterpret_cast<HTTP_REQUEST*>(requestBuffer.get());

			for (;;) {
				// Получаем следующий HTTP-запрос.
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
				Check(receiveStatus, "HttpReceiveHttpRequest");
				auto [statusCode, body, contentType] = router.Route(requestQueue, *request);
				const bool sendBody = request->Verb != HttpVerbHEAD;
				HTTP_DATA_CHUNK chunk{};
				chunk.DataChunkType = HttpDataChunkFromMemory;
				chunk.FromMemory.pBuffer = body.data();
				chunk.FromMemory.BufferLength = static_cast<ULONG>(body.size());

				HTTP_RESPONSE response{};
				response.StatusCode = statusCode;
				response.pReason = statusCode == 200 ? "OK" : "Error";
				response.ReasonLength = statusCode == 200 ? 2 : 5;
				response.EntityChunkCount = sendBody ? 1 : 0;
				response.pEntityChunks = sendBody ? &chunk : nullptr;
				response.Headers.KnownHeaders[HttpHeaderContentType] = {static_cast<USHORT>(std::char_traits<char>::length(contentType)), contentType};
				response.Headers.KnownHeaders[HttpHeaderCacheControl] = {8, "no-store"};

				// Отправляем ответ и закрываем соединение, включая непрочитанное тело.
				const ULONG sendStatus = ::HttpSendHttpResponse(
					requestQueue,
					request->RequestId,
					HTTP_SEND_RESPONSE_FLAG_DISCONNECT,
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
					std::fprintf(stderr, "%s -> %u\n", method, static_cast<unsigned>(response.StatusCode));
				}
			}
		}
	};
}