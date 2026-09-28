#pragma once

// OpenGL ES accepts the modern shader syntax used by the renderer, but it
// requires the ES language version and explicit default precision qualifiers.
// Both Android and OpenHarmony expose OpenGL ES (via EGL), so the ES prefix
// applies to both.
#if defined(__ANDROID__) || defined(__OHOS__)
#define MU_GLSL_SOURCE_PREFIX \
    "#version 300 es\n"       \
    "precision highp float;\n" \
    "precision highp int;\n"
#else
#define MU_GLSL_SOURCE_PREFIX "#version 330 core\n"
#endif
