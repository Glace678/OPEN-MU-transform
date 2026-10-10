#pragma once
// Test-only CTimer2 double. The real NewUIItemMng.h embeds a CTimer2 member,
// so it must be a complete type here. Behaviour is irrelevant to ParseItemData.
struct CTimer2
{
    void SetTimer(int) {}
    void UpdateTime() {}
    bool IsTime() const { return false; }
};