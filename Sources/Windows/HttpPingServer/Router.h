#pragma once
#include "HttpExchange.h"
#include "ChatService.h"

#include <charconv>
#include <optional>

namespace unitoffice::windows::httppingserver::details {

	class Router {
	public:
		explicit Router(std::string_view accessKey) : accessKey(accessKey) {}

		HttpResponse Route(HANDLE requestQueue, const HTTP_REQUEST& request) {
			const auto& url = request.CookedUrl;
			const std::wstring_view path(url.pAbsPath, url.AbsPathLength / sizeof(wchar_t));
			if (path == L"/messages") return RouteMessages(requestQueue, request);
			if (path == L"/ping" && request.Verb == HttpVerbPOST) return {200, "pong\n"};
			if (request.Verb != HttpVerbGET && request.Verb != HttpVerbHEAD) return HttpResponse::Error(404);

			for (const auto& asset : assets) {
				if (asset.path == path) return {200, asset.body, asset.contentType};
			}
			return HttpResponse::Error(404);
		}

	private:
		struct Asset {
			std::wstring_view path;
			const char* body;
			const char* contentType;
		};

		static constexpr char chatPage[] =
#include "Web/Chat.html.inc"
		;
		static constexpr char chatCss[] =
#include "Web/Chat.css.inc"
		;
		static constexpr char chatScript[] =
#include "Web/Chat.js.inc"
		;
		static constexpr char diagnosticsPage[] =
#include "Web/Diagnostics.html.inc"
		;

		static constexpr Asset assets[] = {
			{L"/chat", chatPage, "text/html; charset=utf-8"},
			{L"/local", chatPage, "text/html; charset=utf-8"},
			{L"/sedo", chatPage, "text/html; charset=utf-8"},
			{L"/test", diagnosticsPage, "text/html; charset=utf-8"},
			{L"/web/chat.css", chatCss, "text/css; charset=utf-8"},
			{L"/web/chat.js", chatScript, "text/javascript; charset=utf-8"},
		};

		static std::optional<std::uint64_t> ParseCursor(const HTTP_COOKED_URL& url) {
			if (url.QueryStringLength == 0) return 0;
			std::wstring_view query(url.pQueryString, url.QueryStringLength / sizeof(wchar_t));
			if (!query.starts_with(L"?after=") || query.size() == 7) return std::nullopt;

			std::string digits;
			for (const wchar_t character : query.substr(7)) {
				if (character < L'0' || character > L'9') return std::nullopt;
				digits += static_cast<char>(character);
			}
			std::uint64_t after = 0;
			const auto parsed = std::from_chars(digits.data(), digits.data() + digits.size(), after);
			if (parsed.ec != std::errc{} || parsed.ptr != digits.data() + digits.size()) return std::nullopt;
			return after;
		}

		HttpResponse RouteMessages(HANDLE requestQueue, const HTTP_REQUEST& request) {
			if (Header(request, "X-Chat-Key") != accessKey) return HttpResponse::Error(401);
			if (request.Verb == HttpVerbGET) {
				const auto after = ParseCursor(request.CookedUrl);
				if (!after) return HttpResponse::Error(400);
				return {200, chatService.ReadAfter(*after), "application/json; charset=utf-8"};
			}
			if (request.Verb != HttpVerbPOST) return HttpResponse::Error(405);
			const auto sender = Header(request, "X-Chat-Sender");
			if (sender != "local" && sender != "sedo") return HttpResponse::Error(400);
			std::string text;
			const auto status = ReadMessage(requestQueue, request.RequestId, text);
			if (status != 200) return HttpResponse::Error(status);
			chatService.Add(sender, std::move(text));
			return {200, "accepted"};
		}

		std::string accessKey;
		ChatService chatService;
	};
}