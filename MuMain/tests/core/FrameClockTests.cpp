#include <doctest.h>

#include "Core/Time/FrameClock.h"
#include "Core/Time/FrameScaling.h"
#include <array>
#include <cmath>

using namespace Core::Time;

TEST_CASE("Display maximum stays distinct from the active refresh and follows monitor changes")
{
    FrameTimingSettings settings;
    REQUIRE(settings.mode == FrameRateMode::DisplayMaximum);
    auto policy = ResolveFramePolicy(settings, {60.0, true}, true, false, {239.76, true});
    CHECK(policy.requestedFrameRate == doctest::Approx(239.76));
    CHECK(policy.effectiveFrameRate == doctest::Approx(239.76));
    CHECK_FALSE(settings.verticalSync);
    settings.verticalSync = true;
    policy = ResolveFramePolicy(settings, {60.0, true}, true, true, {239.76, true});
    CHECK(policy.effectiveFrameRate == doctest::Approx(60.0));
    CHECK(policy.reason == FrameLimitReason::VerticalSync);
    policy = ResolveFramePolicy(settings, {143.98, true}, true, false, {165.0, true});
    CHECK(policy.requestedFrameRate == doctest::Approx(165.0));
    policy = ResolveFramePolicy(settings, {143.98, true}, true, false);
    CHECK(policy.requestedFrameRate == doctest::Approx(143.98));
}

TEST_CASE("Reference movement, interpolation and integer ticks are frame-rate independent")
{
    constexpr std::array<int, 9> rates{25, 30, 60, 90, 120, 144, 180, 240, 360};
    for (int fps : rates)
    {
        ReferenceTickAccumulator accumulator;
        const float scale = 25.0f / fps;
        float distance = 0.0f;
        float blended = 0.0f;
        float retained = 1.0f;
        int ticks = 0;
        for (int frame = 0; frame < fps; ++frame)
        {
            distance += ScaleLinearStep(4.0f, scale);
            blended += (1.0f - blended) * ScaleBlendCoefficient(0.1f, scale);
            retained *= ScaleRetention(0.8f, scale);
            ticks += accumulator.Advance(scale);
        }
        CHECK(distance == doctest::Approx(100.0f).epsilon(0.00001));
        CHECK(blended == doctest::Approx(1.0f - std::pow(0.9f, 25.0f)).epsilon(0.00001));
        CHECK(retained == doctest::Approx(std::pow(0.8f, 25.0f)).epsilon(0.00002));
        CHECK(ticks == 25);
    }
    CHECK(ScaleLinearStep(3.0f, -1.0f) == 0.0f);
    CHECK(ScaleLinearStep(3.0f, std::nanf("")) == 0.0f);
    CHECK(ScaleBlendCoefficient(0.1f, -1.0f) == 0.0f);
    CHECK(ScaleRetention(0.8f, -1.0f) == 1.0f);
}

TEST_CASE("FrameClock keeps an absolute deadline without cumulative drift")
{
    FrameClock clock;
    clock.SetTargetFps(60.0);
    clock.UpdateCurrentTime(0.0);
    REQUIRE(clock.ShouldRenderNextFrame());
    clock.MarkFrameRendered();

    constexpr double interval = 1000.0 / 60.0;
    for (int frame = 1; frame <= 600; ++frame)
    {
        clock.UpdateCurrentTime(frame * interval - 0.01);
        CHECK_FALSE(clock.ShouldRenderNextFrame());
        clock.UpdateCurrentTime(frame * interval);
        CHECK(clock.ShouldRenderNextFrame());
        clock.MarkFrameRendered();
    }

    CHECK(clock.GetTimeUntilNextFrame() == doctest::Approx(interval).epsilon(0.000001));
}

TEST_CASE("FrameClock preserves phase for an ordinary late frame")
{
    FrameClock clock;
    clock.SetTargetFps(60.0);
    clock.UpdateCurrentTime(0.0);
    clock.MarkFrameRendered();

    clock.UpdateCurrentTime(17.2);
    REQUIRE(clock.ShouldRenderNextFrame());
    clock.MarkFrameRendered();

    CHECK(clock.GetTimeUntilNextFrame() == doctest::Approx(33.333333333 - 17.2).epsilon(0.000001));
}

TEST_CASE("FrameClock resynchronizes after a stall instead of catch-up rendering")
{
    FrameClock clock;
    clock.SetTargetFps(60.0);
    clock.UpdateCurrentTime(0.0);
    clock.MarkFrameRendered();

    clock.UpdateCurrentTime(100.0);
    REQUIRE(clock.ShouldRenderNextFrame());
    clock.MarkFrameRendered();

    CHECK_FALSE(clock.ShouldRenderNextFrame());
    CHECK(clock.GetTimeUntilNextFrame() == doctest::Approx(1000.0 / 60.0));
}

TEST_CASE("Frame policy reports display, VSync, and background constraints")
{
    FrameTimingSettings settings;
    settings.mode = FrameRateMode::FollowDisplay;
    settings.verticalSync = true;
    settings.backgroundFrameRate = 30.0;

    const auto foreground = ResolveFramePolicy(settings, {143.98, true}, true, true);
    CHECK(foreground.requestedFrameRate == doctest::Approx(143.98));
    CHECK(foreground.effectiveFrameRate == doctest::Approx(143.98));
    CHECK(foreground.verticalSyncPacesFrames);
    CHECK(foreground.reason == FrameLimitReason::VerticalSync);

    const auto background = ResolveFramePolicy(settings, {143.98, true}, false, true);
    CHECK(background.effectiveFrameRate == doctest::Approx(30.0));
    CHECK(background.reason == FrameLimitReason::Background);
}

TEST_CASE("FrameClock has no long-term drift at every supported fixed rate")
{
    constexpr double rates[] = {30.0, 60.0, 90.0, 120.0, 144.0, 180.0};
    for (const double rate : rates)
    {
        FrameClock clock;
        clock.SetTargetFps(rate);
        clock.UpdateCurrentTime(0.0);
        clock.MarkFrameRendered();

        const double interval = 1000.0 / rate;
        const int frames = static_cast<int>(rate * 30.0);
        for (int frame = 1; frame <= frames; ++frame)
        {
            clock.UpdateCurrentTime(frame * interval);
            REQUIRE(clock.ShouldRenderNextFrame());
            clock.MarkFrameRendered();
        }

        CHECK(clock.GetTimeUntilNextFrame() == doctest::Approx(interval).epsilon(0.00001));
    }
}

TEST_CASE("Frame policy preserves fractional refresh and only lets VSync cap at the display")
{
    FrameTimingSettings settings;
    settings.mode = FrameRateMode::Fixed;
    settings.fixedFrameRate = 90.0;
    settings.verticalSync = true;

    const auto belowDisplay = ResolveFramePolicy(settings, {143.98, true}, true, true);
    CHECK(belowDisplay.requestedFrameRate == doctest::Approx(90.0));
    CHECK(belowDisplay.effectiveFrameRate == doctest::Approx(90.0));
    CHECK(belowDisplay.verticalSyncEngaged);
    CHECK_FALSE(belowDisplay.verticalSyncPacesFrames);
    CHECK(HasUnevenVSyncCadence(belowDisplay));
    CHECK(belowDisplay.reason == FrameLimitReason::FrameLimiter);

    settings.fixedFrameRate = 60.0;
    const auto evenCadence = ResolveFramePolicy(settings, {120.0, true}, true, true);
    CHECK_FALSE(HasUnevenVSyncCadence(evenCadence));

    settings.fixedFrameRate = 180.0;
    const auto aboveDisplay = ResolveFramePolicy(settings, {143.98, true}, true, true);
    CHECK(aboveDisplay.requestedFrameRate == doctest::Approx(180.0));
    CHECK(aboveDisplay.effectiveFrameRate == doctest::Approx(143.98));
    CHECK(aboveDisplay.verticalSyncEngaged);
    CHECK(aboveDisplay.verticalSyncPacesFrames);
    CHECK_FALSE(HasUnevenVSyncCadence(aboveDisplay));
    CHECK(aboveDisplay.reason == FrameLimitReason::VerticalSync);
}
