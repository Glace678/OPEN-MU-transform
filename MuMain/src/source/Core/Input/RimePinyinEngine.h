#pragma once

#include <memory>
#include <string>
#include <vector>

namespace Core::Input
{
    struct PinyinComposition
    {
        std::wstring preedit;
        std::vector<std::wstring> candidates;
        int highlightedCandidate = 0;
        int pageNumber = 0;
        bool hasPreviousPage = false;
        bool hasNextPage = false;
    };

    class RimePinyinEngine
    {
    public:
        RimePinyinEngine();
        ~RimePinyinEngine();

        RimePinyinEngine(const RimePinyinEngine&) = delete;
        RimePinyinEngine& operator=(const RimePinyinEngine&) = delete;

        bool Initialize();
        void Shutdown();
        bool IsAvailable() const;
        const std::string& GetUnavailableReason() const;

        bool ProcessLetter(char letter);
        bool Backspace();
        bool SelectCandidate(int indexOnCurrentPage);
        bool ChangePage(bool backward);
        void Clear();

        const PinyinComposition& GetComposition() const;
        std::wstring TakeCommittedText();

    private:
        class Impl;
        std::unique_ptr<Impl> m_impl;
    };
}

