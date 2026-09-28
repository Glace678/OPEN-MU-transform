#include "Core/Input/GamepadTypes.h"

#include <algorithm>
#include <array>
#include <cwctype>

namespace Core::Input
{
    namespace
    {
        constexpr std::array<const wchar_t*, static_cast<std::size_t>(GamepadControl::Count)> ControlNames = {
            L"South",
            L"East",
            L"West",
            L"North",
            L"Back",
            L"Start",
            L"LeftStick",
            L"RightStick",
            L"LeftShoulder",
            L"RightShoulder",
            L"DpadUp",
            L"DpadDown",
            L"DpadLeft",
            L"DpadRight",
            L"LeftTrigger",
            L"RightTrigger",
        };

        constexpr std::array<const wchar_t*, static_cast<std::size_t>(RemappableGamepadAction::Count)> BindingKeys = {
            L"BindConfirm",
            L"BindCancel",
            L"BindPrimaryAttack",
            L"BindContextAction",
            L"BindUseSkill",
            L"BindLockTarget",
            L"BindPreviousPage",
            L"BindNextPage",
            L"BindQuickItem1",
            L"BindQuickItem2",
            L"BindQuickItem3",
            L"BindQuickItem4",
            L"BindMap",
            L"BindMenu",
            L"BindAutoMove",
            L"BindNextTarget",
        };

        bool EqualsIgnoringCase(std::wstring_view left, std::wstring_view right)
        {
            return left.size() == right.size()
                && std::equal(left.begin(), left.end(), right.begin(), [](wchar_t a, wchar_t b) {
                    return std::towlower(a) == std::towlower(b);
                });
        }
    }

    const wchar_t* GamepadControlConfigName(GamepadControl control)
    {
        const auto index = static_cast<std::size_t>(control);
        return index < ControlNames.size() ? ControlNames[index] : L"South";
    }

    std::optional<GamepadControl> ParseGamepadControl(std::wstring_view value)
    {
        for (std::size_t i = 0; i < ControlNames.size(); ++i)
        {
            if (EqualsIgnoringCase(value, ControlNames[i]))
                return static_cast<GamepadControl>(i);
        }
        return std::nullopt;
    }

    const wchar_t* GamepadBindingConfigKey(RemappableGamepadAction action)
    {
        const auto index = static_cast<std::size_t>(action);
        return index < BindingKeys.size() ? BindingKeys[index] : BindingKeys[0];
    }

    bool HasGamepadBindingConflicts(const GamepadBindings& bindings)
    {
        std::array<bool, static_cast<std::size_t>(GamepadControl::Count)> used{};
        for (const GamepadControl control : bindings)
        {
            const auto index = static_cast<std::size_t>(control);
            if (index >= used.size() || used[index])
                return true;
            used[index] = true;
        }
        return false;
    }

    void NormalizeGamepadBindings(GamepadBindings& bindings)
    {
        const GamepadBindings defaults = DefaultGamepadBindings();
        std::array<bool, static_cast<std::size_t>(GamepadControl::Count)> used{};
        for (std::size_t action = 0; action < bindings.size(); ++action)
        {
            auto control = static_cast<std::size_t>(bindings[action]);
            if (control < used.size() && !used[control])
            {
                used[control] = true;
                continue;
            }

            control = static_cast<std::size_t>(defaults[action]);
            if (used[control])
            {
                const auto available = std::find(used.begin(), used.end(), false);
                control = static_cast<std::size_t>(std::distance(used.begin(), available));
            }
            bindings[action] = static_cast<GamepadControl>(control);
            used[control] = true;
        }
    }

    bool RebindGamepadAction(
        GamepadBindings& bindings,
        RemappableGamepadAction action,
        GamepadControl control)
    {
        NormalizeGamepadBindings(bindings);
        const auto actionIndex = static_cast<std::size_t>(action);
        const auto controlIndex = static_cast<std::size_t>(control);
        if (actionIndex >= bindings.size()
            || controlIndex >= static_cast<std::size_t>(GamepadControl::Count))
        {
            return false;
        }

        const GamepadControl previous = bindings[actionIndex];
        if (previous == control)
            return false;

        const auto occupied = std::find(bindings.begin(), bindings.end(), control);
        if (occupied != bindings.end())
            *occupied = previous;
        bindings[actionIndex] = control;
        return true;
    }
}
