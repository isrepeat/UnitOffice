#include "EventBuilder.h"

#include <UniversalDevKit/Diagnostic/Logging.h>

#include "EventBuilder.g.cpp"

namespace winrt::PersonalEventsBuilder::WinRt::implementation {
    EventBuilder::EventBuilder() = default;

    bool EventBuilder::InitializeLogging(winrt::hstring const& filePath) {
        universaldevkit::diagnostic::LoggerOptions loggerOptions;
        loggerOptions.filePath = filePath.c_str();
        return universaldevkit::diagnostic::Logger::TryInitialize(loggerOptions);
    }

    void EventBuilder::LogInfo(winrt::hstring const& message) {
        UDK_LOG_INFO("PersonalEventsBuilder", L"{}", message.c_str());
        universaldevkit::diagnostic::Logger::Flush();
    }

    void EventBuilder::LogError(winrt::hstring const& message) {
        UDK_LOG_ERROR("PersonalEventsBuilder", L"{}", message.c_str());
        universaldevkit::diagnostic::Logger::Flush();
    }

    winrt::hstring EventBuilder::GetMessage() {
        return winrt::hstring{this->eventBuilder.GetMessage()};
    }
}