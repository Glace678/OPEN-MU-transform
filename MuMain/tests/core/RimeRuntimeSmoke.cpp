#include <rime_api.h>
#include "Core/Input/RimeRuntimeInitialization.h"

#ifdef _WIN32
#include <windows.h>
#else
#include <dlfcn.h>
#endif

#include <filesystem>
#include <iostream>
#include <string>

namespace
{
    std::string PathToUtf8(const std::filesystem::path& path)
    {
        const auto value = path.u8string();
        return {value.begin(), value.end()};
    }

    class RuntimeModule
    {
    public:
        explicit RuntimeModule(const std::filesystem::path& path)
#ifdef _WIN32
            : m_handle(LoadLibraryW(path.c_str()))
#else
            : m_handle(dlopen(path.c_str(), RTLD_NOW | RTLD_LOCAL))
#endif
        {
        }

        ~RuntimeModule()
        {
            if (!m_handle)
                return;
#ifdef _WIN32
            FreeLibrary(m_handle);
#else
            dlclose(m_handle);
#endif
        }

        RuntimeModule(const RuntimeModule&) = delete;
        RuntimeModule& operator=(const RuntimeModule&) = delete;

        RimeApi* GetApi() const
        {
            if (!m_handle)
                return nullptr;
            using GetApiFunction = RimeApi* (*)();
#ifdef _WIN32
            auto getApi = reinterpret_cast<GetApiFunction>(GetProcAddress(m_handle, "rime_get_api"));
#else
            auto getApi = reinterpret_cast<GetApiFunction>(dlsym(m_handle, "rime_get_api"));
#endif
            return getApi ? getApi() : nullptr;
        }

    private:
#ifdef _WIN32
        HMODULE m_handle = nullptr;
#else
        void* m_handle = nullptr;
#endif
    };

    bool HasRequiredApi(RimeApi* api)
    {
        return RIME_API_AVAILABLE(api, setup) && RIME_API_AVAILABLE(api, initialize)
            && RIME_API_AVAILABLE(api, finalize) && RIME_API_AVAILABLE(api, start_maintenance)
            && RIME_API_AVAILABLE(api, join_maintenance_thread) && RIME_API_AVAILABLE(api, create_session)
            && RIME_API_AVAILABLE(api, destroy_session) && RIME_API_AVAILABLE(api, select_schema)
            && RIME_API_AVAILABLE(api, process_key) && RIME_API_AVAILABLE(api, get_context)
            && RIME_API_AVAILABLE(api, free_context) && RIME_API_AVAILABLE(api, get_commit)
            && RIME_API_AVAILABLE(api, free_commit) && RIME_API_AVAILABLE(api, clear_composition)
            && RIME_API_AVAILABLE(api, set_option) && RIME_API_AVAILABLE(api, change_page)
            && RIME_API_AVAILABLE(api, select_candidate_on_current_page);
    }

    int Fail(const char* message, int code)
    {
        std::cerr << message << '\n';
        return code;
    }
}

#ifdef _WIN32
int wmain(int argc, wchar_t** argv)
#else
int main(int argc, char** argv)
#endif
{
    if (argc != 4)
        return Fail("usage: rime_runtime_smoke <native-library> <shared-data> <user-data>", 2);

    std::error_code directoryError;
    std::filesystem::create_directories(argv[3], directoryError);
    if (directoryError)
        return Fail("failed to create Rime user-data directory", 3);

    const RuntimeModule module(argv[1]);
    RimeApi* api = module.GetApi();
    if (!HasRequiredApi(api))
    {
        return Fail("native Rime library or required controller API was unavailable", 5);
    }

    const std::string sharedData = PathToUtf8(argv[2]);
    const std::string userData = PathToUtf8(argv[3]);
    Core::Input::InitializeRimeRuntime(api, sharedData.c_str(), userData.c_str(), "rime.mumain.test", true);

    const RimeSessionId session = api->create_session();
    if (session == 0 || !api->select_schema(session, "pinyin_simp_game"))
    {
        if (session != 0)
            api->destroy_session(session);
        api->finalize();
        return Fail("failed to select pinyin_simp_game", 6);
    }

    for (const char key : std::string("nihao"))
    {
        if (!api->process_key(session, key, 0))
        {
            api->destroy_session(session);
            api->finalize();
            return Fail("Rime rejected pinyin input", 7);
        }
    }

    RIME_STRUCT(RimeContext, context);
    if (!api->get_context(session, &context) || context.menu.num_candidates <= 0)
    {
        api->destroy_session(session);
        api->finalize();
        return Fail("Rime returned no candidate for nihao", 8);
    }
    const std::string firstCandidate = context.menu.candidates[0].text;
    api->free_context(&context);

    if (!api->select_candidate_on_current_page(session, 0))
    {
        api->destroy_session(session);
        api->finalize();
        return Fail("failed to select the first candidate", 9);
    }

    RIME_STRUCT(RimeCommit, commit);
    if (!api->get_commit(session, &commit) || commit.text == nullptr || *commit.text == '\0')
    {
        api->destroy_session(session);
        api->finalize();
        return Fail("Rime did not commit the selected candidate", 10);
    }
    const std::string committed = commit.text;
    api->free_commit(&commit);
    api->destroy_session(session);
    api->finalize();

    std::cout << "candidate=" << firstCandidate << " committed=" << committed << '\n';
    return 0;
}
