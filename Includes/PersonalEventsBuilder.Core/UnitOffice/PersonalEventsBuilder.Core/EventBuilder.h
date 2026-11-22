#pragma once
#include <string_view>

namespace unitoffice::personaleventsbuilder::core {
    // Каркас формирования персональных событий.
    class EventBuilder {
    public:
        EventBuilder();

        std::wstring_view GetMessage() const noexcept;
    };
}