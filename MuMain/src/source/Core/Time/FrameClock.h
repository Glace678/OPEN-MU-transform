#pragma once

#include <string_view>

namespace Core::Time
{
    enum class FrameRateMode
    {
        Fixed,
        FollowDisplay,
        DisplayMaximum,
    };

    struct DisplayRefreshRate
    {
        double hertz = 60.0;
        bool detected = false;
    };

    struct FrameTimingSettings
    {
        FrameRateMode mode = FrameRateMode::DisplayMaximum;
        double fixedFrameRate = 60.0;
        double backgroundFrameRate = 30.0;
        bool verticalSync = false;
    };

    enum class FrameLimitReason
    {
        FrameLimiter,
        VerticalSync,
        DisplayRefreshFallback,
        Background,
        Uncapped,
    };

    struct EffectiveFramePolicy
    {
        double requestedFrameRate = 60.0;
        double effectiveFrameRate = 60.0;
        DisplayRefreshRate displayRefreshRate{};
        FrameLimitReason reason = FrameLimitReason::FrameLimiter;
        bool verticalSyncEngaged = false;
        bool verticalSyncPacesFrames = false;
    };

    EffectiveFramePolicy ResolveFramePolicy(
        const FrameTimingSettings& settings,
        DisplayRefreshRate displayRefreshRate,
        bool isForeground,
        bool verticalSyncEngaged,
        DisplayRefreshRate maximumDisplayRefreshRate = {0.0, false});

    bool HasUnevenVSyncCadence(const EffectiveFramePolicy& policy);

    std::string_view ToString(FrameLimitReason reason);

    // Absolute-deadline frame pacer. Deadlines advance from the previous
    // deadline instead of the end of the previous frame, preventing sleep and
    // render overhead from accumulating into long-term drift.
    class FrameClock
    {
    public:
        void SetTargetFps(double framesPerSecond);
        double GetTargetFps() const { return m_targetFps; }
        double GetMsPerFrame() const { return m_frameIntervalMs; }

        void UpdateCurrentTime(double nowMs);
        bool ShouldRenderNextFrame() const;
        void MarkFrameRendered();

        double GetCurrentTime() const { return m_currentTimeMs; }
        double GetCurrentFrameTime() const;
        double GetTimeUntilNextFrame() const;
        void Reset(double nowMs = 0.0);

    private:
        static constexpr double FirstFrameDeadline = -1.0;

        double m_targetFps = -1.0;
        double m_frameIntervalMs = 0.0;
        double m_currentTimeMs = 0.0;
        double m_lastRenderTimeMs = 0.0;
        double m_nextDeadlineMs = FirstFrameDeadline;
    };
}
