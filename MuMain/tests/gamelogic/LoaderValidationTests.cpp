// Tests for the extracted binary-loader validation rules (XC-17).
//
// The file loaders themselves (BuffScriptLoader, OpenMonsterSkillScript,
// dconfig CheckID_HistoryDay) are tightly coupled to FILE* + Win32 UI and
// cannot be instantiated in a test. The bounds introduced for their
// caller-supplied counts (XC-12, XC-6) therefore live in standalone headers
// that production code includes; these cases pin their exact boundaries so
// the clamp off-by-ones the report feared ("#20 修复引入的 clamp 边界差一")
// cannot regress silently.

#include "doctest.h"

#include "GameLogic/Buffs/BuffScriptValidation.h"
#include "Engine/Object/MonsterScriptValidation.h"
#include "Render/Textures/DayHistoryValidation.h"

#include <cstdint>
#include <limits>

TEST_SUITE("BuffScriptValidation")
{
    using BuffScriptValidation::IsValidRecordCount;
    using BuffScriptValidation::MaximumBuffRecords;

    TEST_CASE("valid record counts")
    {
        CHECK(IsValidRecordCount(1, 1));
        CHECK(IsValidRecordCount(1, 160));
        CHECK(IsValidRecordCount(100, sizeof(std::uint32_t)));
        CHECK(IsValidRecordCount(MaximumBuffRecords, 160));
    }

    TEST_CASE("zero records or zero struct size rejected")
    {
        CHECK_FALSE(IsValidRecordCount(0, 160));
        CHECK_FALSE(IsValidRecordCount(10, 0));
        CHECK_FALSE(IsValidRecordCount(0, 0));
    }

    TEST_CASE("record count above the cap rejected")
    {
        CHECK_FALSE(IsValidRecordCount(MaximumBuffRecords + 1, 160));
        CHECK_FALSE(IsValidRecordCount(std::numeric_limits<std::uint32_t>::max(), 1));
    }

    TEST_CASE("struct size times count overflow rejected")
    {
        // structSize * (count) would overflow uint32.
        CHECK_FALSE(IsValidRecordCount(2, std::numeric_limits<std::uint32_t>::max()));
        CHECK_FALSE(IsValidRecordCount(MaximumBuffRecords,
                                      std::numeric_limits<std::uint32_t>::max()));
    }
}

TEST_SUITE("MonsterScriptValidation")
{
    using MonsterScriptValidation::IsValidMonsterIndex;
    using MonsterScriptValidation::IsValidRecordCount;

    constexpr int ModelEnd = 500;

    TEST_CASE("valid file record counts")
    {
        CHECK(IsValidRecordCount(0, ModelEnd));
        CHECK(IsValidRecordCount(1, ModelEnd));
        CHECK(IsValidRecordCount(ModelEnd, ModelEnd));
    }

    TEST_CASE("invalid file record counts")
    {
        CHECK_FALSE(IsValidRecordCount(-1, ModelEnd));
        CHECK_FALSE(IsValidRecordCount(ModelEnd + 1, ModelEnd));
        CHECK_FALSE(IsValidRecordCount(1, 0));   // non-positive table
        CHECK_FALSE(IsValidRecordCount(0, -10));
    }

    TEST_CASE("valid monster indices")
    {
        CHECK(IsValidMonsterIndex(0, ModelEnd));
        CHECK(IsValidMonsterIndex(ModelEnd - 1, ModelEnd));
    }

    TEST_CASE("invalid monster indices")
    {
        // -1 is the decoded sentinel on a malformed record; ModelEnd itself is
        // one past the last slot.
        CHECK_FALSE(IsValidMonsterIndex(-1, ModelEnd));
        CHECK_FALSE(IsValidMonsterIndex(ModelEnd, ModelEnd));
        CHECK_FALSE(IsValidMonsterIndex(1000, ModelEnd));
    }
}

TEST_SUITE("DayHistoryValidation")
{
    using DayHistoryValidation::IsAppendPositionValid;
    using DayHistoryValidation::IsStoredCountValid;
    using DayHistoryValidation::MaximumHistoryEntries;

    TEST_CASE("stored counts readable in place")
    {
        CHECK(IsStoredCountValid(0));
        CHECK(IsStoredCountValid(1));
        // A full table is still readable for in-place updates.
        CHECK(IsStoredCountValid(MaximumHistoryEntries));
    }

    TEST_CASE("corrupt stored count rejected")
    {
        CHECK_FALSE(IsStoredCountValid(MaximumHistoryEntries + 1));
        CHECK_FALSE(IsStoredCountValid(MaximumHistoryEntries + 100));
    }

    TEST_CASE("appendable positions")
    {
        CHECK(IsAppendPositionValid(0));
        CHECK(IsAppendPositionValid(1));
        CHECK(IsAppendPositionValid(MaximumHistoryEntries - 1));
    }

    TEST_CASE("full-table position rejected")
    {
        // This is the XC-6 boundary: a file with 100 records plus a new ID
        // previously wrote days[100] out of bounds.
        CHECK_FALSE(IsAppendPositionValid(MaximumHistoryEntries));
        CHECK_FALSE(IsAppendPositionValid(MaximumHistoryEntries + 1));
    }
}
