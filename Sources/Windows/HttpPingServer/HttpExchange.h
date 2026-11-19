#pragma once
#include <Windows.h>
#include <http.h>

#include <string_view>
#include <string>

namespace unitoffice::windows::httppingserver {
	namespace details {
		inline std::string_view Header(const HTTP_REQUEST& request, std::string_view name) {
			for (USHORT index = 0; index < request.Headers.UnknownHeaderCount; ++index) {
				const auto& header = request.Headers.pUnknownHeaders[index];
				if (header.NameLength == name.size() && ::_strnicmp(header.pName, name.data(), name.size()) == 0) {
					return {header.pRawValue, header.RawValueLength};
				}
			}
			return {};
		}

		inline USHORT ReadMessage(HANDLE requestQueue, HTTP_REQUEST_ID requestId, std::string& body, std::size_t limit = 16000) {
			// Читаем тело частями и ограничиваем размер до выделения истории.
			char buffer[4096];
			for (;;) {
				ULONG received = 0;
				const auto status = ::HttpReceiveRequestEntityBody(
					requestQueue,
					requestId,
					0,
					buffer,
					sizeof(buffer),
					&received,
					nullptr
				);
				if (body.size() + received > limit) {
					return 413;
				}
				body.append(buffer, received);
				if (status == ERROR_HANDLE_EOF) break;
				if (status != NO_ERROR) {
					return 400;
				}
			}
			if (body.empty() || body.find_first_not_of(" \r\n\t") == std::string::npos) {
				return 400;
			}
			if (::MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, body.data(), static_cast<int>(body.size()), nullptr, 0) == 0) {
				return 400;
			}
			return 200;
		}
	} // namespace details

	struct HttpResponse {
		USHORT statusCode;
		std::string body;
		const char* contentType = "text/plain; charset=utf-8";
		bool allowAnyOrigin = false;

		static HttpResponse Error(USHORT statusCode) {
			return {statusCode, "Request rejected"};
		}
	};
}