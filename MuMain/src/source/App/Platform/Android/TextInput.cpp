#include "TextInput.h"

#if defined(__ANDROID__)
#include <SDL3/SDL.h>
#include <jni.h>

namespace Platform::Android::Input
{
void SynchronizeArea(const SDL_Rect& area, bool password, bool multiline)
{
    auto* environment = static_cast<JNIEnv*>(SDL_GetAndroidJNIEnv());
    if (environment == nullptr) return;
    auto activity = static_cast<jobject>(SDL_GetAndroidActivity());
    if (activity == nullptr) return;
    jclass activityClass = environment->GetObjectClass(activity);
    jmethodID synchronize = activityClass == nullptr ? nullptr
        : environment->GetMethodID(activityClass, "syncTextInputArea", "(IIIIZZ)V");
    if (synchronize != nullptr)
    {
        environment->CallVoidMethod(activity, synchronize,
            area.x, area.y, area.w, area.h,
            static_cast<jboolean>(password), static_cast<jboolean>(multiline));
    }
    if (environment->ExceptionCheck())
    {
        environment->ExceptionClear();
        SDL_LogError(SDL_LOG_CATEGORY_APPLICATION, "Android text-input area synchronization failed");
    }
    if (activityClass != nullptr) environment->DeleteLocalRef(activityClass);
    environment->DeleteLocalRef(activity);
}
}
#endif
