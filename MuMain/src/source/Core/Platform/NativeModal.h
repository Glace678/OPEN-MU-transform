// Native (Win32) modal dialogs shown on top of the game window need the real OS
// pointer: the game keeps the per-thread Win32 cursor counter negative via
// WM_SETCURSOR (it draws its own cursor sprite), which also hides the pointer
// over a same-thread MessageBox and makes the Yes/No buttons unclickable.
// BeginNativeModal() balances the counter and frees any cursor clip for the
// duration of a native dialog; EndNativeModal() restores the hidden state.
#pragma once

namespace Core::Platform
{
#if defined(_WIN32)
    void BeginNativeModal();
    void EndNativeModal();
    bool IsNativeModalActive();
#else
    inline void BeginNativeModal() {}
    inline void EndNativeModal() {}
    inline bool IsNativeModalActive() { return false; }
#endif
}
