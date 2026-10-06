#pragma once

#include <vector>
#include "Core/Utilities/Singleton.h"

struct TimeCheck
{
    double		iBackupTime;	// backed-up time value
    int		iIndex;			// timer index
    bool	bTimeCheck;		// whether the timer is being checked
};

class CTimeCheck : public Singleton <CTimeCheck>
{
public:
    std::vector<TimeCheck> stl_Time;
    std::vector<TimeCheck>::iterator stl_Time_I;

    CTimeCheck();
    virtual ~CTimeCheck();

    int	 CheckIndex(int index);
    bool GetTimeCheck(int index, int DelayTime);
    void DeleteTimeIndex(int index);
};

#define g_Time CTimeCheck::GetSingleton()
