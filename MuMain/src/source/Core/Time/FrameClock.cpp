#include "Core/Time/FrameClock.h"

#include <algorithm>
#include <cmath>

namespace Core::Time
{
    namespace
    {
        constexpr double DefaultRefreshRate = 60.0;
        constexpr double MinimumFrameRate = 1.0;

        double SanitizedRefreshRate(DisplayRefreshRate refreshRate)
        {
            return refreshRate.hertz >= MinimumFrameRate
                ? refreshRate.hertz
                : DefaultRefreshRate;
        }
    }

    EffectiveFramePolicy ResolveFramePolicy(
        const FrameTimingSettings& settings,
        DisplayRefreshRate displayRefreshRate,
        bool isForeground,
        bool verticalSyncEngaged,
        DisplayRefreshRate maximumDisplayRefreshRate)
    {
        displayRefreshRate.hertz = SanitizedRefreshRate(displayRefreshRate);

        EffectiveFramePolicy policy;
        policy.displayRefreshRate = displayRefreshRate;
        policy.verticalSyncEngaged = settings.verticalSync && verticalSyncEngaged;
        policy.requestedFrameRate = settings.mode == FrameRateMode::FollowDisplay
            ? displayRefreshRate.hertz
            : std::max(settings.fixedFrameRate, MinimumFrameRate);
        if (settings.mode == FrameRateMode::DisplayMaximum)
        {
            policy.requestedFrameRate = maximumDisplayRefreshRate.detected
                ? SanitizedRefreshRate(maximumDisplayRefreshRate)
                : displayRefreshRate.hertz;
        }

        if (!isForeground)
        {
            policy.effectiveFrameRate = std::max(settings.backgroundFrameRate, MinimumFrameRate);
            policy.reason = FrameLimitReason::Background;
            return policy;
        }

        if (settings.verticalSync && verticalSyncEngaged
            && policy.requestedFrameRate >= displayRefreshRate.hertz)
        {
            policy.effectiveFrameRate = displayRefreshRate.hertz;
            policy.verticalSyncPacesFrames = true;
            policy.reason = FrameLimitReason::VerticalSync;
            return policy;
        }

        policy.effectiveFrameRate = policy.requestedFrameRate;
        policy.reason = displayRefreshRate.detected
            ? FrameLimitReason::FrameLimiter
            : FrameLimitReason::DisplayRefreshFallback;
        return policy;
    }

    bool HasUnevenVSyncCadence(const EffectiveFramePolicy& policy)
    {
        if (!policy.verticalSyncEngaged
            || policy.verticalSyncPacesFrames
            || policy.effectiveFrameRate <= 0.0
            || policy.displayRefreshRate.hertz <= policy.effectiveFrameRate)
        {
            return false;
        }

        const double displayIntervalsPerFrame =
            policy.displayRefreshRate.hertz / policy.effectiveFrameRate;
        return std::abs(displayIntervalsPerFrame - std::round(displayIntervalsPerFrame)) > 0.01;
    }

    std::string_view ToString(FrameLimitReason reason)
    {
        switch (reason)
        {
        case FrameLimitReason::FrameLimiter: return "frame limiter";
        case FrameLimitReason::VerticalSync: return "vertical sync";
        case FrameLimitReason::DisplayRefreshFallback: return "display refresh unavailable; using 60 Hz";
        case FrameLimitReason::Background: return "background frame limit";
        case FrameLimitReason::Uncapped: return "uncapped";
        }

        return "unknown";
    }

    void FrameClock::SetTargetFps(double framesPerSecond)
    {
        if (framesPerSecond < 0.0)
        {
            m_targetFps = -1.0;
            m_frameIntervalMs = 0.0;
        }
        else if (framesPerSecond > 0.0 && std::isfinite(framesPerSecond))
        {
            m_targetFps = framesPerSecond;
            m_frameIntervalMs = 1000.0 / framesPerSecond;
        }
        else
        {
            return;
        }

        m_nextDeadlineMs = FirstFrameDeadline;
    }

    void FrameClock::UpdateCurrentTime(double nowMs)
    {
        if (std::isfinite(nowMs))
        {
            m_currentTimeMs = nowMs;
        }
    }

    bool FrameClock::ShouldRenderNextFrame() const
    {
        constexpr double DeadlineToleranceMs = 0.001;
        return m_frameIntervalMs <= 0.0
            || m_nextDeadlineMs == FirstFrameDeadline
            || m_currentTimeMs + DeadlineToleranceMs >= m_nextDeadlineMs;
    }

    void FrameClock::MarkFrameRendered()
    {
        m_lastRenderTimeMs = m_currentTimeMs;
        if (m_frameIntervalMs <= 0.0)
        {
            m_nextDeadlineMs = FirstFrameDeadline;
            return;
        }

        if (m_nextDeadlineMs == FirstFrameDeadline)
        {
            m_nextDeadlineMs = m_currentTimeMs + m_frameIntervalMs;
            return;
        }

        m_nextDeadlineMs += m_frameIntervalMs;

        // A long stall should not trigger a burst of catch-up frames. Keep the
        // phase for ordinary late frames, but resynchronize once an entire
        // interval was missed.
        if (m_currentTimeMs >= m_nextDeadlineMs + m_frameIntervalMs)
        {
            m_nextDeadlineMs = m_currentTimeMs + m_frameIntervalMs;
        }
    }

    double FrameClock::GetCurrentFrameTime() const
    {
        return std::max(0.0, m_currentTimeMs - m_lastRenderTimeMs);
    }

    double FrameClock::GetTimeUntilNextFrame() const
    {
        if (m_frameIntervalMs <= 0.0 || m_nextDeadlineMs == FirstFrameDeadline)
        {
            return 0.0;
        }

        return std::max(0.0, m_nextDeadlineMs - m_currentTimeMs);
    }

    void FrameClock::Reset(double nowMs)
    {
        m_currentTimeMs = nowMs;
        m_lastRenderTimeMs = nowMs;
        m_nextDeadlineMs = FirstFrameDeadline;
    }
}
