#pragma once

#include <functional>
#include <array>
#include <set>
#include <string>
#include <thread>
#include <atomic>

#include "MuHelperData.h"

namespace MUHelper
{
class CMuHelper
{
public:
    CMuHelper()  = default;
    ~CMuHelper() = default;

public:
    static void CALLBACK TimerProc(HWND hwnd, UINT uMsg, UINT_PTR idEvent, DWORD dwTime);

    ConfigData GetConfig() const;
    void       Save(const ConfigData& config);
    void       Load(const ConfigData& config);
    void       Start();
    void       Stop();
    void       Toggle();
    void       TriggerStart();
    void       TriggerStop();
    bool       IsActive() { return m_bActive; }
    void       AddCost(int iCost) { m_iTotalCost += iCost; }
    int        GetTotalCost() { return m_iTotalCost; }

    void AddTarget(int iTargetId, bool bIsAttacking);
    void DeleteTarget(int iTargetId);
    void DeleteAllTargets();

    void AddItem(int iItemId, POINT posDropped);
    void DeleteItem(int iItemId);

private:
    void            WorkLoop(HWND hWnd, UINT uMsg, UINT_PTR idEvent, DWORD dwTime);
    void            Work();
    int             ActivatePet();
    int             Buff();
    int             BuffTarget(CHARACTER* pTargetChar, ActionSkillType iBuffSkill);
    int             RecoverHealth();
    int             Heal();
    int             HealSelf(ActionSkillType iHealingSkill);
    int             DrainLife();
    int             ConsumePotion();
    int             Attack();
    int             RepairEquipments();
    int             Regroup();
    ActionSkillType SelectAttackSkill();
    int             SimulateAttack(ActionSkillType iSkill);
    int             SimulateSkill(ActionSkillType iSkill, bool bTargetRequired, int iTarget);
    int             SimulateBasicAttack(int iTarget);
    int             SimulateComboAttack();
    int             GetNearestTarget();
    int             GetFarthestAttackingTarget();
    void            CleanupTargets();
    int             ComputeDistanceByRange(int iRange);
    int             ComputeDistanceFromTarget(CHARACTER* pTarget);
    int             ComputeDistanceBetween(POINT posA, POINT posB);
    int             SimulateMove(POINT posMove);
    int             ObtainItem();
    int             SelectItemToObtain();
    bool            ShouldObtainItem(int iItemId);
    ActionSkillType GetHealingSkill();
    ActionSkillType GetDrainLifeSkill();
    bool            HasAssignedBuffSkill();
    bool            IsSelfPositionSkill(ActionSkillType iSkill);

private:
    ConfigData        m_config{};
    POINT             m_posOriginal{0, 0};
    std::thread       m_timerThread;
    std::atomic<bool> m_bActive{false};
    std::set<int>     m_setTargets;
    std::set<int>     m_setTargetsAttacking;
    std::set<int>     m_setItems;
    int               m_iCurrentItem{MAX_ITEMS};
    int               m_iCurrentTarget{-1};
    int               m_iCurrentBuffIndex{0};
    int               m_iCurrentBuffPartyIndex{0};
    int               m_iCurrentHealPartyIndex{0};
    int               m_iComboState{0};
    ActionSkillType   m_iCurrentSkill{AT_SKILL_UNDEFINED};
    int               m_iHuntingDistance{0};
    int               m_iObtainingDistance{0};
    int               m_iLoopCounter{0};
    int               m_iSecondsElapsed{0};
    int               m_iSecondsAway{0};
    bool              m_bTimerActivatedBuffOngoing{false};
    bool              m_bPetActivated{false};
    int               m_iTotalCost{0};
};

extern CMuHelper g_MuHelper;
}