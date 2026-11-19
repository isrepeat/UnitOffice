#pragma once
#include <UniversalDevKit/Diagnostic/Logging.h>

#include "HttpExchange.h"

#include <system_error>
#include <memory>

#pragma comment(lib, "Httpapi.lib")

namespace unitoffice::windows::httphosting {
	class HttpServer {
	public:
		template<typename Route>
		static void Run(const std::wstring& url, Route& route) {
			HttpServer httpServer;
			HttpServer::Check(::HttpCreateHttpHandle(&httpServer.requestQueue, 0), "HttpCreateHttpHandle");
			HttpServer::Check(::HttpAddUrl(httpServer.requestQueue, url.c_str(), nullptr), "HttpAddUrl");
			httpServer.registeredUrl = url.c_str();
			UDK_LOG_INFO("HttpHosting", L"Listening on {}", url);
			HttpServer::Serve(httpServer.requestQueue, route);
		}

	private:
		HttpServer() {
			HttpServer::Check(::HttpInitialize(HTTPAPI_VERSION_1, HTTP_INITIALIZE_SERVER, nullptr), "HttpInitialize");
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

		template<typename Route>
		static void Serve(HANDLE requestQueue, Route& route) {
			constexpr ULONG requestBufferSize = 32768;
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
				HttpServer::Check(receiveStatus, "HttpReceiveHttpRequest");
				auto [statusCode, body, contentType, allowAnyOrigin] = route(requestQueue, *request);
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
				constexpr std::string_view cacheControl = "no-store, no-cache, max-age=0, must-revalidate";
				response.Headers.KnownHeaders[HttpHeaderCacheControl] = {static_cast<USHORT>(cacheControl.size()), cacheControl.data()};
				constexpr char corsHeaderName[] = "Access-Control-Allow-Origin";
				HTTP_UNKNOWN_HEADER corsHeader{sizeof(corsHeaderName) - 1, 1, corsHeaderName, "*"};
				if (allowAnyOrigin) {
					response.Headers.UnknownHeaderCount = 1;
					response.Headers.pUnknownHeaders = &corsHeader;
				}

				// Send the response and close the connection even if the request body has unread data.
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
					UDK_LOG_ERROR("HttpHosting", "HttpSendHttpResponse failed (Win32 error {})", sendStatus);
				} else {
					const char* method = request->Verb == HttpVerbGET ? "GET" :
						request->Verb == HttpVerbHEAD ? "HEAD" : request->Verb == HttpVerbPOST ? "POST" : "OTHER";
					UDK_LOG_INFO("HttpHosting", "{} -> {}", method, response.StatusCode);
				}
			}
		}
	};
}