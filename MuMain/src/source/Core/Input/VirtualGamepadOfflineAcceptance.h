#pragma once

#if defined(MU_ENABLE_VIRTUAL_GAMEPAD_TESTS) && defined(_WIN32)
#include <cstdlib>
#include <cstring>
#endif

namespace Core::Input
{
    inline bool IsVirtualGamepadOfflineLoginAcceptance()
    {
#if defined(MU_ENABLE_VIRTUAL_GAMEPAD_TESTS) && defined(_WIN32)
        const char* state = std::getenv("MU_VIRTUAL_GAMEPAD_STATE");
        const char* offline = std::getenv("MU_VIRTUAL_GAMEPAD_OFFLINE_LOGIN_SCENE");
        return state && state[0] && offline && std::strcmp(offline, "1") == 0;
#else
        return false;
#endif
    }
}
