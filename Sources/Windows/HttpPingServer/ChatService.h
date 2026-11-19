#pragma once
#include <string_view>
#include <cstdint>
#include <utility>
#include <string>
#include <deque>

namespace unitoffice::windows::httppingserver {
	namespace details {
		inline std::string JsonString(std::string_view value) {
			std::string result = "\"";
			constexpr char hex[] = "0123456789abcdef";
			for (unsigned char character : value) {
				if (character == '"' || character == '\\') {
					result += '\\';
					result += character;
				} else if (character < 32) {
					result += "\\u00";
					result += hex[character >> 4];
					result += hex[character & 15];
				} else {
					result += character;
				}
			}
			return result + '"';
		}
	} // namespace details

	struct Message {
		std::uint64_t id;
		std::string sender;
		std::string text;
	};

	class ChatService {
	public:
		void Add(std::string_view sender, std::string text) {
			messages.push_back({nextId++, std::string(sender), std::move(text)});
			if (messages.size() > 200) {
				messages.pop_front();
			}
		}

		std::string ReadAfter(std::uint64_t after) const {
			std::string body = "{\"latest\":" + std::to_string(nextId - 1) + ",\"messages\":[";
			bool first = true;
			for (const auto& message : messages) {
				if (message.id <= after) continue;
				if (!first) {
					body += ',';
				}
				first = false;
				body += "{\"id\":" + std::to_string(message.id) + ",\"sender\":" + details::JsonString(message.sender) +
					",\"text\":" + details::JsonString(message.text) + "}";
			}
			body += "]}";
			return body;
		}

	private:
		std::deque<Message> messages;
		std::uint64_t nextId = 1;
	};
}