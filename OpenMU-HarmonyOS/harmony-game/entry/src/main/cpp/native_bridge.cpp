#include <string>
#include <unistd.h>
#include <napi/native_api.h>

// H-05: SetEnv / SetDataRoot previously ignored napi_get_cb_info and setenv/chdir
// return codes and always returned nullptr, so ArkTS could not tell that startup
// setup had failed and proceeded to loadContent anyway. Every parameter and
// system call is now checked and a NAPI error is thrown on failure, which the
// EntryAbility/Startup try/catch surfaces before the game routes to play.

static napi_value ThrowNapiError(napi_env env, const char* message) {
    napi_throw_error(env, nullptr, message);
    return nullptr;
}

static bool GetStringArg(napi_env env, napi_value value, std::string& out) {
    size_t needed = 0;
    if (napi_get_value_string_utf8(env, value, nullptr, 0, &needed) != napi_ok) {
        return false;
    }
    std::string buffer(needed, '\0');
    size_t copied = 0;
    if (napi_get_value_string_utf8(env, value, &buffer[0], needed + 1, &copied) != napi_ok) {
        return false;
    }
    buffer.resize(copied);
    out = buffer;
    return true;
}

static napi_value SetEnv(napi_env env, napi_callback_info info) {
    size_t argc = 2;
    napi_value args[2];
    napi_status status = napi_get_cb_info(env, info, &argc, args, nullptr, nullptr);
    if (status != napi_ok) {
        return ThrowNapiError(env, "SetEnv: failed to read arguments");
    }
    if (argc != 2) {
        return ThrowNapiError(env, "SetEnv expects exactly (name, value)");
    }
    std::string name;
    std::string value;
    if (!GetStringArg(env, args[0], name) || !GetStringArg(env, args[1], value)) {
        return ThrowNapiError(env, "SetEnv: arguments must be strings");
    }
    if (name.empty()) {
        return ThrowNapiError(env, "SetEnv: environment name must not be empty");
    }
    if (setenv(name.c_str(), value.c_str(), 1) != 0) {
        return ThrowNapiError(env, ("SetEnv: setenv failed for " + name).c_str());
    }
    napi_value result;
    napi_get_undefined(env, &result);
    return result;
}

static napi_value SetDataRoot(napi_env env, napi_callback_info info) {
    size_t argc = 1;
    napi_value args[1];
    napi_status status = napi_get_cb_info(env, info, &argc, args, nullptr, nullptr);
    if (status != napi_ok) {
        return ThrowNapiError(env, "SetDataRoot: failed to read arguments");
    }
    std::string path;
    if (argc == 1) {
        if (!GetStringArg(env, args[0], path) || path.empty()) {
            return ThrowNapiError(env, "SetDataRoot: path must be a non-empty string");
        }
        if (chdir(path.c_str()) != 0) {
            return ThrowNapiError(env, ("SetDataRoot: chdir failed: " + path).c_str());
        }
    } else {
        char cwd[4096];
        if (getcwd(cwd, sizeof(cwd)) == nullptr) {
            return ThrowNapiError(env, "SetDataRoot: getcwd failed");
        }
    }
    napi_value result;
    napi_get_undefined(env, &result);
    return result;
}

EXTERN_C_START
static napi_value Init(napi_env env, napi_value exports) {
    napi_property_descriptor desc[] = {
        {"setEnv", nullptr, SetEnv, nullptr, nullptr, nullptr, napi_default, nullptr},
        {"setDataRoot", nullptr, SetDataRoot, nullptr, nullptr, nullptr, napi_default, nullptr},
    };
    napi_define_properties(env, exports, sizeof(desc) / sizeof(desc[0]), desc);
    return exports;
}
EXTERN_C_END

static napi_module demoModule = {
    .nm_version = 1,
    .nm_flags = 0,
    .nm_filename = nullptr,
    .nm_register_func = Init,
    .nm_modname = "openmubridge",  // 107-01: must equal CMake target openmubridge (libopenmubridge.so) imported by ArkTS
    .nm_priv = ((void*)0),
    .reserved = {0},
};

extern "C" __attribute__((constructor)) void RegisterEntryModule(void) {
    napi_module_register(&demoModule);
}