#pragma once
#include <string_view>
#include <cstdint>
#include <utility>
#include <string>
#include <vector>
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

	struct Attachment {
		std::string name;
		std::string data;
	};

	struct Message {
		std::uint64_t id;
		std::string sender;
		std::string text;
		std::string image;
		std::vector<Attachment> attachments;
		std::size_t bytes;
	};

	class ChatService {
	public:
		void Add(std::string_view sender, std::string text, std::string image, std::vector<Attachment> attachments) {
			std::size_t bytes = text.size() + image.size();
			for (const auto& attachment : attachments) {
				bytes += sizeof(Attachment) + attachment.name.size() + attachment.data.size();
			}
			messages.push_back({
				nextId++,
				std::string(sender),
				std::move(text),
				std::move(image),
				std::move(attachments),
				bytes
			});
			storedBytes += bytes;
			while (messages.size() > 200 || storedBytes > 96 * 1024 * 1024) {
				const auto& oldest = messages.front();
				storedBytes -= oldest.bytes;
				messages.pop_front();
			}
		}

		std::string ReadAfter(std::uint64_t after) const {
			std::string body = "{\"latest\":" + std::to_string(nextId - 1) + ",\"messages\":[";
			bool first = true;
			for (const auto& message : messages) {
				if (message.id <= after) {
					continue;
				}
				if (!first) {
					body += ',';
				}
				first = false;
				body += "{\"id\":" + std::to_string(message.id) + ",\"sender\":" + details::JsonString(message.sender) +
					",\"text\":" + details::JsonString(message.text) + ",\"image\":" + details::JsonString(message.image);
				if (!message.attachments.empty()) {
					body += ",\"files\":[";
					for (const auto& attachment : message.attachments) {
						if (&attachment != &message.attachments.front()) {
							body += ',';
						}
						body += "{\"name\":" + details::JsonString(attachment.name) + ",\"data\":" + details::JsonString(attachment.data) + '}';
					}
					body += ']';
				}
				body += '}';
			}
			body += "]}";
			return body;
		}

	private:
		std::deque<Message> messages;
		std::uint64_t nextId = 1;
		std::size_t storedBytes = 0;
	};
}