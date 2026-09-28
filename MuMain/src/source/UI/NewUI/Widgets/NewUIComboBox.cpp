//////////////////////////////////////////////////////////////////////
// NewUIComboBox.cpp: minimal click-to-open dropdown widget.
//////////////////////////////////////////////////////////////////////

#include "stdafx.h"
#include "UI/NewUI/Widgets/NewUIComboBox.h"
#include "UI/Legacy/UIControls.h"  // for g_pRenderText macro

#include "Core/Input/FocusNavigator.h"
#include "Core/Input/GamepadService.h"

#include "Render/Core/ImmediateRenderer.h"
#include "Render/Shaders/PassthroughShader.h"
#include "Render/Core/RenderConfig.h"
#include "Render/Textures/ZzzOpenglUtil.h"

// From ZzzOpenglUtil.cpp -- viewport scaling helpers.
float ConvertX(float x);
float ConvertY(float y);

extern unsigned int WindowWidth, WindowHeight;
extern int MouseWheel;

using namespace SEASON3B;

namespace
{
    // Visual style for the combo widget. Tweaked to match the option window's
    // existing arrow-button look (see NewUIOptionWindow::RenderContents).
    constexpr float BG_BRIGHTNESS_IDLE     = 0.05f;
    constexpr float BG_BRIGHTNESS_HOVER    = 0.15f;
    constexpr float BG_BRIGHTNESS_OPEN     = 0.10f;  // closed field shade while dropdown is open
    constexpr float SCROLLBAR_TRACK_BRIGHT = 0.02f;
    constexpr float SCROLLBAR_THUMB_BRIGHT = 0.35f;

    constexpr int TEXT_PAD_X     = 6;   // left padding inside rows
    // RenderText uses a font ascent that leaves a few pixels below the visual
    // center when placed at the row's top edge.  Keep the label centered in
    // the 16px field/list rows.
    constexpr int TEXT_PAD_Y     = 2;
    constexpr int ARROW_WIDTH    = 14;  // width reserved for the dropdown-arrow glyph
    constexpr int SCROLLBAR_WIDTH = 6;  // vertical scrollbar width inside the list
    constexpr std::uint64_t NAVIGATION_INITIAL_REPEAT_MS = 300;
    constexpr std::uint64_t NAVIGATION_REPEAT_MS = 110;

    // Draws a solid-color axis-aligned rectangle in UI coordinates.
    // Matches the raw-GL pattern used by NewUIOptionWindow.
    void DrawSolidRect(int x, int y, int w, int h, float brightness)
    {
        DisableTexture2D();

        float gx = ConvertX((float)x);
        float gy = ConvertY((float)y);
        float gw = ConvertX((float)w);
        float gh = ConvertY((float)h);
        gy = (float)WindowHeight - gy;

        IR::Begin(GL_QUADS);
        PassthroughShader::Instance().SetUseTexture(false);
        IR::Color4f(brightness, brightness, brightness, 1.0f);
        IR::Vertex2f(gx,      gy);
        IR::Vertex2f(gx + gw, gy);
        IR::Vertex2f(gx + gw, gy - gh);
        IR::Vertex2f(gx,      gy - gh);
        IR::End();

        glColor4f(1.0f, 1.0f, 1.0f, 1.0f);
        EnableTexture2D();
    }
}

void CNewUIComboBox::Setup(int x, int y, int width, int itemHeight,
                            const wchar_t* const* labels, int itemCount, int initialIdx,
                            int maxVisibleItems)
{
    m_X = x;
    m_Y = y;
    m_Width = width;
    m_ItemHeight = itemHeight;
    m_Labels = labels;
    m_ItemCount = itemCount;
    m_MaxVisibleItems = maxVisibleItems;
    m_ScrollOffset = 0;
    SetSelectedIndex(initialIdx);
    m_HighlightedIndex = m_SelectedIndex;
    m_bOpen = false;
    m_bLockHighlightedFocus = false;
    m_bRestoreFieldFocus = false;
    m_iHeldNavigationDelta = 0;
    m_uNextNavigationRepeatMs = 0;
}

void CNewUIComboBox::SetSelectedIndex(int idx)
{
    if (m_ItemCount <= 0)
    {
        m_SelectedIndex = 0;
        return;
    }
    if (idx < 0) idx = 0;
    if (idx >= m_ItemCount) idx = m_ItemCount - 1;
    m_SelectedIndex = idx;
    if (!m_bOpen)
        m_HighlightedIndex = m_SelectedIndex;
    ScrollToShowIndex(m_SelectedIndex);
}

void CNewUIComboBox::Close()
{
    m_bOpen = false;
    m_HighlightedIndex = m_SelectedIndex;
    m_bLockHighlightedFocus = false;
    m_bRestoreFieldFocus = false;
    m_iHeldNavigationDelta = 0;
}

void CNewUIComboBox::CloseAndRestoreFocus()
{
    Close();
    m_bRestoreFieldFocus = true;
}

int CNewUIComboBox::GetVisibleCount() const
{
    if (m_MaxVisibleItems <= 0 || m_MaxVisibleItems >= m_ItemCount)
        return m_ItemCount;
    return m_MaxVisibleItems;
}

int CNewUIComboBox::GetMaxScrollOffset() const
{
    const int visible = GetVisibleCount();
    return (m_ItemCount > visible) ? (m_ItemCount - visible) : 0;
}

bool CNewUIComboBox::IsScrollable() const
{
    return m_MaxVisibleItems > 0 && m_ItemCount > m_MaxVisibleItems;
}

void CNewUIComboBox::ClampScrollOffset()
{
    const int maxOffset = GetMaxScrollOffset();
    if (m_ScrollOffset < 0)            m_ScrollOffset = 0;
    if (m_ScrollOffset > maxOffset)    m_ScrollOffset = maxOffset;
}

void CNewUIComboBox::ScrollToShowIndex(int idx)
{
    if (!IsScrollable())
    {
        m_ScrollOffset = 0;
        return;
    }
    if (idx < m_ScrollOffset)
        m_ScrollOffset = idx;
    else if (idx >= m_ScrollOffset + m_MaxVisibleItems)
        m_ScrollOffset = idx - m_MaxVisibleItems + 1;
    ClampScrollOffset();
}

void CNewUIComboBox::Open()
{
    m_bOpen = true;
    m_HighlightedIndex = m_SelectedIndex;
    m_bLockHighlightedFocus = true;
    m_bRestoreFieldFocus = false;
    m_iHeldNavigationDelta = 0;
    ScrollToShowIndex(m_HighlightedIndex);
}

void CNewUIComboBox::SetHighlightedIndex(int idx)
{
    m_HighlightedIndex = ComboBoxNavigation::ClampIndex(idx, m_ItemCount);
    ScrollToShowIndex(m_HighlightedIndex);
}

void CNewUIComboBox::FocusField()
{
    auto& navigator = Core::Input::FocusNavigator::Instance();
    navigator.SetCurrent(this, ComboBoxNavigation::FieldFocusId);

    MouseX = m_X + m_Width / 2;
    MouseY = m_Y + m_ItemHeight / 2;
    Core::Input::GamepadService::Instance().SetPointerPosition(
        static_cast<float>(MouseX),
        static_cast<float>(MouseY));
}

void CNewUIComboBox::FocusHighlightedItem()
{
    const int row = m_HighlightedIndex - m_ScrollOffset;
    if (row < 0 || row >= GetVisibleCount())
        return;

    auto& navigator = Core::Input::FocusNavigator::Instance();
    const std::uint32_t focusId = ComboBoxNavigation::ItemFocusId(m_HighlightedIndex);
    navigator.Register(
        this,
        focusId,
        static_cast<float>(m_X),
        static_cast<float>(GetListY() + row * m_ItemHeight),
        static_cast<float>(IsScrollable() ? m_Width - SCROLLBAR_WIDTH : m_Width),
        static_cast<float>(m_ItemHeight));
    navigator.SetCurrent(this, focusId);

    const int rowWidth = IsScrollable() ? m_Width - SCROLLBAR_WIDTH : m_Width;
    MouseX = m_X + rowWidth / 2;
    MouseY = GetListY() + row * m_ItemHeight + m_ItemHeight / 2;
    Core::Input::GamepadService::Instance().SetPointerPosition(
        static_cast<float>(MouseX),
        static_cast<float>(MouseY));
}

int CNewUIComboBox::GetFocusedItemIndex() const
{
    const auto current = Core::Input::FocusNavigator::Instance().Current();
    if (!current.has_value()
        || current->owner != reinterpret_cast<std::uintptr_t>(this))
    {
        return -1;
    }
    return ComboBoxNavigation::ItemIndexFromFocusId(current->id, m_ItemCount);
}

void CNewUIComboBox::RegisterFocusNodes()
{
    auto& navigator = Core::Input::FocusNavigator::Instance();
    if (!m_bOpen)
    {
        navigator.Register(
            this,
            ComboBoxNavigation::FieldFocusId,
            static_cast<float>(m_X),
            static_cast<float>(m_Y),
            static_cast<float>(m_Width),
            static_cast<float>(m_ItemHeight));
        if (m_bRestoreFieldFocus)
        {
            FocusField();
            m_bRestoreFieldFocus = false;
        }
        return;
    }

    const bool navigationApplied = UpdateControllerNavigation();
    const int visible = GetVisibleCount();
    const int rowWidth = IsScrollable() ? m_Width - SCROLLBAR_WIDTH : m_Width;
    if (navigationApplied)
    {
        m_bLockHighlightedFocus = true;
    }

    if (m_bLockHighlightedFocus)
    {
        FocusHighlightedItem();
        m_bLockHighlightedFocus = false;
        return;
    }

    for (int row = 0; row < visible; ++row)
    {
        const int index = m_ScrollOffset + row;
        navigator.Register(
            this,
            ComboBoxNavigation::ItemFocusId(index),
            static_cast<float>(m_X),
            static_cast<float>(GetListY() + row * m_ItemHeight),
            static_cast<float>(rowWidth),
            static_cast<float>(m_ItemHeight));
    }

    const int focusedIndex = GetFocusedItemIndex();
    if (focusedIndex >= 0 && focusedIndex != m_HighlightedIndex)
        SetHighlightedIndex(focusedIndex);
}

int CNewUIComboBox::GetNavigationDelta() const
{
    const bool previous = SEASON3B::IsPress(VK_UP)
        || SEASON3B::IsRepeat(VK_UP)
        || SEASON3B::IsPress(VK_LEFT)
        || SEASON3B::IsRepeat(VK_LEFT);
    const bool next = SEASON3B::IsPress(VK_DOWN)
        || SEASON3B::IsRepeat(VK_DOWN)
        || SEASON3B::IsPress(VK_RIGHT)
        || SEASON3B::IsRepeat(VK_RIGHT);
    const bool previousPage = SEASON3B::IsPress(VK_PRIOR)
        || SEASON3B::IsRepeat(VK_PRIOR);
    const bool nextPage = SEASON3B::IsPress(VK_NEXT)
        || SEASON3B::IsRepeat(VK_NEXT);

    if ((previous || previousPage) == (next || nextPage))
        return 0;
    if (previousPage)
        return -GetVisibleCount();
    if (nextPage)
        return GetVisibleCount();
    return previous ? -1 : 1;
}

bool CNewUIComboBox::ShouldApplyNavigation(int delta)
{
    if (delta == 0)
    {
        m_iHeldNavigationDelta = 0;
        m_uNextNavigationRepeatMs = 0;
        return false;
    }

    const std::uint64_t nowMs = SDL_GetTicks();
    const bool newlyPressed = SEASON3B::IsPress(VK_UP)
        || SEASON3B::IsPress(VK_DOWN)
        || SEASON3B::IsPress(VK_LEFT)
        || SEASON3B::IsPress(VK_RIGHT)
        || SEASON3B::IsPress(VK_PRIOR)
        || SEASON3B::IsPress(VK_NEXT);
    if (newlyPressed || delta != m_iHeldNavigationDelta)
    {
        m_iHeldNavigationDelta = delta;
        m_uNextNavigationRepeatMs = nowMs + NAVIGATION_INITIAL_REPEAT_MS;
        return true;
    }
    if (nowMs < m_uNextNavigationRepeatMs)
        return false;

    m_uNextNavigationRepeatMs = nowMs + NAVIGATION_REPEAT_MS;
    return true;
}

bool CNewUIComboBox::UpdateControllerNavigation()
{
    if (!m_bOpen)
        return false;

    const int delta = GetNavigationDelta();
    const bool shouldApply = ShouldApplyNavigation(delta);

    // The global focus navigator runs before this widget's frame registration.
    // When it already moved between visible rows, the logical pointer identifies
    // that destination and must win over the local key repeater (otherwise one
    // D-pad press would skip two rows).
    if ((delta == -1 || delta == 1)
        && CheckMouseIn(m_X, GetListY(), m_Width, GetListHeight()))
    {
        const int pointerIndex = GetItemIndexAtMouse();
        if (pointerIndex >= 0 && pointerIndex != m_HighlightedIndex)
        {
            SetHighlightedIndex(pointerIndex);
            return false;
        }
    }

    if (!shouldApply)
        return false;

    const int nextIndex = (delta == -1 || delta == 1)
        ? ComboBoxNavigation::StepIndex(m_HighlightedIndex, m_ItemCount, delta)
        : ComboBoxNavigation::PageIndex(
            m_HighlightedIndex,
            m_ItemCount,
            GetVisibleCount(),
            delta < 0 ? -1 : 1);
    if (nextIndex == m_HighlightedIndex)
        return false;

    SetHighlightedIndex(nextIndex);
    return true;
}

bool CNewUIComboBox::CommitSelection(int idx)
{
    const int nextIndex = ComboBoxNavigation::ClampIndex(idx, m_ItemCount);
    const bool changed = nextIndex != m_SelectedIndex;
    m_SelectedIndex = nextIndex;
    CloseAndRestoreFocus();
    return changed;
}

int CNewUIComboBox::GetItemIndexAtMouse() const
{
    if (!m_bOpen)
        return -1;

    const int listY = GetListY();
    const int visible = GetVisibleCount();
    // Scrollbar eats the right edge when present
    const int rowWidth = IsScrollable() ? (m_Width - SCROLLBAR_WIDTH) : m_Width;

    for (int row = 0; row < visible; row++)
    {
        const int itemY = listY + row * m_ItemHeight;
        if (CheckMouseIn(m_X, itemY, rowWidth, m_ItemHeight))
            return m_ScrollOffset + row;
    }
    return -1;
}

bool CNewUIComboBox::IsMouseOverWidget() const
{
    if (CheckMouseIn(m_X, m_Y, m_Width, m_ItemHeight))
        return true;
    if (m_bOpen && CheckMouseIn(m_X, GetListY(), m_Width, GetListHeight()))
        return true;
    return false;
}

bool CNewUIComboBox::UpdateMouseEvent()
{
    if (m_bOpen && SEASON3B::IsPress(VK_RETURN))
        return CommitSelection(m_HighlightedIndex);

    // Mouse-wheel scrolling inside the open dropdown (consumed so it doesn't
    // leak to other handlers like the volume sliders).
    if (m_bOpen && IsScrollable() && MouseWheel != 0 &&
        CheckMouseIn(m_X, GetListY(), m_Width, GetListHeight()))
    {
        // MouseWheel sign convention matches NewUIOptionWindow sliders:
        // positive = wheel up = scroll toward earlier items.
        if (MouseWheel > 0) m_ScrollOffset--;
        else                m_ScrollOffset++;
        ClampScrollOffset();
        MouseWheel = 0;
    }

    if (!SEASON3B::IsPress(VK_LBUTTON))
        return false;

    const bool clickedClosedField = CheckMouseIn(m_X, m_Y, m_Width, m_ItemHeight);

    // Clicking the closed field toggles the dropdown.
    if (clickedClosedField)
    {
        if (m_bOpen)
            return CommitSelection(m_HighlightedIndex);
        Open();
        return false;
    }

    if (!m_bOpen)
        return false;

    // Dropdown open: did the click land on an item?
    const int hitIdx = GetItemIndexAtMouse();
    if (hitIdx < 0)
    {
        // Click landed outside the list (or on the scrollbar column) -- close
        // without changing selection.
        Close();
        return false;
    }

    return CommitSelection(hitIdx);
}

void CNewUIComboBox::Render()
{
    if (m_ItemCount <= 0 || m_Labels == nullptr)
        return;

    // --- Closed field ---
    const bool hoverClosed = CheckMouseIn(m_X, m_Y, m_Width, m_ItemHeight);
    const float closedBrightness = m_bOpen ? BG_BRIGHTNESS_OPEN
                                            : (hoverClosed ? BG_BRIGHTNESS_HOVER
                                                           : BG_BRIGHTNESS_IDLE);
    DrawSolidRect(m_X, m_Y, m_Width, m_ItemHeight, closedBrightness);

    g_pRenderText->SetFont(g_hFont);
    g_pRenderText->SetBgColor(0);
    g_pRenderText->SetTextColor(255, 255, 255, 255);
    g_pRenderText->RenderText(m_X + TEXT_PAD_X, m_Y + TEXT_PAD_Y, m_Labels[m_SelectedIndex]);

    // Dropdown arrow glyph (plain text -- no asset needed)
    g_pRenderText->SetTextColor(255, 230, 200, 255);
    g_pRenderText->RenderText(m_X + m_Width - ARROW_WIDTH, m_Y + TEXT_PAD_Y, m_bOpen ? L"^" : L"v");

    // --- Expanded list (drawn on top of anything below) ---
    if (!m_bOpen)
        return;

    const int listY = GetListY();
    const int visible = GetVisibleCount();
    const bool hasScrollbar = IsScrollable();
    const int rowWidth = hasScrollbar ? (m_Width - SCROLLBAR_WIDTH) : m_Width;

    g_pRenderText->SetTextColor(255, 255, 255, 255);

    for (int row = 0; row < visible; row++)
    {
        const int absIdx = m_ScrollOffset + row;
        const int itemY = listY + row * m_ItemHeight;
        const bool hoverItem = CheckMouseIn(m_X, itemY, rowWidth, m_ItemHeight);
        const bool isSelected = (absIdx == m_HighlightedIndex);

        float bright = BG_BRIGHTNESS_IDLE;
        if (hoverItem)      bright = BG_BRIGHTNESS_HOVER;
        else if (isSelected) bright = BG_BRIGHTNESS_OPEN;

        DrawSolidRect(m_X, itemY, rowWidth, m_ItemHeight, bright);
        g_pRenderText->RenderText(m_X + TEXT_PAD_X, itemY + TEXT_PAD_Y, m_Labels[absIdx]);
    }

    // --- Scrollbar (track + proportional thumb) ---
    if (hasScrollbar)
    {
        const int listHeight = GetListHeight();
        const int barX = m_X + m_Width - SCROLLBAR_WIDTH;

        // Track
        DrawSolidRect(barX, listY, SCROLLBAR_WIDTH, listHeight, SCROLLBAR_TRACK_BRIGHT);

        // Thumb: size proportional to (visible / total), position to (offset / maxOffset).
        int thumbHeight = (listHeight * visible) / m_ItemCount;
        if (thumbHeight < m_ItemHeight / 2)
            thumbHeight = m_ItemHeight / 2;

        const int maxOffset = GetMaxScrollOffset();
        const int thumbTravel = listHeight - thumbHeight;
        const int thumbY = listY + ((maxOffset > 0) ? (thumbTravel * m_ScrollOffset / maxOffset) : 0);

        DrawSolidRect(barX, thumbY, SCROLLBAR_WIDTH, thumbHeight, SCROLLBAR_THUMB_BRIGHT);
    }
}
