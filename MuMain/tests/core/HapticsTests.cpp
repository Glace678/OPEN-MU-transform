#include <doctest.h>
#include "Network/Server/ServerResultHapticPolicy.h"

#include "Core/Haptics/Haptics.h"

#include <vector>

using namespace Core::Haptics;

namespace
{
    struct RumbleCall
    {
        std::uint16_t low;
        std::uint16_t high;
        std::uint32_t durationMs;
    };

    class FakeHapticOutput final : public IHapticOutput
    {
    public:
        bool SupportsRumble() const override { return supported; }
        void Play(std::uint16_t low, std::uint16_t high, std::uint32_t durationMs) override
        {
            calls.push_back({low, high, durationMs});
        }
        void Stop() override { ++stopCount; }

        bool supported = true;
        std::vector<RumbleCall> calls;
        int stopCount = 0;
    };
}

TEST_CASE("Purchase haptic uses two server-result pulses and the specified gap")
{
    FakeHapticOutput output;
    HapticScheduler scheduler(output);
    REQUIRE(scheduler.Publish(HapticEvent::PurchaseSucceeded, 100.0));
    REQUIRE(output.calls.size() == 1);
    CHECK(output.calls[0].durationMs == 55);
    CHECK(output.calls[0].low == doctest::Approx(0.25 * 0.70 * 65535).epsilon(0.001));
    CHECK(output.calls[0].high == doctest::Approx(0.45 * 0.70 * 65535).epsilon(0.001));

    scheduler.Update(155.0);
    CHECK(output.stopCount == 1);
    scheduler.Update(209.0);
    CHECK(output.calls.size() == 1);
    scheduler.Update(210.0);
    REQUIRE(output.calls.size() == 2);
    CHECK(output.calls[1].durationMs == 45);
}

TEST_CASE("Concurrent pulses mix by the stronger value on each motor")
{
    FakeHapticOutput output;
    HapticScheduler scheduler(output);
    REQUIRE(scheduler.Publish(HapticEvent::CriticalHit, 0.0));
    REQUIRE(scheduler.Publish(HapticEvent::Confirmed, 0.0));
    // The weaker UI pulse does not restart or shorten the critical-hit pulse.
    REQUIRE(output.calls.size() == 1);
    CHECK(output.calls.back().low == doctest::Approx(0.55 * 0.70 * 65535).epsilon(0.001));
    CHECK(output.calls.back().high == doctest::Approx(0.68 * 0.70 * 65535).epsilon(0.001));
}

TEST_CASE("High frequency events are rate limited and stop clears the queue")
{
    FakeHapticOutput output;
    HapticScheduler scheduler(output);
    CHECK(scheduler.Publish(HapticEvent::FocusMoved, 100.0));
    CHECK_FALSE(scheduler.Publish(HapticEvent::FocusMoved, 150.0));
    CHECK(scheduler.Publish(HapticEvent::FocusMoved, 170.0));

    scheduler.Stop();
    CHECK(output.stopCount >= 1);
    const auto callCount = output.calls.size();
    scheduler.Update(1000.0);
    CHECK(output.calls.size() == callCount);
}

TEST_CASE("Repeated equal-strength hits extend the hardware rumble deadline")
{
    FakeHapticOutput output;
    HapticScheduler scheduler(output);

    REQUIRE(scheduler.Publish(HapticEvent::NormalHit, 0.0));
    REQUIRE(output.calls.size() == 1);
    CHECK(output.calls.back().durationMs == 45);

    REQUIRE(scheduler.Publish(HapticEvent::NormalHit, 40.0));
    REQUIRE(output.calls.size() == 2);
    CHECK(output.calls.back().durationMs == 45);
}

TEST_CASE("UI test feedback cannot override a higher-priority combat event")
{
    FakeHapticOutput output;
    HapticScheduler scheduler(output);

    REQUIRE(scheduler.Publish(HapticEvent::Injured, 0.0));
    REQUIRE(scheduler.Publish(HapticEvent::LongTest, 0.0));
    REQUIRE(output.calls.size() == 1);
    CHECK(output.calls.back().low == doctest::Approx(0.65 * 0.70 * 65535).epsilon(0.001));
    CHECK(output.calls.back().high == doctest::Approx(0.25 * 0.70 * 65535).epsilon(0.001));
}

TEST_CASE("Disabling a category immediately clears active feedback")
{
    FakeHapticOutput output;
    HapticScheduler scheduler(output);
    REQUIRE(scheduler.Publish(HapticEvent::NormalHit, 0.0));

    auto settings = scheduler.GetSettings();
    settings.combatEnabled = false;
    scheduler.SetSettings(settings);
    CHECK(output.stopCount >= 1);

    const auto callCount = output.calls.size();
    scheduler.Update(10.0);
    CHECK(output.calls.size() == callCount);
}

TEST_CASE("Long test pulse is bounded to 500 milliseconds")
{
    FakeHapticOutput output;
    HapticScheduler scheduler(output);
    REQUIRE(scheduler.Publish(HapticEvent::LongTest, 0.0));
    REQUIRE(output.calls.size() == 1);
    CHECK(output.calls.front().durationMs == 500);
}

TEST_CASE("Server response haptics preserve protocol-specific result codes")
{
    using Network::Server::HapticForResult;
    using Network::Server::ResultOperation;
    CHECK(HapticForResult(ResultOperation::JoinServer, 1) == HapticEvent::Confirmed);
    CHECK(HapticForResult(ResultOperation::JoinServer, 0) == HapticEvent::OperationFailed);
    CHECK(HapticForResult(ResultOperation::CreateCharacter, 1) == HapticEvent::Confirmed);
    CHECK(HapticForResult(ResultOperation::CreateCharacter, 2) == HapticEvent::OperationFailed);
    CHECK(HapticForResult(ResultOperation::DeleteCharacter, 1) == HapticEvent::Confirmed);
    CHECK(HapticForResult(ResultOperation::DeleteCharacter, 3) == HapticEvent::OperationFailed);
    CHECK(HapticForResult(ResultOperation::Party, 5) == HapticEvent::Confirmed);
    CHECK(HapticForResult(ResultOperation::Party, 0) == HapticEvent::OperationFailed);
    CHECK(HapticForResult(ResultOperation::GuildJoin, 1) == HapticEvent::Confirmed);
    CHECK(HapticForResult(ResultOperation::GuildJoin, 0xA1) == HapticEvent::OperationFailed);
    for (int code : {1, 4, 5})
        CHECK(HapticForResult(ResultOperation::GuildLeave, code) == HapticEvent::Confirmed);
    CHECK(HapticForResult(ResultOperation::GuildLeave, 2) == HapticEvent::OperationFailed);
    CHECK(HapticForResult(ResultOperation::GuildCreate, 1) == HapticEvent::Confirmed);
    CHECK(HapticForResult(ResultOperation::GuildCreate, 6) == HapticEvent::OperationFailed);
    CHECK(HapticForResult(ResultOperation::LegacyQuest, 0) == HapticEvent::Confirmed);
    CHECK(HapticForResult(ResultOperation::LegacyQuest, 1) == HapticEvent::OperationFailed);
    CHECK(HapticForResult(ResultOperation::QuestComplete, 1) == HapticEvent::Confirmed);
    CHECK(HapticForResult(ResultOperation::QuestComplete, 2) == HapticEvent::OperationFailed);
    CHECK(HapticForResult(ResultOperation::QuestComplete, 3) == HapticEvent::OperationFailed);
}

TEST_CASE("Input acknowledgement does not replace combat or transaction feedback")
{
    CHECK(HapticScheduler::PatternFor(HapticEvent::InputAcknowledged).priority
        < HapticScheduler::PatternFor(HapticEvent::NormalHit).priority);
    CHECK(HapticScheduler::PatternFor(HapticEvent::InputAcknowledged).priority
        < HapticScheduler::PatternFor(HapticEvent::PurchaseSucceeded).priority);
    FakeHapticOutput output;
    HapticScheduler scheduler(output);
    CHECK(scheduler.Publish(HapticEvent::InputAcknowledged, 0.0));
    CHECK_FALSE(scheduler.Publish(HapticEvent::InputAcknowledged, 1.0));
    CHECK(scheduler.Publish(HapticEvent::InputAcknowledged, 40.0));
}

TEST_CASE("Test pulses bypass category switches but respect the master switch")
{
    FakeHapticOutput output;
    HapticScheduler scheduler(output);
    HapticSettings settings;
    settings.uiEnabled = false;
    scheduler.SetSettings(settings);

    CHECK_FALSE(scheduler.Publish(HapticEvent::Confirmed, 0.0));
    CHECK(scheduler.Publish(HapticEvent::ShortTest, 0.0));

    settings.enabled = false;
    scheduler.SetSettings(settings);
    CHECK_FALSE(scheduler.Publish(HapticEvent::LongTest, 100.0));
}
