#pragma once

#include "UI/Widgets/UIBaseDef.h"

inline float g_fScreenRate_x = 1.0f;
inline float g_fScreenRate_y = 1.0f;

// Rendering is not part of the headless CWin geometry test.
class CSprite
{
public:
    virtual ~CSprite() = default;
    static float ResolutionScaleX() { return g_fScreenRate_x > 0 ? g_fScreenRate_x : 1.0f; }
    static float ResolutionScaleY() { return g_fScreenRate_y > 0 ? g_fScreenRate_y : 1.0f; }
    void Create(int, int, int, int, SFrameCoord*, int, int, bool) {}
    void SetSizeArt(float, float, CHANGE_PRAM) {}
    void SetSize(int, int, CHANGE_PRAM) {}
    void SetPositionArt(float, float) {}
    void SetAlpha(BYTE) {}
    BYTE GetAlpha() const { return 0; }
    void SetColor(BYTE, BYTE, BYTE) {}
    void Show(bool) {}
    void Render() {}
    float GetScaleX() const { return ResolutionScaleX(); }
    float GetScaleY() const { return ResolutionScaleY(); }
};
