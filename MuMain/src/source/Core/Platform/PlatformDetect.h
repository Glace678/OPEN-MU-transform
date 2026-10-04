// PlatformDetect.h - unified platform classification.
//
// Different toolchains expose different pre-defined macros for the same
// platform family. This header classifies them once into a small set of
// MU_PLATFORM_* macros so the rest of the code does not have to repeat the
// platform-specific #if chains (and does not silently forget iOS when
// matching "mobile").
#pragma once

#if defined(__APPLE__)
#include <TargetConditionals.h>
#endif

// Phone/tablet platforms: Android, HarmonyOS and iOS.
//
// iOS is detected through Apple's TargetConditionals rather than __APPLE__
// alone, so macOS keeps its desktop classification.
#if defined(__ANDROID__) || defined(__OHOS__) \
    || (defined(__APPLE__) && defined(TARGET_OS_IPHONE) && TARGET_OS_IPHONE)
#define MU_PLATFORM_MOBILE 1
#else
#define MU_PLATFORM_MOBILE 0
#endif

// Desktop platforms: Windows, Linux and macOS.
#if !MU_PLATFORM_MOBILE
#define MU_PLATFORM_DESKTOP 1
#else
#define MU_PLATFORM_DESKTOP 0
#endif
