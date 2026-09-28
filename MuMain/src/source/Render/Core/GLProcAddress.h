#pragma once

#include <SDL3/SDL.h>

#if defined(__ANDROID__) || defined(__OHOS__)
extern "C" void* gl4es_GetProcAddress(const char* name);
#endif

namespace MuGL
{
// Rendering calls which carry GL state must pass through gl4es on mobile GPUs
// (Android and OpenHarmony both expose OpenGL ES through EGL).
// Keep this strict: silently falling back to native GLES would mix gl4es'
// virtual buffer/VAO names and program state with a different object domain.
inline SDL_FunctionPointer GetProcAddress(const char* name)
{
#if defined(__ANDROID__) || defined(__OHOS__)
    return reinterpret_cast<SDL_FunctionPointer>(gl4es_GetProcAddress(name));
#else
    return SDL_GL_GetProcAddress(name);
#endif
}

// Use only for an operation whose complete object lifetime stays in native GL.
inline SDL_FunctionPointer GetNativeProcAddress(const char* name)
{
    return SDL_GL_GetProcAddress(name);
}
}
