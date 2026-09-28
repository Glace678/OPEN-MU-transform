#pragma once

// SceneManager.h - Top-level scene orchestration

#include "Core/Platform/WinCompat.h"
#include "Core/Time/FrameClock.h"

//=============================================================================
// Frame Timing State
//=============================================================================

class FrameTimingState : public Core::Time::FrameClock {
public:
    double lastWaterChange = 0.0;
};

// Global frame timing state
extern FrameTimingState g_frameTiming;

//=============================================================================
// Scene orchestration
//=============================================================================

void UpdateSceneState();
void RenderScene(HDC Hdc);
void MainScene(HDC hDC);

// FPS management (legacy - use g_frameTiming instead)
void SetTargetFps(double targetFps);
double GetTargetFps();

// Debug overlay controls
void SetShowDebugInfo(bool enabled);
void SetShowFpsCounter(bool enabled);
void ResetFrameStats();

struct FrameStatisticsSnapshot
{
    double actualFps = 0.0;
    double averageFps = 0.0;
    double onePercentLowFps = 0.0;
    double slowestFrameFps = 0.0;
    int sampleCount = 0;
};

FrameStatisticsSnapshot GetFrameStatistics();

// GLP-01: GL call/draw/buffer counters + GPU pass timers overlay, console-toggled via
// `$glstats on/off` (muConsoleDebug.cpp). Independent of $details/$fpscounter -- can be
// shown alongside either.
void SetShowGLStats(bool enabled);
