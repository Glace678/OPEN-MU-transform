#include "Core/Time/FramePacer.h"

#include <algorithm>
#include <chrono>
#include <cmath>
#include <thread>

#ifdef _WIN32
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <Windows.h>

#ifndef CREATE_WAITABLE_TIMER_HIGH_RESOLUTION
#define CREATE_WAITABLE_TIMER_HIGH_RESOLUTION 0x00000002
#endif
#endif

namespace Core::Time
{
    namespace
    {
        using Clock = std::chrono::steady_clock;

        constexpr double MaximumWaitSliceMs = 10.0;
        constexpr double HighResolutionSpinReserveMs = 0.5;
        constexpr double FallbackSpinReserveMs = 2.0;

        void SpinUntil(Clock::time_point deadline)
        {
            while (Clock::now() < deadline)
            {
                // The clock query is the synchronization point. Avoid yield()
                // here: on Windows it can defer this thread for a full 15.6 ms
                // scheduler quantum and makes 90-180 FPS pacing impossible.
            }
        }
    }

    FramePacer::FramePacer()
    {
#ifdef _WIN32
        HANDLE timer = CreateWaitableTimerExW(
            nullptr,
            nullptr,
            CREATE_WAITABLE_TIMER_HIGH_RESOLUTION,
            TIMER_MODIFY_STATE | SYNCHRONIZE);
        if (timer != nullptr)
        {
            m_waitableTimer = timer;
            m_usesHighResolutionTimer = true;
        }
        else
        {
            // High-resolution timers were added during the Windows 10 era.
            // Keep older supported builds functional; MainLoop's
            // timeBeginPeriod(1) improves this ordinary timer's resolution.
            m_waitableTimer = CreateWaitableTimerExW(
                nullptr,
                nullptr,
                0,
                TIMER_MODIFY_STATE | SYNCHRONIZE);
        }
#endif
    }

    FramePacer::~FramePacer()
    {
#ifdef _WIN32
        if (m_waitableTimer != nullptr)
        {
            CloseHandle(static_cast<HANDLE>(m_waitableTimer));
        }
#endif
    }

    void FramePacer::WaitFor(double remainingMs)
    {
        if (!std::isfinite(remainingMs) || remainingMs <= 0.0)
        {
            return;
        }

        const bool reachesFrameDeadline = remainingMs <= MaximumWaitSliceMs;
        const double sliceMs = std::min(remainingMs, MaximumWaitSliceMs);
        const double desiredSpinReserveMs = m_usesHighResolutionTimer
            ? HighResolutionSpinReserveMs
            : FallbackSpinReserveMs;
        const double spinReserveMs = reachesFrameDeadline
            ? std::min(desiredSpinReserveMs, sliceMs)
            : 0.0;

        const auto startedAt = Clock::now();
        const auto sliceDeadline = startedAt
            + std::chrono::duration_cast<Clock::duration>(
                std::chrono::duration<double, std::milli>(sliceMs));
        const auto coarseDeadline = sliceDeadline
            - std::chrono::duration_cast<Clock::duration>(
                std::chrono::duration<double, std::milli>(spinReserveMs));

        bool coarseWaitCompleted = coarseDeadline <= startedAt;
#ifdef _WIN32
        if (!coarseWaitCompleted && m_waitableTimer != nullptr)
        {
            const double coarseWaitMs = std::chrono::duration<double, std::milli>(
                coarseDeadline - Clock::now()).count();
            if (coarseWaitMs > 0.0)
            {
                LARGE_INTEGER dueTime{};
                dueTime.QuadPart = -std::max<LONGLONG>(
                    1,
                    static_cast<LONGLONG>(std::llround(coarseWaitMs * 10000.0)));
                if (SetWaitableTimerEx(
                        static_cast<HANDLE>(m_waitableTimer),
                        &dueTime,
                        0,
                        nullptr,
                        nullptr,
                        nullptr,
                        0)
                    && WaitForSingleObject(static_cast<HANDLE>(m_waitableTimer), INFINITE)
                        == WAIT_OBJECT_0)
                {
                    coarseWaitCompleted = true;
                }
                else
                {
                    CloseHandle(static_cast<HANDLE>(m_waitableTimer));
                    m_waitableTimer = nullptr;
                    m_usesHighResolutionTimer = false;
                }
            }
        }
#endif

        if (!coarseWaitCompleted)
        {
            std::this_thread::sleep_until(coarseDeadline);
        }

        if (reachesFrameDeadline)
        {
            SpinUntil(sliceDeadline);
        }
    }
}
