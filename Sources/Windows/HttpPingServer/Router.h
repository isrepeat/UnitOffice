#pragma once
#include "HttpExchange.h"
#include "ChatService.h"

#include <filesystem>
#include <functional>
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
				return {200, "pong\n", "text/plain; charset=utf-8", true};
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
					if (std::wstring_view(asset.fileName) == L"Chat.html") {
						for (const auto* name : {"Chat.css", "Chat.js"}) {
							std::ifstream resource(this->webDirectory / name, std::ios::binary);
							if (!resource) {
								return HttpResponse::Error(500);
							}
							const std::string content(std::istreambuf_iterator<char>{resource}, std::istreambuf_iterator<char>{});
							const std::string reference = std::string(name) + '"';
							const auto position = body.find(reference);
							if (position != std::string::npos) {
								body.replace(
									position,
									reference.size(),
									std::string(name) + "?v=" + std::to_string(std::hash<std::string>{}(content)) + '"'
								);
							}
						}
					}
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
			{L"/page1", L"Page1.html", "text/html; charset=utf-8"},
			{L"/page1/card", L"Page1.html", "text/html; charset=utf-8"},
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
			const auto type = details::Header(request, "X-Chat-Type");
			const bool hasImage = type == "image";
			const bool hasFiles = type == "file" || type == "files";
			constexpr std::size_t maxFileBytes = 50 * 1024 * 1024;
			constexpr std::size_t maxEncodedBytes = ((maxFileBytes + 2) / 3) * 4;
			constexpr std::size_t maxFileName = 4096;
			constexpr std::size_t maxFiles = 200;
			const auto status = details::ReadMessage(
				requestQueue,
				request.RequestId,
				text,
				hasFiles ? maxEncodedBytes + maxFiles * (maxFileName + 6) + 16001 : (hasImage ? 5610000 : 16000)
			);
			if (status != 200) {
				return HttpResponse::Error(status);
			}
			std::string image;
			std::vector<Attachment> attachments;
			if (hasImage) {
				const auto separator = text.find('\n');
				if (separator == std::string::npos || separator > 5592430 || text.size() - separator - 1 > 16000) {
					return HttpResponse::Error(400);
				}
				image = text.substr(0, separator);
				text.erase(0, separator + 1);
				constexpr std::string_view prefix = "data:image/png;base64,";
				if (!image.starts_with(prefix) || !std::string_view(image).substr(prefix.size()).starts_with("iVBORw0KGgo") ||
					(image.size() - prefix.size()) % 4 != 0 ||
					image.find_first_not_of("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=", prefix.size()) != std::string::npos) {
					return HttpResponse::Error(400);
				}
			} else if (hasFiles) {
				std::string_view remaining(text);
				std::size_t totalBytes = 0;
				do {
					const auto nameEnd = remaining.find('\n');
					const auto dataEnd = nameEnd == std::string_view::npos ? nameEnd : remaining.find('\n', nameEnd + 1);
					if (nameEnd == 0 || nameEnd > maxFileName || dataEnd == std::string_view::npos || attachments.size() == maxFiles) {
						return HttpResponse::Error(400);
					}
					const auto name = remaining.substr(0, nameEnd);
					const auto data = remaining.substr(nameEnd + 1, dataEnd - nameEnd - 1);
					if (name.find_first_not_of("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_.!~*'()%") != std::string_view::npos ||
						data.size() % 4 != 0) {
						return HttpResponse::Error(400);
					}
					const std::size_t padding = data.ends_with("==") ? 2 : (data.ends_with("=") ? 1 : 0);
					if (data.substr(0, data.size() - padding).find_first_not_of("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/")
						!= std::string_view::npos) {
						return HttpResponse::Error(400);
					}
					totalBytes += data.size() / 4 * 3 - padding;
					if (totalBytes > maxFileBytes) {
						return HttpResponse::Error(413);
					}
					attachments.push_back({std::string(name), std::string(data)});
					remaining.remove_prefix(dataEnd + 1);
				} while (type == "files" && !remaining.starts_with('\n'));
				if (type == "files") {
					remaining.remove_prefix(1);
				}
				if (remaining.size() > 16000) {
					return HttpResponse::Error(400);
				}
				text.erase(0, text.size() - remaining.size());
			}
			this->chatService.Add(sender, std::move(text), std::move(image), std::move(attachments));
			return {200, "accepted"};
		}

		std::string accessKey;
		std::filesystem::path webDirectory;
		ChatService chatService;
	};
}