#include "Core/Input/FocusNavigator.h"

#include <algorithm>
#include <cmath>
#include <limits>

namespace Core::Input
{
    namespace
    {
        constexpr std::size_t NoIndex = std::numeric_limits<std::size_t>::max();

        bool IsFiniteRect(float x, float y, float width, float height)
        {
            return std::isfinite(x)
                && std::isfinite(y)
                && std::isfinite(width)
                && std::isfinite(height)
                && width > 0.0f
                && height > 0.0f;
        }
    }

    void FocusRegistry::BeginFrame()
    {
        m_hasPreviousCenter = false;
        if (m_hasCurrent)
        {
            const std::size_t current = FindByIdentity(m_currentOwner, m_currentId);
            if (current != NoIndex)
            {
                m_previousCenterX = m_nodes[current].centerX;
                m_previousCenterY = m_nodes[current].centerY;
                m_hasPreviousCenter = true;
            }
        }

        m_nodes.clear();
        m_collecting = true;
    }

    void FocusRegistry::EndFrame()
    {
        EndFrame(
            std::numeric_limits<float>::quiet_NaN(),
            std::numeric_limits<float>::quiet_NaN());
    }

    void FocusRegistry::EndFrame(float preferredX, float preferredY)
    {
        m_collecting = false;

        if (m_nodes.empty())
        {
            m_hasCurrent = false;
            m_currentOwner = 0;
            m_currentId = 0;
            m_hasPreviousCenter = false;
            return;
        }

        if (m_hasCurrent
            && FindByIdentity(m_currentOwner, m_currentId) != NoIndex)
        {
            m_hasPreviousCenter = false;
            return;
        }

        std::size_t fallback = 0;
        if (m_hasPreviousCenter)
        {
            fallback = FindNearest(m_previousCenterX, m_previousCenterY);
        }
        else if (std::isfinite(preferredX) && std::isfinite(preferredY))
        {
            fallback = FindNearest(preferredX, preferredY);
        }

        m_currentOwner = m_nodes[fallback].owner;
        m_currentId = m_nodes[fallback].id;
        m_hasCurrent = true;
        m_hasPreviousCenter = false;
    }

    bool FocusRegistry::Register(
        const void* owner,
        std::uint32_t id,
        float x,
        float y,
        float width,
        float height,
        bool enabled)
    {
        if (!m_collecting || owner == nullptr || !enabled
            || !IsFiniteRect(x, y, width, height))
        {
            return false;
        }

        const auto ownerKey = reinterpret_cast<std::uintptr_t>(owner);
        FocusNode node;
        node.owner = ownerKey;
        node.id = id;
        node.x = x;
        node.y = y;
        node.width = width;
        node.height = height;
        node.centerX = x + width * 0.5f;
        node.centerY = y + height * 0.5f;
        node.enabled = true;

        const std::size_t duplicate = FindByIdentity(ownerKey, id);
        if (duplicate != NoIndex)
        {
            m_nodes[duplicate] = node;
        }
        else
        {
            m_nodes.push_back(node);
        }
        return true;
    }

    bool FocusRegistry::Move(FocusDirection direction)
    {
        if (m_collecting || m_nodes.empty())
            return false;

        std::size_t current = NoIndex;
        if (m_hasCurrent)
            current = FindByIdentity(m_currentOwner, m_currentId);
        if (current == NoIndex)
        {
            m_currentOwner = m_nodes.front().owner;
            m_currentId = m_nodes.front().id;
            m_hasCurrent = true;
            return true;
        }

        const FocusNode& source = m_nodes[current];
        std::size_t best = NoIndex;
        float bestScore = std::numeric_limits<float>::infinity();
        float bestPrimaryDistance = std::numeric_limits<float>::infinity();

        for (std::size_t index = 0; index < m_nodes.size(); ++index)
        {
            if (index == current)
                continue;

            const FocusNode& candidate = m_nodes[index];
            const float dx = candidate.centerX - source.centerX;
            const float dy = candidate.centerY - source.centerY;

            float primary = 0.0f;
            float perpendicular = 0.0f;
            switch (direction)
            {
            case FocusDirection::Up:
                if (dy >= 0.0f) continue;
                primary = -dy;
                perpendicular = std::abs(dx);
                break;
            case FocusDirection::Down:
                if (dy <= 0.0f) continue;
                primary = dy;
                perpendicular = std::abs(dx);
                break;
            case FocusDirection::Left:
                if (dx >= 0.0f) continue;
                primary = -dx;
                perpendicular = std::abs(dy);
                break;
            case FocusDirection::Right:
                if (dx <= 0.0f) continue;
                primary = dx;
                perpendicular = std::abs(dy);
                break;
            }

            // Perpendicular displacement is weighted so a visually aligned
            // control wins over an equally distant diagonal control.
            const float score = primary * primary
                + 4.0f * perpendicular * perpendicular;
            if (score < bestScore
                || (score == bestScore && primary < bestPrimaryDistance))
            {
                best = index;
                bestScore = score;
                bestPrimaryDistance = primary;
            }
        }

        if (best == NoIndex)
            return false;

        m_currentOwner = m_nodes[best].owner;
        m_currentId = m_nodes[best].id;
        m_hasCurrent = true;
        return true;
    }

    bool FocusRegistry::SetCurrent(const void* owner, std::uint32_t id)
    {
        if (m_collecting || owner == nullptr)
            return false;

        const auto ownerKey = reinterpret_cast<std::uintptr_t>(owner);
        if (FindByIdentity(ownerKey, id) == NoIndex)
            return false;

        m_currentOwner = ownerKey;
        m_currentId = id;
        m_hasCurrent = true;
        return true;
    }

    bool FocusRegistry::SelectNearest(float x, float y)
    {
        if (m_collecting || m_nodes.empty()
            || !std::isfinite(x) || !std::isfinite(y))
        {
            return false;
        }

        const FocusNode& node = m_nodes[FindNearest(x, y)];
        m_currentOwner = node.owner;
        m_currentId = node.id;
        m_hasCurrent = true;
        return true;
    }

    std::optional<FocusNode> FocusRegistry::Current() const
    {
        if (!m_hasCurrent)
            return std::nullopt;

        const std::size_t current = FindByIdentity(m_currentOwner, m_currentId);
        if (current == NoIndex)
            return std::nullopt;
        return m_nodes[current];
    }

    void FocusRegistry::Clear()
    {
        m_nodes.clear();
        m_currentOwner = 0;
        m_currentId = 0;
        m_hasCurrent = false;
        m_hasPreviousCenter = false;
        m_collecting = false;
    }

    bool FocusRegistry::SameIdentity(
        const FocusNode& node,
        std::uintptr_t owner,
        std::uint32_t id)
    {
        return node.owner == owner && node.id == id;
    }

    std::size_t FocusRegistry::FindByIdentity(
        std::uintptr_t owner,
        std::uint32_t id) const
    {
        const auto found = std::find_if(
            m_nodes.begin(),
            m_nodes.end(),
            [owner, id](const FocusNode& node)
            {
                return SameIdentity(node, owner, id);
            });
        return found == m_nodes.end()
            ? NoIndex
            : static_cast<std::size_t>(found - m_nodes.begin());
    }

    std::size_t FocusRegistry::FindNearest(float x, float y) const
    {
        std::size_t best = 0;
        float bestDistance = std::numeric_limits<float>::infinity();
        for (std::size_t index = 0; index < m_nodes.size(); ++index)
        {
            const float dx = m_nodes[index].centerX - x;
            const float dy = m_nodes[index].centerY - y;
            const float distance = dx * dx + dy * dy;
            if (distance < bestDistance)
            {
                best = index;
                bestDistance = distance;
            }
        }
        return best;
    }

    FocusNavigator& FocusNavigator::Instance()
    {
        static FocusNavigator instance;
        return instance;
    }
}
