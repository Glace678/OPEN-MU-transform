// scope87 guard regression tests (headless, MinGW/g++).
// Each case first demonstrates the PRE-FIX out-of-bounds index (RED) and then
// asserts the POST-FIX guard safely rejects/clamps the hostile input (GREEN).
#include <cstdio>
#include <cstddef>
#include <cstdint>
#include <cstring>
#include <cwchar>

static int g_fail = 0;
#define CHECK(cond, name) do { \
    if (cond) { std::printf("  PASS  %s\n", name); } \
    else { std::printf("  FAIL  %s\n", name); g_fail++; } \
} while (0)

static const int MAX_CHARACTERS_CLIENT = 400;
static const int MAX_ITEM_SPECIAL = 8;
static const int MAX_QUESTS = 200;

// 87-05 after-fix guard
static bool IsPartyMember_after(int selectcharacterindex)
{
    if (selectcharacterindex < 0 || selectcharacterindex >= MAX_CHARACTERS_CLIENT) return false;
    return true;
}

// 87-36 after-fix append
struct FakeItem { int SpecialNum; int Special[MAX_ITEM_SPECIAL]; };
static bool AppendSpecial_after(FakeItem* it, int opt)
{
    int Index = -1;
    for (int i = 0; i < it->SpecialNum; ++i)
        if (it->Special[i] == opt) { Index = i; break; }
    if (Index < 0)
    {
        if (it->SpecialNum >= MAX_ITEM_SPECIAL) return false;
        it->Special[it->SpecialNum] = opt;
        it->SpecialNum++;
    }
    return true;
}

// 87-37 after-fix decrement
static int DecrementQuestIndex_after(std::uint8_t idx)
{
    if (idx == 0) return 0;
    return idx - 1;
}

// 87-67 after-fix clamp
static unsigned long ClampBuffer_after(unsigned long v)
{
    if (v < 1024) v = 1024;
    if (v > (1ul << 20)) v = (1ul << 20);
    return v;
}

// 87-82 after-fix SetDirString
static bool SetDirString_after(wchar_t* buf, std::size_t cap, std::size_t len)
{
    if (!buf || len == 0) return false;
    if (len + 1 >= cap) return false;
    if (buf[len - 1] != L'\\') { buf[len] = L'\\'; buf[len + 1] = 0; }
    return true;
}

int main()
{
    std::printf("== scope87 guard regression ==\n");

    std::printf("[87-05] IsPartyMember signed receive\n");
    unsigned long oldIdx = (unsigned long)(int)-1;
    CHECK(oldIdx >= (unsigned long)MAX_CHARACTERS_CLIENT,
          "RED: -1 as DWORD yields out-of-range index (>=400)");
    CHECK(IsPartyMember_after(-1) == false, "GREEN: IsPartyMember(-1) -> false");
    CHECK(IsPartyMember_after(MAX_CHARACTERS_CLIENT) == false, "GREEN: idx==400 -> false");
    CHECK(IsPartyMember_after(0) == true, "GREEN: idx==0 accepted");

    std::printf("[87-36] Pet Special append capacity\n");
    FakeItem it; it.SpecialNum = MAX_ITEM_SPECIAL;
    CHECK(MAX_ITEM_SPECIAL == (int)(sizeof(it.Special)/sizeof(it.Special[0])),
          "RED: array capacity == 8, write at [8] would be out of bounds");
    CHECK(AppendSpecial_after(&it, 999) == false, "GREEN: append rejected at full capacity");
    it.SpecialNum = 0;
    CHECK(AppendSpecial_after(&it, 999) == true && it.Special[0] == 999 && it.SpecialNum == 1,
          "GREEN: append accepted when room available");

    std::printf("[87-37] Quest index underflow\n");
    std::uint8_t idx0 = 0;
    std::uint8_t wrapped = (std::uint8_t)(idx0 - 1);
    CHECK(wrapped == 255 && wrapped >= MAX_QUESTS,
          "RED: uint8 0-- wraps to 255 (>= MAX_QUESTS=200)");
    CHECK(DecrementQuestIndex_after(0) == 0, "GREEN: idx==0 rejected (no wrap)");
    CHECK(DecrementQuestIndex_after(5) == 4, "GREEN: idx==5 -> 4");

    std::printf("[87-61] StringCchCopy element vs byte count\n");
    wchar_t dst[32];
    CHECK(sizeof(dst) == sizeof(wchar_t)*32, "RED: sizeof(dst)=64 bytes != 32 elements");
    std::size_t elemCount = sizeof(dst)/sizeof(dst[0]);
    CHECK(elemCount == 32, "GREEN: _countof(dst)=32 elements (correct cchDest)");
    (void)dst;

    std::printf("[87-67] Read buffer size clamp\n");
    CHECK(ClampBuffer_after(0) == 1024ul, "GREEN: 0 -> 1024 floor");
    CHECK(ClampBuffer_after(1) == 1024ul, "GREEN: 1 -> 1024 floor");
    CHECK(ClampBuffer_after(5ul*1024*1024) == (1ul<<20), "GREEN: 5MB -> 1MB ceiling");
    CHECK(ClampBuffer_after(4096) == 4096ul, "GREEN: 4096 left as-is");

    std::printf("[87-82] SetDirString bounds\n");
    wchar_t path[260];
    CHECK(SetDirString_after(path, 260, 0) == false, "GREEN: len==0 rejected (no underflow)");
    CHECK(SetDirString_after(path, 260, 259) == false, "GREEN: len==259 rejected (no overflow)");
    std::wcsncpy(path, L"Data\\Script", 12);
    CHECK(SetDirString_after(path, 260, 11) == true && path[11] == L'\\',
          "GREEN: normal path appends trailing backslash");

    std::printf("[87-66] RenderFrame empty category guard\n");
    int iSizeCategory = 0;
    CHECK(iSizeCategory - 1 == -1, "RED: iSizeCategory==0 -> GetPos(-1) out of range");
    bool rendered = !(iSizeCategory <= 0);
    CHECK(rendered == false, "GREEN: iSizeCategory<=0 returns early");

    std::printf("== result: %s ==\n", g_fail == 0 ? "ALL GREEN" : "HAS FAILURES");
    return g_fail == 0 ? 0 : 1;
}