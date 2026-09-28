#pragma once

#if defined(__ANDROID__)
#include <SDL3/SDL_rect.h>

namespace Platform::Android::Input
{
void SynchronizeArea(const SDL_Rect& area, bool password, bool multiline);
}
#endif
