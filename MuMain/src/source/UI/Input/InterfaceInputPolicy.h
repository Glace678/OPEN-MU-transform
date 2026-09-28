#pragma once

namespace UI::Input
{
    struct LayerState
    {
        bool hasModalMessage{};
        bool skillSelectionOpen{};
        bool hasParty{};
        bool siegeActive{};
    };

    bool IsReadOnlyLayer(int key, bool hasModalMessage);
    bool BlocksWorldInput(int key, const LayerState& state);
}
