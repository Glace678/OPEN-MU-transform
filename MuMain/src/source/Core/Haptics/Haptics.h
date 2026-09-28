#pragma once

#include <cstdint>
#include <optional>
#include <unordered_map>
#include <vector>

namespace Core::Haptics
{
    enum class HapticCategory
    {
        UserInterface,
        Combat,
        Transaction,
    };

    enum class HapticEvent
    {
        FocusMoved,
        SettingChanged,
        Confirmed,
        Cancelled,
        ItemPickedUp,
        ItemDropped,
        PurchaseSucceeded,
        TransactionSucceeded,
        OperationFailed,
        NormalHit,
        CriticalHit,
        HeavyHit,
        Injured,
        Died,
        MajorEvent,
        ShortTest,
        LongTest,
        InputAcknowledged,
    };

    struct HapticPulse
    {
        float lowFrequency = 0.0f;
        float highFrequency = 0.0f;
        std::uint32_t durationMs = 0;
        std::uint32_t delayBeforeMs = 0;
    };

    struct HapticPattern
    {
        HapticCategory category = HapticCategory::UserInterface;
        int priority = 0;
        std::vector<HapticPulse> pulses;
    };

    struct HapticSettings
    {
        bool enabled = true;
        int intensityPercent = 70;
        bool combatEnabled = true;
        bool uiEnabled = true;
        bool transactionEnabled = true;
    };

    class IHapticOutput
    {
    public:
        virtual ~IHapticOutput() = default;
        virtual bool SupportsRumble() const = 0;
        virtual void Play(std::uint16_t lowFrequency, std::uint16_t highFrequency, std::uint32_t durationMs) = 0;
        virtual void Stop() = 0;
    };

    class HapticScheduler
    {
    public:
        explicit HapticScheduler(IHapticOutput& output);

        void SetSettings(const HapticSettings& settings);
        const HapticSettings& GetSettings() const { return m_settings; }
        bool Publish(HapticEvent event, double nowMs);
        void Update(double nowMs);
        void Stop();
        bool SupportsRumble() const { return m_output.SupportsRumble(); }

        static const HapticPattern& PatternFor(HapticEvent event);

    private:
        struct ScheduledPulse
        {
            double startsAtMs = 0.0;
            double endsAtMs = 0.0;
            float lowFrequency = 0.0f;
            float highFrequency = 0.0f;
            int priority = 0;
        };

        bool IsCategoryEnabled(HapticCategory category) const;
        bool IsRateLimited(HapticEvent event, double nowMs);
        static std::uint32_t RateLimitMs(HapticEvent event);

        IHapticOutput& m_output;
        HapticSettings m_settings;
        std::vector<ScheduledPulse> m_pulses;
        std::unordered_map<HapticEvent, double> m_lastPublished;
        std::uint16_t m_lastLow = 0;
        std::uint16_t m_lastHigh = 0;
        double m_outputScheduledUntilMs = 0.0;
        bool m_outputActive = false;
    };
}
