#pragma once
#include "HttpExchange.h"
#include "ChatService.h"

#include <filesystem>
#include <stdexcept>
#include <charconv>
#include <iterator>
#include <optional>
#include <fstream>

namespace unitoffice::windows::httppingserver {

	class Router {
	public:
		explicit Router(std::string_view accessKey)
			: accessKey(accessKey)
			, webDirectory(Router::GetWebDirectory()) {
		}

		HttpResponse Route(HANDLE requestQueue, const HTTP_REQUEST& request) {
			const auto& url = request.CookedUrl;
			const std::wstring_view path(url.pAbsPath, url.AbsPathLength / sizeof(wchar_t));
			if (path == L"/messages") {
				return this->RouteMessages(requestQueue, request);
			}
			if (path == L"/ping" && request.Verb == HttpVerbPOST) {
				return {200, "pong\n"};
			}
			if (request.Verb != HttpVerbGET && request.Verb != HttpVerbHEAD) {
				return HttpResponse::Error(404);
			}

			for (const auto& asset : assets) {
				if (asset.path == path) {
					std::ifstream file(this->webDirectory / asset.fileName, std::ios::binary);
					if (!file) {
						return HttpResponse::Error(500);
					}
					std::string body(std::istreambuf_iterator<char>{file}, std::istreambuf_iterator<char>{});
					return {200, std::move(body), asset.contentType};
				}
			}
			return HttpResponse::Error(404);
		}

	private:
		struct Asset {
			std::wstring_view path;
			const wchar_t* fileName;
			const char* contentType;
		};

		static constexpr Asset assets[] = {
			{L"/chat", L"Chat.html", "text/html; charset=utf-8"},
			{L"/local", L"Chat.html", "text/html; charset=utf-8"},
			{L"/sedo", L"Chat.html", "text/html; charset=utf-8"},
			{L"/test", L"Diagnostics.html", "text/html; charset=utf-8"},
			{L"/Chat.css", L"Chat.css", "text/css; charset=utf-8"},
			{L"/Chat.js", L"Chat.js", "text/javascript; charset=utf-8"},
			{L"/web/chat.css", L"Chat.css", "text/css; charset=utf-8"},
			{L"/web/chat.js", L"Chat.js", "text/javascript; charset=utf-8"},
		};

		static std::filesystem::path GetWebDirectory() {
			std::wstring executable(32768, L'\0');
			const auto length = ::GetModuleFileNameW(nullptr, executable.data(), static_cast<DWORD>(executable.size()));
			if (length == 0 || length >= executable.size()) {
				throw std::runtime_error("Could not find the HttpPingServer executable directory.");
			}
			executable.resize(length);
			return std::filesystem::path(executable).parent_path() / L"Web";
		}

		static std::optional<std::uint64_t> ParseCursor(const HTTP_COOKED_URL& url) {
			if (url.QueryStringLength == 0) {
				return 0;
			}
			std::wstring_view query(url.pQueryString, url.QueryStringLength / sizeof(wchar_t));
			if (!query.starts_with(L"?after=") || query.size() == 7) {
				return std::nullopt;
			}

			std::string digits;
			for (const wchar_t character : query.substr(7)) {
				if (character < L'0' || character > L'9') {
					return std::nullopt;
				}
				digits += static_cast<char>(character);
			}
			std::uint64_t after = 0;
			const auto parsed = std::from_chars(digits.data(), digits.data() + digits.size(), after);
			if (parsed.ec != std::errc{} || parsed.ptr != digits.data() + digits.size()) {
				return std::nullopt;
			}
			return after;
		}

		HttpResponse RouteMessages(HANDLE requestQueue, const HTTP_REQUEST& request) {
			if (details::Header(request, "X-Chat-Key") != accessKey) {
				return HttpResponse::Error(401);
			}
			if (request.Verb == HttpVerbGET) {
				const auto after = Router::ParseCursor(request.CookedUrl);
				if (!after) {
					return HttpResponse::Error(400);
				}
				return {200, this->chatService.ReadAfter(*after), "application/json; charset=utf-8"};
			}
			if (request.Verb != HttpVerbPOST) {
				return HttpResponse::Error(405);
			}
			const auto sender = details::Header(request, "X-Chat-Sender");
			if (sender != "local" && sender != "sedo") {
				return HttpResponse::Error(400);
			}
			std::string text;
			const auto status = details::ReadMessage(requestQueue, request.RequestId, text);
			if (status != 200) {
				return HttpResponse::Error(status);
			}
			this->chatService.Add(sender, std::move(text));
			return {200, "accepted"};
		}

		std::string accessKey;
		std::filesystem::path webDirectory;
		ChatService chatService;
	};
}