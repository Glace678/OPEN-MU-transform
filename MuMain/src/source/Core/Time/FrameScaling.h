#pragma once

#include <algorithm>
#include <cmath>

namespace Core::Time
{
    inline float SanitizeFrameScale(float frameScale)
    {
        return std::isfinite(frameScale) ? std::max(frameScale, 0.0f) : 0.0f;
    }

    // Converts a value expressed "per 25 FPS reference frame" to the current
    // rendered frame. At the legacy reference rate frameScale is exactly 1.
    inline float ScaleLinearStep(float referenceStep, float frameScale)
    {
        return referenceStep * SanitizeFrameScale(frameScale);
    }

    // Converts a legacy `value += (target - value) * blend` update without
    // changing its reference-frame behavior. Multiplying blend directly by dt
    // changes the curve; exponentiating the retained fraction does not.
    inline float ScaleBlendCoefficient(float referenceBlend, float frameScale)
    {
        const float blend = std::clamp(referenceBlend, 0.0f, 1.0f);
        return 1.0f - std::pow(1.0f - blend, SanitizeFrameScale(frameScale));
    }

    // Converts a per-reference-frame multiplicative damping factor (for
    // example velocity *= 0.8) to an arbitrary rendered-frame duration.
    inline float ScaleRetention(float referenceRetention, float frameScale)
    {
        return std::pow(std::max(referenceRetention, 0.0f), SanitizeFrameScale(frameScale));
    }

    class ReferenceTickAccumulator
    {
    public:
        int Advance(float frameScale)
        {
            m_accumulatedTicks += static_cast<double>(SanitizeFrameScale(frameScale));
            const int wholeTicks = static_cast<int>(std::floor(m_accumulatedTicks + 1.0e-5));
            m_accumulatedTicks -= static_cast<double>(wholeTicks);
            return wholeTicks;
        }

        void Reset() { m_accumulatedTicks = 0.0f; }

    private:
        double m_accumulatedTicks = 0.0;
    };
}
