#pragma once

#include "Core/Platform/PlatformDetect.h"

// OpenGL ES accepts the modern shader syntax used by the renderer, but it
// requires the ES language version and explicit default precision qualifiers.
// Android, OpenHarmony, and iOS all expose OpenGL ES 3.0, so the ES prefix
// applies across all mobile targets.
#if MU_PLATFORM_MOBILE
#define MU_GLSL_SOURCE_PREFIX \
    "#version 300 es\n" \
    "precision highp float;\n" \
    "precision highp int;\n"
#else
#define MU_GLSL_SOURCE_PREFIX "#version 330 core\n"
#endif
