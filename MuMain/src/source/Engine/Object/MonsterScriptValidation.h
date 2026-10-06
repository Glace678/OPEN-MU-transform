#pragma once

// Pure validation rules for the binary monster skill script. Extracted from
// OpenMonsterSkillScript (XC-12) so the bounds on the file-supplied record
// count and the per-record monster index can be unit tested on their own
// (XC-17).

namespace MonsterScriptValidation
{
    // True when the file may contain fileCount records: non-negative, and at
    // most one record per monster index in [0, modelEnd).
    constexpr bool IsValidRecordCount(int fileCount, int modelEnd)
    {
        return modelEnd > 0
            && fileCount >= 0
            && fileCount <= modelEnd;
    }

    // True when a record's monster index addresses a slot in MonsterSkill[].
    // A malformed record leaves the decoded index at its -1 sentinel.
    constexpr bool IsValidMonsterIndex(int index, int modelEnd)
    {
        return index >= 0 && index < modelEnd;
    }
}
