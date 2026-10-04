// Timer.cpp: implementation of the CTimer class.
//////////////////////////////////////////////////////////////////////
#include "stdafx.h"
#include "Timer.h"
#include <chrono>

CTimer::CTimer()
{
    m_startTime = Clock::now();
    m_absStartTime = m_startTime;
}

double CTimer::GetTimeElapsed()
{
    auto now = Clock::now();
    auto elapsed = std::chrono::duration<double, std::milli>(now - m_startTime);
    return elapsed.count(); // Return elapsed time in milliseconds
}

double CTimer::GetAbsTime()
{
    auto now = Clock::now();
    auto elapsed = std::chrono::duration<double, std::milli>(now - m_absStartTime);
    return elapsed.count(); // Return absolute time in milliseconds
}

void CTimer::ResetTimer()
{
    m_startTime = Clock::now(); // Reset start time to now
}

void CTimer2::SetTimer(unsigned int delay)
{
    m_delay = delay;
    m_timerStarted = false;
    m_timeReached = false;
}

unsigned int CTimer2::GetDelay() const
{
    return m_delay;
}

void CTimer2::ResetTimer()
{
    m_timerStarted = false;
    m_timeReached = false;
}

void CTimer2::UpdateTime()
{
    using SteadyClock = std::chrono::steady_clock;

    if (m_delay == 0)
    {
        m_timeReached = true;
        return;
    }

    m_timeReached = false;
    auto now = SteadyClock::now();

    if (!m_timerStarted)
    {
        m_startTickTime = now;
        m_timerStarted = true;
        return;
    }

    const auto elapsed = std::chrono::duration_cast<std::chrono::milliseconds>(now - m_startTickTime);
    if (elapsed.count() > static_cast<long long>(m_delay))
    {
        m_startTickTime = now;
        m_timeReached = true;
    }
}

bool CTimer2::IsTime() const
{
    return m_timeReached;
}
