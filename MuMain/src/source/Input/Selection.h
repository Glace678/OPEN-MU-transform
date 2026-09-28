#pragma once

// Mouse picking: each frame this resolves what the cursor is over (monster,
// player, NPC, item, operable object) and updates the Selected* globals that
// the rest of the client reads. Extracted from ZzzInterface.cpp.
namespace Input::Selection
{
    void SelectObjects();

    // Selects the next visible combat target and returns a screen-space point
    // over it. The caller moves the logical gamepad pointer to this point so
    // the regular mouse-picking code remains the single source of truth.
    bool CycleGamepadTarget(int& outScreenX, int& outScreenY);

    // Reprojects the current gamepad-cycle target as it moves. Returns false
    // when that target is no longer alive, visible, or on screen.
    bool ProjectGamepadTarget(int& outScreenX, int& outScreenY);
}
