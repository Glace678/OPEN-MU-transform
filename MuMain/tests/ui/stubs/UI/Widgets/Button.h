#pragma once

#include "Render/Sprites/Sprite.h"

class CButton : public CSprite
{
public:
    void Release() {}
    void SetActive(bool) {}
    void Update() {}
    void Render() {}
    BOOL CursorInObject() { return FALSE; }
};
