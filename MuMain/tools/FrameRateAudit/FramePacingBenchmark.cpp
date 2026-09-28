#include "Core/Time/FrameClock.h"
#include "Core/Time/FramePacer.h"

#include <algorithm>
#include <array>
#include <chrono>
#include <cmath>
#include <cstdlib>
#include <ctime>
#include <functional>
#include <iomanip>
#include <iostream>
#include <numeric>
#include <optional>
#include <stdexcept>
#include <string>
#include <string_view>
#include <thread>
#include <vector>

#ifdef _WIN32
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <Windows.h>
#include <mmsystem.h>
#endif

namespace
{
    using Clock = std::chrono::steady_clock;
    using Core::Time::DisplayRefreshRate;
    using Core::Time::EffectiveFramePolicy;
    using Core::Time::FrameClock;
    using Core::Time::FramePacer;
    using Core::Time::FrameRateMode;
    using Core::Time::FrameTimingSettings;

    enum class WaitMode
    {
        Sleep,
        Spin,
    };

    struct Options
    {
        double durationSeconds = 1.5;
        double warmupSeconds = 0.2;
        double tolerancePercent = 2.0;
        double displayHertz = 143.98;
        WaitMode waitMode = WaitMode::Sleep;
    };

    struct Measurement
    {
        std::string label;
        double targetFps = 0.0;
        double actualFps = 0.0;
        double averageIntervalMs = 0.0;
        double p95IntervalMs = 0.0;
        double p99IntervalMs = 0.0;
        double maximumIntervalMs = 0.0;
        double onePercentLowFps = 0.0;
        double cpuCorePercent = 0.0;
        double driftMs = 0.0;
        double errorPercent = 0.0;
        std::size_t renderedFrames = 0;
        std::size_t missedDeadlines = 0;
        bool passed = false;
    };

#ifdef _WIN32
    class TimerResolution
    {
    public:
        TimerResolution()
            : m_enabled(timeBeginPeriod(1) == TIMERR_NOERROR)
        {
        }

        ~TimerResolution()
        {
            if (m_enabled)
            {
                timeEndPeriod(1);
            }
        }

        bool IsEnabled() const { return m_enabled; }

    private:
        bool m_enabled = false;
    };
#else
    class TimerResolution
    {
    public:
        bool IsEnabled() const { return false; }
    };
#endif

    double ProcessCpuSeconds()
    {
#ifdef _WIN32
        FILETIME creationTime{};
        FILETIME exitTime{};
        FILETIME kernelTime{};
        FILETIME userTime{};
        if (!GetProcessTimes(
                GetCurrentProcess(),
                &creationTime,
                &exitTime,
                &kernelTime,
                &userTime))
        {
            return 0.0;
        }

        ULARGE_INTEGER kernel{};
        kernel.LowPart = kernelTime.dwLowDateTime;
        kernel.HighPart = kernelTime.dwHighDateTime;
        ULARGE_INTEGER user{};
        user.LowPart = userTime.dwLowDateTime;
        user.HighPart = userTime.dwHighDateTime;
        return static_cast<double>(kernel.QuadPart + user.QuadPart) / 10000000.0;
#else
        return static_cast<double>(std::clock()) / static_cast<double>(CLOCKS_PER_SEC);
#endif
    }

    double ParsePositiveDouble(std::string_view value, std::string_view option)
    {
        const std::string owned(value);
        char* end = nullptr;
        const double parsed = std::strtod(owned.c_str(), &end);
        if (end != owned.c_str() + owned.size() || !std::isfinite(parsed) || parsed <= 0.0)
        {
            throw std::runtime_error("Invalid value for " + std::string(option) + ": " + owned);
        }

        return parsed;
    }

    Options ParseOptions(int argc, char** argv)
    {
        Options options;
        for (int index = 1; index < argc; ++index)
        {
            const std::string_view argument(argv[index]);
            if (argument == "--help")
            {
                std::cout
                    << "Usage: frame_pacing_benchmark [options]\n"
                    << "  --duration-seconds N  Measurement time per frame-rate mode (default 1.5)\n"
                    << "  --warmup-seconds N    Warm-up time per mode (default 0.2)\n"
                    << "  --tolerance-percent N Allowed actual-FPS error (default 2.0)\n"
                    << "  --display-hz N        Fractional refresh used by FollowDisplay (default 143.98)\n"
                    << "  --wait-mode MODE      sleep (OS scheduler) or spin (clock reference)\n";
                std::exit(EXIT_SUCCESS);
            }

            if (argument == "--wait-mode")
            {
                if (index + 1 >= argc)
                {
                    throw std::runtime_error("Missing value for " + std::string(argument));
                }

                const std::string_view value(argv[++index]);
                if (value == "sleep")
                {
                    options.waitMode = WaitMode::Sleep;
                }
                else if (value == "spin")
                {
                    options.waitMode = WaitMode::Spin;
                }
                else
                {
                    throw std::runtime_error("Invalid value for --wait-mode: " + std::string(value));
                }
                continue;
            }

            if (index + 1 >= argc)
            {
                throw std::runtime_error("Missing value for " + std::string(argument));
            }

            const std::string_view value(argv[++index]);
            if (argument == "--duration-seconds")
            {
                options.durationSeconds = ParsePositiveDouble(value, argument);
            }
            else if (argument == "--warmup-seconds")
            {
                options.warmupSeconds = ParsePositiveDouble(value, argument);
            }
            else if (argument == "--tolerance-percent")
            {
                options.tolerancePercent = ParsePositiveDouble(value, argument);
            }
            else if (argument == "--display-hz")
            {
                options.displayHertz = ParsePositiveDouble(value, argument);
            }
            else
            {
                throw std::runtime_error("Unknown option: " + std::string(argument));
            }
        }

        return options;
    }

    double ElapsedMilliseconds(Clock::time_point start, Clock::time_point end)
    {
        return std::chrono::duration<double, std::milli>(end - start).count();
    }

    void WaitForDeadline(
        FrameClock& frameClock,
        FramePacer& framePacer,
        Clock::time_point start,
        WaitMode waitMode)
    {
        while (true)
        {
            const auto now = Clock::now();
            frameClock.UpdateCurrentTime(ElapsedMilliseconds(start, now));
            if (frameClock.ShouldRenderNextFrame())
            {
                return;
            }

            if (waitMode == WaitMode::Spin)
            {
                continue;
            }

            framePacer.WaitFor(frameClock.GetTimeUntilNextFrame());
        }
    }

    std::vector<double> CaptureIntervals(
        double targetFps,
        double durationSeconds,
        WaitMode waitMode,
        FramePacer& framePacer)
    {
        FrameClock frameClock;
        frameClock.SetTargetFps(targetFps);

        const auto start = Clock::now();
        frameClock.Reset(0.0);
        frameClock.UpdateCurrentTime(0.0);
        frameClock.MarkFrameRendered();

        const auto frameCount = static_cast<std::size_t>(
            std::max(2.0, std::round(targetFps * durationSeconds)));
        std::vector<double> intervals;
        intervals.reserve(frameCount);

        auto previousFrame = start;
        for (std::size_t frame = 0; frame < frameCount; ++frame)
        {
            WaitForDeadline(frameClock, framePacer, start, waitMode);
            const auto renderedAt = Clock::now();
            frameClock.UpdateCurrentTime(ElapsedMilliseconds(start, renderedAt));
            frameClock.MarkFrameRendered();
            intervals.push_back(ElapsedMilliseconds(previousFrame, renderedAt));
            previousFrame = renderedAt;
        }

        return intervals;
    }

    double Percentile(std::vector<double> values, double percentile)
    {
        std::sort(values.begin(), values.end());
        const double rank = percentile * static_cast<double>(values.size() - 1);
        const auto lower = static_cast<std::size_t>(std::floor(rank));
        const auto upper = static_cast<std::size_t>(std::ceil(rank));
        const double fraction = rank - static_cast<double>(lower);
        return values[lower] + (values[upper] - values[lower]) * fraction;
    }

    double OnePercentLowFps(std::vector<double> intervals)
    {
        std::sort(intervals.begin(), intervals.end(), std::greater<>());
        const auto sampleCount = static_cast<std::size_t>(
            std::max(1.0, std::ceil(static_cast<double>(intervals.size()) * 0.01)));
        const double slowestIntervalTotal = std::accumulate(
            intervals.begin(),
            intervals.begin() + sampleCount,
            0.0);
        return 1000.0 / (slowestIntervalTotal / static_cast<double>(sampleCount));
    }

    Measurement Measure(
        std::string label,
        double targetFps,
        const Options& options,
        FramePacer& framePacer)
    {
        CaptureIntervals(targetFps, options.warmupSeconds, options.waitMode, framePacer);
        const double cpuStartedAt = ProcessCpuSeconds();
        auto intervals = CaptureIntervals(targetFps, options.durationSeconds, options.waitMode, framePacer);
        const double cpuFinishedAt = ProcessCpuSeconds();

        const double elapsedMs = std::accumulate(intervals.begin(), intervals.end(), 0.0);
        const double idealElapsedMs = static_cast<double>(intervals.size()) * 1000.0 / targetFps;

        Measurement result;
        result.label = std::move(label);
        result.targetFps = targetFps;
        result.actualFps = static_cast<double>(intervals.size()) * 1000.0 / elapsedMs;
        result.averageIntervalMs = elapsedMs / static_cast<double>(intervals.size());
        result.p95IntervalMs = Percentile(intervals, 0.95);
        result.p99IntervalMs = Percentile(intervals, 0.99);
        result.maximumIntervalMs = *std::max_element(intervals.begin(), intervals.end());
        result.onePercentLowFps = OnePercentLowFps(intervals);
        const double cpuSeconds = cpuFinishedAt - cpuStartedAt;
        result.cpuCorePercent = cpuSeconds / (elapsedMs / 1000.0) * 100.0;
        result.driftMs = elapsedMs - idealElapsedMs;
        result.errorPercent = std::abs(result.actualFps - targetFps) / targetFps * 100.0;
        result.renderedFrames = intervals.size();
        const double missedDeadlineThresholdMs = 1500.0 / targetFps;
        result.missedDeadlines = static_cast<std::size_t>(std::count_if(
            intervals.begin(),
            intervals.end(),
            [missedDeadlineThresholdMs](double intervalMs)
            {
                return intervalMs > missedDeadlineThresholdMs;
            }));
        result.passed = result.errorPercent <= options.tolerancePercent;
        return result;
    }

    EffectiveFramePolicy ResolveFollowDisplayPolicy(double displayHertz, bool vsyncEngaged)
    {
        FrameTimingSettings settings;
        settings.mode = FrameRateMode::FollowDisplay;
        settings.verticalSync = vsyncEngaged;
        return Core::Time::ResolveFramePolicy(
            settings,
            DisplayRefreshRate{displayHertz, true},
            true,
            vsyncEngaged);
    }

    void PrintResults(const std::vector<Measurement>& measurements)
    {
        std::cout
            << std::left << std::setw(22) << "Mode"
            << std::right << std::setw(10) << "Target"
            << std::setw(10) << "Actual"
            << std::setw(11) << "Error %"
            << std::setw(12) << "Mean ms"
            << std::setw(11) << "P95 ms"
            << std::setw(11) << "P99 ms"
            << std::setw(11) << "Max ms"
            << std::setw(11) << "1% Low"
            << std::setw(12) << "CPU core%"
            << std::setw(9) << "Missed"
            << std::setw(11) << "Drift ms"
            << std::setw(9) << "Result" << '\n';

        for (const auto& measurement : measurements)
        {
            std::cout
                << std::left << std::setw(22) << measurement.label
                << std::right << std::fixed << std::setprecision(3)
                << std::setw(10) << measurement.targetFps
                << std::setw(10) << measurement.actualFps
                << std::setw(11) << measurement.errorPercent
                << std::setw(12) << measurement.averageIntervalMs
                << std::setw(11) << measurement.p95IntervalMs
                << std::setw(11) << measurement.p99IntervalMs
                << std::setw(11) << measurement.maximumIntervalMs
                << std::setw(11) << measurement.onePercentLowFps
                << std::setw(12) << measurement.cpuCorePercent
                << std::setw(9) << measurement.missedDeadlines
                << std::setw(11) << measurement.driftMs
                << std::setw(9) << (measurement.passed ? "PASS" : "FAIL")
                << '\n';
        }
    }
}

int main(int argc, char** argv)
{
    try
    {
        const Options options = ParseOptions(argc, argv);
        const TimerResolution timerResolution;
        FramePacer framePacer;

        const auto followDisplayLimiter = ResolveFollowDisplayPolicy(options.displayHertz, false);
        const auto followDisplayVsync = ResolveFollowDisplayPolicy(options.displayHertz, true);
        const bool policyPassed =
            std::abs(followDisplayLimiter.requestedFrameRate - options.displayHertz) < 0.000001
            && std::abs(followDisplayLimiter.effectiveFrameRate - options.displayHertz) < 0.000001
            && !followDisplayLimiter.verticalSyncPacesFrames
            && std::abs(followDisplayVsync.effectiveFrameRate - options.displayHertz) < 0.000001
            && followDisplayVsync.verticalSyncPacesFrames;

        std::cout << "MuMain FrameClock real-time pacing benchmark\n"
                  << "clock=steady_clock, wait="
                  << (options.waitMode == WaitMode::Sleep
                          ? "shared FramePacer (waitable timer + final spin)"
                          : "absolute busy-spin reference")
                  << ", "
                  << "timer_resolution_1ms=" << (timerResolution.IsEnabled() ? "yes" : "n/a") << '\n'
                  << "high_resolution_waitable_timer="
                  << (framePacer.UsesHighResolutionTimer() ? "yes" : "no") << '\n'
                  << "duration_per_mode=" << options.durationSeconds
                  << "s, warmup_per_mode=" << options.warmupSeconds
                  << "s, tolerance=+/-" << options.tolerancePercent << "%\n"
                  << "FollowDisplay policy (" << options.displayHertz << " Hz): limiter="
                  << followDisplayLimiter.effectiveFrameRate << " Hz, VSync="
                  << followDisplayVsync.effectiveFrameRate << " Hz, externally_paced="
                  << (followDisplayVsync.verticalSyncPacesFrames ? "yes" : "no")
                  << ", result=" << (policyPassed ? "PASS" : "FAIL") << "\n\n";

        constexpr std::array<double, 6> fixedRates{30.0, 60.0, 90.0, 120.0, 144.0, 180.0};
        std::vector<Measurement> measurements;
        measurements.reserve(fixedRates.size() + 1);
        for (const double rate : fixedRates)
        {
            measurements.push_back(Measure(
                "Fixed " + std::to_string(static_cast<int>(rate)),
                rate,
                options,
                framePacer));
        }
        measurements.push_back(Measure(
            "FollowDisplay",
            followDisplayLimiter.effectiveFrameRate,
            options,
            framePacer));

        PrintResults(measurements);
        std::cout << "\nResult checks average FPS against the configured tolerance; "
                     "1% Low and Missed (>1.5x interval) expose scheduler jitter.\n";
        const bool timingPassed = std::all_of(
            measurements.begin(),
            measurements.end(),
            [](const Measurement& measurement) { return measurement.passed; });

        std::cout << "\nOverall: " << (policyPassed && timingPassed ? "PASS" : "FAIL") << '\n';
        return policyPassed && timingPassed ? EXIT_SUCCESS : EXIT_FAILURE;
    }
    catch (const std::exception& error)
    {
        std::cerr << "frame_pacing_benchmark: " << error.what() << '\n';
        return EXIT_FAILURE;
    }
}
