#include "Core/Haptics/Haptics.h"

#include <algorithm>
#include <array>
#include <cmath>
#include <limits>

namespace Core::Haptics
{
    namespace
    {
        constexpr std::uint32_t MaximumPulseMs = 500;
        constexpr std::uint32_t MaximumPatternMs = 1000;
        constexpr float MaximumStrength = 1.0f;

        HapticPattern MakePattern(
            HapticCategory category,
            int priority,
            std::initializer_list<HapticPulse> pulses)
        {
            return { category, priority, pulses };
        }

        const std::array<HapticPattern, 18> Patterns = {
            MakePattern(HapticCategory::UserInterface, 10, {{0.00f, 0.12f, 18, 0}}),
            MakePattern(HapticCategory::UserInterface, 10, {{0.00f, 0.18f, 22, 0}}),
            MakePattern(HapticCategory::UserInterface, 20, {{0.10f, 0.25f, 35, 0}}),
            MakePattern(HapticCategory::UserInterface, 20, {{0.18f, 0.06f, 45, 0}}),
            MakePattern(HapticCategory::Transaction, 30, {{0.12f, 0.24f, 40, 0}}),
            MakePattern(HapticCategory::Transaction, 30, {{0.12f, 0.24f, 40, 0}}),
            MakePattern(HapticCategory::Transaction, 70, {
                {0.25f, 0.45f, 55, 0},
                {0.15f, 0.35f, 45, 55},
            }),
            MakePattern(HapticCategory::Transaction, 60, {{0.20f, 0.35f, 55, 0}}),
            MakePattern(HapticCategory::Transaction, 80, {{0.45f, 0.12f, 160, 0}}),
            MakePattern(HapticCategory::Combat, 40, {{0.18f, 0.32f, 45, 0}}),
            MakePattern(HapticCategory::Combat, 75, {{0.55f, 0.68f, 95, 0}}),
            MakePattern(HapticCategory::Combat, 80, {{0.70f, 0.80f, 120, 0}}),
            MakePattern(HapticCategory::Combat, 85, {{0.65f, 0.25f, 110, 0}}),
            MakePattern(HapticCategory::Combat, 100, {{0.75f, 0.55f, 300, 0}}),
            MakePattern(HapticCategory::Combat, 90, {{0.70f, 0.50f, 220, 0}}),
            MakePattern(HapticCategory::UserInterface, 15, {{0.25f, 0.35f, 80, 0}}),
            MakePattern(HapticCategory::UserInterface, 15, {{0.45f, 0.45f, 500, 0}}),
            MakePattern(HapticCategory::UserInterface, 1, {{0.06f, 0.14f, 18, 0}}),
        };

        std::size_t Index(HapticEvent event)
        {
            return static_cast<std::size_t>(event);
        }

        std::uint16_t ToMotorValue(float strength, float intensity)
        {
            const float scaled = std::clamp(strength * intensity, 0.0f, MaximumStrength);
            return static_cast<std::uint16_t>(std::lround(scaled * std::numeric_limits<std::uint16_t>::max()));
        }
    }

    HapticScheduler::HapticScheduler(IHapticOutput& output)
        : m_output(output)
    {
    }

    void HapticScheduler::SetSettings(const HapticSettings& settings)
    {
        const HapticSettings previous = m_settings;
        m_settings = settings;
        m_settings.intensityPercent = std::clamp(m_settings.intensityPercent, 0, 100);
        const bool categoryDisabled =
            (previous.combatEnabled && !m_settings.combatEnabled)
            || (previous.uiEnabled && !m_settings.uiEnabled)
            || (previous.transactionEnabled && !m_settings.transactionEnabled);
        if ((previous.enabled && !m_settings.enabled)
            || m_settings.intensityPercent == 0
            || categoryDisabled)
        {
            Stop();
        }
    }

    bool HapticScheduler::Publish(HapticEvent event, double nowMs)
    {
        if (!m_settings.enabled || m_settings.intensityPercent <= 0 || !m_output.SupportsRumble())
        {
            return false;
        }

        const HapticPattern& pattern = PatternFor(event);
        const bool isTestPulse = event == HapticEvent::ShortTest || event == HapticEvent::LongTest;
        if ((!isTestPulse && !IsCategoryEnabled(pattern.category)) || IsRateLimited(event, nowMs))
        {
            return false;
        }

        double cursorMs = nowMs;
        const double patternEndsAt = nowMs + MaximumPatternMs;
        for (const HapticPulse& pulse : pattern.pulses)
        {
            cursorMs += pulse.delayBeforeMs;
            const std::uint32_t duration = std::min(pulse.durationMs, MaximumPulseMs);
            const double endsAt = std::min(cursorMs + duration, patternEndsAt);
            if (endsAt > cursorMs)
            {
                m_pulses.push_back({
                    cursorMs,
                    endsAt,
                    std::clamp(pulse.lowFrequency, 0.0f, MaximumStrength),
                    std::clamp(pulse.highFrequency, 0.0f, MaximumStrength),
                    pattern.priority,
                });
            }
            cursorMs = endsAt;
            if (cursorMs >= patternEndsAt) break;
        }

        m_lastPublished[event] = nowMs;
        Update(nowMs);
        return true;
    }

    void HapticScheduler::Update(double nowMs)
    {
        std::erase_if(m_pulses, [nowMs](const ScheduledPulse& pulse) {
            return pulse.endsAtMs <= nowMs;
        });

        float low = 0.0f;
        float high = 0.0f;
        int activePriority = std::numeric_limits<int>::min();
        for (const ScheduledPulse& pulse : m_pulses)
        {
            if (pulse.startsAtMs <= nowMs && pulse.endsAtMs > nowMs)
                activePriority = std::max(activePriority, pulse.priority);
        }

        for (const ScheduledPulse& pulse : m_pulses)
        {
            if (pulse.startsAtMs <= nowMs && pulse.endsAtMs > nowMs)
            {
                if (pulse.priority == activePriority)
                {
                    low = std::max(low, pulse.lowFrequency);
                    high = std::max(high, pulse.highFrequency);
                }
            }
        }

        if (low == 0.0f && high == 0.0f)
        {
            if (m_outputActive)
            {
                m_output.Stop();
                m_outputActive = false;
                m_lastLow = 0;
                m_lastHigh = 0;
                m_outputScheduledUntilMs = 0.0;
            }
            return;
        }

        const auto sampleOutput = [this](double sampleMs, int& priority, float& sampleLow, float& sampleHigh)
        {
            priority = std::numeric_limits<int>::min();
            sampleLow = 0.0f;
            sampleHigh = 0.0f;
            for (const ScheduledPulse& pulse : m_pulses)
            {
                if (pulse.startsAtMs <= sampleMs && pulse.endsAtMs > sampleMs)
                    priority = std::max(priority, pulse.priority);
            }
            for (const ScheduledPulse& pulse : m_pulses)
            {
                if (pulse.priority == priority
                    && pulse.startsAtMs <= sampleMs
                    && pulse.endsAtMs > sampleMs)
                {
                    sampleLow = std::max(sampleLow, pulse.lowFrequency);
                    sampleHigh = std::max(sampleHigh, pulse.highFrequency);
                }
            }
        };

        double nextChangeMs = std::numeric_limits<double>::infinity();
        const auto considerChangeAt = [&](double candidateMs)
        {
            if (candidateMs <= nowMs || candidateMs >= nextChangeMs)
                return;

            int candidatePriority = 0;
            float candidateLow = 0.0f;
            float candidateHigh = 0.0f;
            sampleOutput(candidateMs, candidatePriority, candidateLow, candidateHigh);
            if (candidatePriority != activePriority
                || candidateLow != low
                || candidateHigh != high)
            {
                nextChangeMs = candidateMs;
            }
        };
        for (const ScheduledPulse& pulse : m_pulses)
        {
            considerChangeAt(pulse.startsAtMs);
            considerChangeAt(pulse.endsAtMs);
        }

        const float intensity = m_settings.intensityPercent / 100.0f;
        const std::uint16_t lowValue = ToMotorValue(low, intensity);
        const std::uint16_t highValue = ToMotorValue(high, intensity);
        const double remainingMs = std::isfinite(nextChangeMs)
            ? std::max(1.0, std::ceil(nextChangeMs - nowMs))
            : 1.0;
        if (m_outputActive
            && lowValue == m_lastLow
            && highValue == m_lastHigh
            && m_outputScheduledUntilMs >= nextChangeMs)
        {
            return;
        }

        m_output.Play(lowValue, highValue, static_cast<std::uint32_t>(remainingMs));
        m_outputActive = true;
        m_lastLow = lowValue;
        m_lastHigh = highValue;
        m_outputScheduledUntilMs = nowMs + remainingMs;
    }

    void HapticScheduler::Stop()
    {
        m_pulses.clear();
        m_lastPublished.clear();
        m_output.Stop();
        m_outputActive = false;
        m_lastLow = 0;
        m_lastHigh = 0;
        m_outputScheduledUntilMs = 0.0;
    }

    const HapticPattern& HapticScheduler::PatternFor(HapticEvent event)
    {
        return Patterns.at(Index(event));
    }

    bool HapticScheduler::IsCategoryEnabled(HapticCategory category) const
    {
        switch (category)
        {
        case HapticCategory::UserInterface: return m_settings.uiEnabled;
        case HapticCategory::Combat: return m_settings.combatEnabled;
        case HapticCategory::Transaction: return m_settings.transactionEnabled;
        }

        return false;
    }

    bool HapticScheduler::IsRateLimited(HapticEvent event, double nowMs)
    {
        const std::uint32_t limitMs = RateLimitMs(event);
        if (limitMs == 0) return false;

        const auto previous = m_lastPublished.find(event);
        return previous != m_lastPublished.end() && nowMs - previous->second < limitMs;
    }

    std::uint32_t HapticScheduler::RateLimitMs(HapticEvent event)
    {
        switch (event)
        {
        case HapticEvent::FocusMoved: return 70;
        case HapticEvent::SettingChanged: return 55;
        case HapticEvent::InputAcknowledged: return 35;
        case HapticEvent::NormalHit: return 40;
        case HapticEvent::CriticalHit:
        case HapticEvent::HeavyHit:
        case HapticEvent::Injured: return 60;
        default: return 0;
        }
    }
}
