using Windows.System;

namespace UDK.WinUI.Navigation {
    public interface IKeyboardNavigationArea {
        bool FocusArea();
        void MoveFocus(VirtualKey direction);
    }
}