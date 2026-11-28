#pragma once
#include <UnitOffice/PersonalEventsBuilder.Core/EventBuilder.h>

#include "EventBuilder.g.h"

namespace winrt::PersonalEventsBuilder::WinRt::implementation {
    struct EventBuilder : EventBuilderT<EventBuilder> {
        EventBuilder();

        static void LogInfo(winrt::hstring const& message);
        static void LogError(winrt::hstring const& message);
        static bool InitializeLogging(winrt::hstring const& filePath);

        winrt::hstring GetMessage();

    private:
        ::unitoffice::personaleventsbuilder::core::EventBuilder eventBuilder;
    };
}

namespace winrt::PersonalEventsBuilder::WinRt::factory_implementation {
    struct EventBuilder : EventBuilderT<EventBuilder, implementation::EventBuilder> {
    };
}