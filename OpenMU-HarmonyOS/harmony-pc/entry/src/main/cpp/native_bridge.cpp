// NAPI bridge for the OpenMU HarmonyOS PC / 2-in-1 shell. Identical role to the
// phone build: configure the process (data root + auto-login env) before SDL's
// OpenHarmony backend starts SDL_main. On PC the primary input is keyboard +
// mouse (normal SDL event path); touch on 2-in-1 devices still drives the shared
// MobileGestureMapper.
#include <napi/native_api.h>

#include <cstdlib>
#include <filesystem>
#include <string>

namespace
{
// H-05 (PC/2-in-1): the PC shell previously ignored napi_get_cb_info, setenv and
// std::filesystem::current_path return codes and always returned nullptr, so ArkTS
// could not distinguish "configured" from "failed" and proceeded to loadContent
// even when the data root was unusable. Mirror the phone build: check every result
// and throw a NAPI error so EntryAbility can intercept before routing to the game.
napi_value ThrowNapiError(napi_env env, const char* message)
{
    napi_throw_error(env, nullptr, message);
    return nullptr;
}

std::string Utf8FromValue(napi_env env, napi_value value)
{
    size_t length = 0;
    if (napi_get_value_string_utf8(env, value, nullptr, 0, &length) != napi_ok)
    {
        return {};
    }
    std::string result(length, '\0');
    size_t copied = 0;
    if (napi_get_value_string_utf8(env, value, result.data(), length + 1, &copied) != napi_ok)
    {
        return {};
    }
    result.resize(copied);
    return result;
}

napi_value SetEnv(napi_env env, napi_callback_info info)
{
    size_t argc = 2;
    napi_value argv[2] = {nullptr, nullptr};
    napi_status status = napi_get_cb_info(env, info, &argc, argv, nullptr, nullptr);
    if (status != napi_ok)
    {
        return ThrowNapiError(env, "SetEnv: failed to read arguments");
    }
    if (argc != 2)
    {
        return ThrowNapiError(env, "SetEnv expects exactly (name, value)");
    }
    const std::string name = Utf8FromValue(env, argv[0]);
    const std::string value = Utf8FromValue(env, argv[1]);
    if (name.empty())
    {
        return ThrowNapiError(env, "SetEnv: environment name must not be empty");
    }
    if (::setenv(name.c_str(), value.c_str(), 1) != 0)
    {
        return ThrowNapiError(env, ("SetEnv: setenv failed for " + name).c_str());
    }
    napi_value result;
    napi_get_undefined(env, &result);
    return result;
}

napi_value SetDataRoot(napi_env env, napi_callback_info info)
{
    size_t argc = 1;
    napi_value argv[1] = {nullptr};
    napi_status status = napi_get_cb_info(env, info, &argc, argv, nullptr, nullptr);
    if (status != napi_ok)
    {
        return ThrowNapiError(env, "SetDataRoot: failed to read arguments");
    }
    if (argc != 1)
    {
        return ThrowNapiError(env, "SetDataRoot expects exactly (root)");
    }
    const std::string root = Utf8FromValue(env, argv[0]);
    if (root.empty())
    {
        return ThrowNapiError(env, "SetDataRoot: root must not be empty");
    }
    std::error_code error;
    std::filesystem::current_path(std::filesystem::u8path(root), error);
    if (error)
    {
        return ThrowNapiError(env, ("SetDataRoot: chdir failed: " + root).c_str());
    }
    const std::string configPath = (std::filesystem::u8path(root) / "config.ini").string();
    if (::setenv("MU_CONFIG_FILE", configPath.c_str(), 1) != 0)
    {
        return ThrowNapiError(env, "SetDataRoot: setenv MU_CONFIG_FILE failed");
    }
    napi_value result;
    napi_get_undefined(env, &result);
    return result;
}

napi_value Init(napi_env env, napi_value exports)
{
    napi_property_descriptor descriptors[] = {
        {"setEnv", nullptr, SetEnv, nullptr, nullptr, nullptr, napi_default, nullptr},
        {"setDataRoot", nullptr, SetDataRoot, nullptr, nullptr, nullptr, napi_default, nullptr},
    };
    napi_define_properties(env, exports, sizeof(descriptors) / sizeof(descriptors[0]), descriptors);
    return exports;
}
} // namespace

static napi_module g_openmuBridgeModule = {
    .nm_version = 1,
    .nm_flags = 0,
    .nm_filename = nullptr,
    .nm_register_func = Init,
    .nm_modname = "openmubridge",
    .nm_priv = nullptr,
    .reserved = {0},
};

extern "C" __attribute__((constructor)) void RegisterOpenMuBridgeModule(void)
{
    napi_module_register(&g_openmuBridgeModule);
}
