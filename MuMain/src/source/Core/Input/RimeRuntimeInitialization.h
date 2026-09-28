#pragma once

#include <rime_api.h>

namespace Core::Input
{
    inline void InitializeRimeRuntime(RimeApi* api, const char* sharedData,
        const char* userData, const char* applicationName, bool fullMaintenance)
    {
        // The shipped pinyin schema needs only core/dict/gears. Distribution
        // plugins add unshipped dependencies and can crash during finalization.
        static const char* modules[] = { "default", nullptr };
        RIME_STRUCT(RimeTraits, traits);
        traits.shared_data_dir = sharedData;
        traits.user_data_dir = userData;
        traits.distribution_name = "MuMain Controller Keyboard";
        traits.distribution_code_name = "mumain-controller";
        traits.distribution_version = "1.0";
        traits.app_name = applicationName;
        traits.min_log_level = 2;
        traits.modules = modules;

        api->setup(&traits);
        api->initialize(&traits);
        if (RIME_API_AVAILABLE(api, start_maintenance)
            && RIME_API_AVAILABLE(api, join_maintenance_thread)
            && api->start_maintenance(fullMaintenance ? True : False))
        {
            api->join_maintenance_thread();
        }
    }
}
