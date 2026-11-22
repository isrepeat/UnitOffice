#include "UnitOffice/PersonalEventsBuilder.Core/EventBuilder.h"

namespace unitoffice::personaleventsbuilder::core {
    EventBuilder::EventBuilder() = default;

    std::wstring_view EventBuilder::GetMessage() const noexcept {
        return L"Hello, World!";
    }
}