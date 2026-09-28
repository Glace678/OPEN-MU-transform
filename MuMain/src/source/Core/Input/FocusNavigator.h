#pragma once

#include "Core/Input/GamepadTypes.h"

#include <cstddef>
#include <cstdint>
#include <optional>
#include <vector>

namespace Core::Input
{
    enum class FocusDirection
    {
        Up,
        Down,
        Left,
        Right,
    };

    class FocusRegistry
    {
    public:
        void BeginFrame();
        void EndFrame();
        void EndFrame(float preferredX, float preferredY);

        bool Register(
            const void* owner,
            std::uint32_t id,
            float x,
            float y,
            float width,
            float height,
            bool enabled = true);

        bool Move(FocusDirection direction);
        bool SetCurrent(const void* owner, std::uint32_t id);
        bool SelectNearest(float x, float y);

        std::optional<FocusNode> Current() const;
        std::size_t Size() const { return m_nodes.size(); }
        bool IsCollecting() const { return m_collecting; }
        void Clear();

    private:
        static bool SameIdentity(
            const FocusNode& node,
            std::uintptr_t owner,
            std::uint32_t id);
        std::size_t FindByIdentity(std::uintptr_t owner, std::uint32_t id) const;
        std::size_t FindNearest(float x, float y) const;

        std::vector<FocusNode> m_nodes;
        std::uintptr_t m_currentOwner = 0;
        std::uint32_t m_currentId = 0;
        float m_previousCenterX = 0.0f;
        float m_previousCenterY = 0.0f;
        bool m_hasCurrent = false;
        bool m_hasPreviousCenter = false;
        bool m_collecting = false;
    };

    class FocusNavigator
    {
    public:
        static FocusNavigator& Instance();

        void BeginFrame() { m_registry.BeginFrame(); }
        void EndFrame() { m_registry.EndFrame(); }
        void EndFrame(float preferredX, float preferredY)
        {
            m_registry.EndFrame(preferredX, preferredY);
        }

        bool Register(
            const void* owner,
            std::uint32_t id,
            float x,
            float y,
            float width,
            float height,
            bool enabled = true)
        {
            return m_registry.Register(owner, id, x, y, width, height, enabled);
        }

        bool Move(FocusDirection direction) { return m_registry.Move(direction); }
        bool SetCurrent(const void* owner, std::uint32_t id)
        {
            return m_registry.SetCurrent(owner, id);
        }
        bool SelectNearest(float x, float y) { return m_registry.SelectNearest(x, y); }
        std::optional<FocusNode> Current() const { return m_registry.Current(); }
        std::size_t Size() const { return m_registry.Size(); }
        void Clear() { m_registry.Clear(); }

    private:
        FocusRegistry m_registry;
    };
}
