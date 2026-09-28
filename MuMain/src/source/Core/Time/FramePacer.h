#pragma once

namespace Core::Time
{
    // Waits in short, event-friendly slices and uses a small final spin only
    // when the slice reaches the frame deadline. FrameClock remains the source
    // of truth for the cross-frame absolute deadline and stall resynchronizing.
    class FramePacer
    {
    public:
        FramePacer();
        ~FramePacer();

        FramePacer(const FramePacer&) = delete;
        FramePacer& operator=(const FramePacer&) = delete;

        void WaitFor(double remainingMs);
        bool UsesHighResolutionTimer() const { return m_usesHighResolutionTimer; }

    private:
        void* m_waitableTimer = nullptr;
        bool m_usesHighResolutionTimer = false;
    };
}
