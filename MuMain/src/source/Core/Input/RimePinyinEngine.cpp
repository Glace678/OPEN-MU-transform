#include "stdafx.h"
#include "Core/Input/RimePinyinEngine.h"
#include "Core/Input/RimeRuntimeInitialization.h"

#include "rime_api.h"

#include <SDL3/SDL.h>
#include <algorithm>
#include <cstdlib>
#include <filesystem>
#include <utility>

#ifdef _WIN32
#include <windows.h>
#else
#include <codecvt>
#include <locale>
#endif

namespace Core::Input
{
    namespace
    {
        std::wstring Utf8ToWide(const char* value)
        {
            if (value == nullptr || *value == '\0')
                return {};
#ifdef _WIN32
            const int length = MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, value, -1, nullptr, 0);
            if (length <= 1)
                return {};
            std::wstring result(static_cast<std::size_t>(length), L'\0');
            MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, value, -1, result.data(), length);
            result.pop_back();
            return result;
#else
            std::wstring_convert<std::codecvt_utf8<wchar_t>> converter;
            return converter.from_bytes(value);
#endif
        }

        std::string PathToUtf8(const std::filesystem::path& path)
        {
#ifdef _WIN32
            const std::wstring wide = path.wstring();
            const int length = WideCharToMultiByte(CP_UTF8, 0, wide.c_str(), -1, nullptr, 0, nullptr, nullptr);
            if (length <= 1)
                return {};
            std::string result(static_cast<std::size_t>(length), '\0');
            WideCharToMultiByte(CP_UTF8, 0, wide.c_str(), -1, result.data(), length, nullptr, nullptr);
            result.pop_back();
            return result;
#else
            return path.string();
#endif
        }
    }

    class RimePinyinEngine::Impl
    {
    public:
        ~Impl()
        {
            Shutdown();
        }

        bool Initialize()
        {
            if (m_available)
                return true;

            const char* basePathValue = SDL_GetBasePath();
            const std::filesystem::path basePath = basePathValue && *basePathValue
                ? std::filesystem::u8path(basePathValue)
                : std::filesystem::current_path();

#ifdef _WIN32
            constexpr const char* LibraryName = "rime.dll";
#elif defined(__APPLE__)
            constexpr const char* LibraryName = "librime.dylib";
#else
            constexpr const char* LibraryName = "librime.so";
#endif
            const std::filesystem::path libraryCandidates[] = {
                basePath / LibraryName,
                basePath / "rime" / LibraryName,
            };
            for (const auto& candidate : libraryCandidates)
            {
                if (!std::filesystem::exists(candidate))
                    continue;
                m_library = SDL_LoadObject(PathToUtf8(candidate).c_str());
                if (m_library != nullptr)
                    break;
            }

            if (m_library == nullptr)
            {
                m_reason = "rime runtime is not installed next to the game";
                return false;
            }

            using GetApi = RimeApi* (*)();
            auto getApi = reinterpret_cast<GetApi>(SDL_LoadFunction(m_library, "rime_get_api"));
            if (getApi == nullptr || (m_api = getApi()) == nullptr)
            {
                m_reason = "rime_get_api is unavailable";
                Shutdown();
                return false;
            }
            if (!RIME_API_AVAILABLE(m_api, setup)
                || !RIME_API_AVAILABLE(m_api, initialize)
                || !RIME_API_AVAILABLE(m_api, finalize)
                || !RIME_API_AVAILABLE(m_api, create_session)
                || !RIME_API_AVAILABLE(m_api, destroy_session)
                || !RIME_API_AVAILABLE(m_api, process_key)
                || !RIME_API_AVAILABLE(m_api, get_commit)
                || !RIME_API_AVAILABLE(m_api, free_commit)
                || !RIME_API_AVAILABLE(m_api, get_context)
                || !RIME_API_AVAILABLE(m_api, free_context)
                || !RIME_API_AVAILABLE(m_api, select_schema)
                || !RIME_API_AVAILABLE(m_api, clear_composition)
                || !RIME_API_AVAILABLE(m_api, set_option)
                || !RIME_API_AVAILABLE(m_api, select_candidate_on_current_page)
                || !RIME_API_AVAILABLE(m_api, change_page))
            {
                m_reason = "rime runtime is missing required API functions";
                Shutdown();
                return false;
            }

            const std::filesystem::path sharedData = basePath / "rime" / "share";
            std::filesystem::path userData;
            if (const char* localData = std::getenv("OPENMU_LOCAL_DATA_DIR"); localData && *localData)
                userData = std::filesystem::u8path(localData) / "Rime";
            else
                userData = basePath / "userdata" / "rime";

            std::error_code error;
            std::filesystem::create_directories(userData, error);
            if (error)
            {
                m_reason = "rime user data directory cannot be created";
                Shutdown();
                return false;
            }

            m_sharedData = PathToUtf8(sharedData);
            m_userData = PathToUtf8(userData);
            InitializeRimeRuntime(m_api, m_sharedData.c_str(), m_userData.c_str(), "rime.mumain", false);
            m_initialized = true;

            m_session = m_api->create_session();
            if (m_session == 0 || !m_api->select_schema(m_session, "pinyin_simp_game"))
            {
                m_reason = "pinyin_simp_game schema could not be loaded";
                Shutdown();
                return false;
            }

            m_api->set_option(m_session, "ascii_mode", False);
            m_available = true;
            Refresh();
            return true;
        }

        void Shutdown()
        {
            m_available = false;
            m_composition = {};
            m_committed.clear();
            if (m_api && m_session != 0)
                m_api->destroy_session(m_session);
            m_session = 0;
            if (m_api && m_initialized)
                m_api->finalize();
            m_initialized = false;
            m_api = nullptr;
            if (m_library)
                SDL_UnloadObject(m_library);
            m_library = nullptr;
        }

        bool ProcessLetter(char letter)
        {
            if (!m_available || letter < 'a' || letter > 'z')
                return false;
            return ProcessKey(letter, 0);
        }

        bool Backspace()
        {
            constexpr int XKeyBackspace = 0xff08;
            return ProcessKey(XKeyBackspace, 0);
        }

        bool SelectCandidate(int indexOnCurrentPage)
        {
            if (!m_available
                || indexOnCurrentPage < 0
                || indexOnCurrentPage >= static_cast<int>(m_composition.candidates.size()))
            {
                return false;
            }
            const bool handled = m_api->select_candidate_on_current_page(
                m_session,
                static_cast<std::size_t>(indexOnCurrentPage)) != False;
            ReadCommit();
            Refresh();
            return handled;
        }

        bool ChangePage(bool backward)
        {
            if (!m_available)
                return false;
            const bool handled = m_api->change_page(m_session, backward ? True : False) != False;
            Refresh();
            return handled;
        }

        void Clear()
        {
            if (m_available)
                m_api->clear_composition(m_session);
            m_composition = {};
            m_committed.clear();
        }

        bool ProcessKey(int keyCode, int modifiers)
        {
            if (!m_available)
                return false;
            const bool handled = m_api->process_key(m_session, keyCode, modifiers) != False;
            ReadCommit();
            Refresh();
            return handled;
        }

        void ReadCommit()
        {
            RIME_STRUCT(RimeCommit, commit);
            if (m_api->get_commit(m_session, &commit))
            {
                m_committed += Utf8ToWide(commit.text);
                m_api->free_commit(&commit);
            }
        }

        void Refresh()
        {
            m_composition = {};
            if (!m_available)
                return;

            RIME_STRUCT(RimeContext, context);
            if (!m_api->get_context(m_session, &context))
                return;

            m_composition.preedit = Utf8ToWide(context.composition.preedit);
            m_composition.highlightedCandidate = std::max(0, context.menu.highlighted_candidate_index);
            m_composition.pageNumber = std::max(0, context.menu.page_no);
            m_composition.hasPreviousPage = context.menu.page_no > 0;
            m_composition.hasNextPage = context.menu.is_last_page == False;
            const int count = std::max(0, context.menu.num_candidates);
            m_composition.candidates.reserve(static_cast<std::size_t>(count));
            for (int i = 0; i < count; ++i)
                m_composition.candidates.push_back(Utf8ToWide(context.menu.candidates[i].text));
            m_api->free_context(&context);
        }

        std::wstring TakeCommittedText()
        {
            return std::exchange(m_committed, {});
        }

        SDL_SharedObject* m_library = nullptr;
        RimeApi* m_api = nullptr;
        RimeSessionId m_session = 0;
        bool m_initialized = false;
        bool m_available = false;
        std::string m_reason;
        std::string m_sharedData;
        std::string m_userData;
        PinyinComposition m_composition;
        std::wstring m_committed;
    };

    RimePinyinEngine::RimePinyinEngine()
        : m_impl(std::make_unique<Impl>())
    {
    }

    RimePinyinEngine::~RimePinyinEngine() = default;
    bool RimePinyinEngine::Initialize() { return m_impl->Initialize(); }
    void RimePinyinEngine::Shutdown() { m_impl->Shutdown(); }
    bool RimePinyinEngine::IsAvailable() const { return m_impl->m_available; }
    const std::string& RimePinyinEngine::GetUnavailableReason() const { return m_impl->m_reason; }
    bool RimePinyinEngine::ProcessLetter(char letter) { return m_impl->ProcessLetter(letter); }
    bool RimePinyinEngine::Backspace() { return m_impl->Backspace(); }
    bool RimePinyinEngine::SelectCandidate(int index) { return m_impl->SelectCandidate(index); }
    bool RimePinyinEngine::ChangePage(bool backward) { return m_impl->ChangePage(backward); }
    void RimePinyinEngine::Clear() { m_impl->Clear(); }
    const PinyinComposition& RimePinyinEngine::GetComposition() const { return m_impl->m_composition; }
    std::wstring RimePinyinEngine::TakeCommittedText() { return m_impl->TakeCommittedText(); }
}
