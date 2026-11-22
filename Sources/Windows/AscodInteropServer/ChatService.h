#pragma once
#include <algorithm>
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
		bool Add(std::string_view sender, std::string text, std::string image, std::vector<Attachment> attachments) {
			std::size_t bytes = text.size() + image.size();
			for (const auto& attachment : attachments) {
				bytes += sizeof(Attachment) + attachment.name.size() + attachment.data.size();
			}
			std::size_t pendingCount = 0;
			std::size_t pendingBytes = 0;
			for (const auto& message : messages) {
				if (this->IsPending(message)) {
					++pendingCount;
					pendingBytes += message.bytes;
				}
			}
			if (pendingCount >= maxMessages || bytes > maxStoredBytes - pendingBytes) {
				return false;
			}
			while (messages.size() >= maxMessages || bytes > maxStoredBytes - storedBytes) {
				const auto oldest = std::find_if(messages.begin(), messages.end(), [this](const Message& message) {
					return !this->IsPending(message);
				});
				storedBytes -= oldest->bytes;
				messages.erase(oldest);
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
			return true;
		}

		void Clear() {
			messages.clear();
			storedBytes = 0;
			localAcknowledged = 0;
			sedoAcknowledged = 0;
			clearedThrough = nextId++;
		}

		void Acknowledge(std::string_view recipient, std::uint64_t after) {
			if (after >= nextId) {
				return;
			}
			auto& acknowledged = recipient == "local" ? localAcknowledged : sedoAcknowledged;
			acknowledged = (std::max)(acknowledged, after);
		}

		std::string ReadAfter(std::uint64_t after, std::string_view recipient) const {
			const auto peerAcknowledged = recipient == "local" ? sedoAcknowledged : localAcknowledged;
			std::string body = "{\"latest\":" + std::to_string(nextId - 1) +
				",\"clearedThrough\":" + std::to_string(clearedThrough) +
				",\"peerAcknowledged\":" + std::to_string(peerAcknowledged) + ",\"messages\":[";
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
		bool IsPending(const Message& message) const {
			const auto acknowledged = message.sender == "local" ? sedoAcknowledged : localAcknowledged;
			return message.id > acknowledged;
		}

		static constexpr std::size_t maxMessages = 200;
		static constexpr std::size_t maxStoredBytes = 96 * 1024 * 1024;
		std::deque<Message> messages;
		std::uint64_t nextId = 1;
		std::uint64_t clearedThrough = 0;
		std::uint64_t localAcknowledged = 0;
		std::uint64_t sedoAcknowledged = 0;
		std::size_t storedBytes = 0;
	};
}