#include "EventBuilder.h"

#include "EventBuilder.g.cpp"

namespace winrt::PersonalEventsBuilder::WinRt::implementation
{
    EventBuilder::EventBuilder() = default;

    winrt::hstring EventBuilder::GetMessage()
    {
        return L"Hello from PersonalEventsBuilder!";
    }
}