#include "stdafx.h"

#ifdef _EDITOR

#include "MuEditorConsoleUI.h"

#include "imgui.h"
#include <ctime>
#include <sstream>
#include <iomanip>
#include <mutex>
#include <cstdarg>
#include <cstdio>
#include <cwchar>
#include <filesystem>
#include <vector>
#include <algorithm>

#include "../MuEditor/Core/MuEditorCore.h"

// Mutex for thread-safe console access
static std::mutex g_consoleMutex;

namespace
{
int FormatWideLog(wchar_t* buffer, std::size_t bufferCount, const wchar_t* format, va_list args)
{
    if (!buffer || bufferCount == 0 || !format)
    {
        if (buffer && bufferCount > 0)
            buffer[0] = L'\0';
        return -1;
    }

#ifdef _MSC_VER
    int result = _vsnwprintf_s(buffer, bufferCount, _TRUNCATE, format, args);
#else
    int result = std::vswprintf(buffer, bufferCount, format, args);
#endif
    buffer[bufferCount - 1] = L'\0';
    return result;
}

void ForwardFormattedWideToConsole(const wchar_t* buffer)
{
    if (!buffer || buffer[0] == L'\0')
        return;

    // 4096 UTF-16 code units expand to at most 12,288 UTF-8 bytes (+1 NUL)
    char      utf8Buffer[16384] = {};
    const int written           = WideCharToMultiByte(CP_UTF8, 0, buffer, -1, utf8Buffer,
                                                      static_cast<int>(sizeof(utf8Buffer)), nullptr, nullptr);
    if (written <= 0)
        return;

    utf8Buffer[sizeof(utf8Buffer) - 1] = '\0';
    std::string str(utf8Buffer);
    while (!str.empty() && (str.back() == '\n' || str.back() == '\r'))
        str.pop_back();

    if (!str.empty())
        g_MuEditorConsoleUI.LogGame(str);
}
}

// Replacement console output functions
extern "C" {
int editor_wprintf(const wchar_t* format, ...)
{
    if (!format)
        return -1;

    wchar_t buffer[4096] = {};
    va_list args;
    va_start(args, format);
    int result = FormatWideLog(buffer, _countof(buffer), format, args);
    va_end(args);

    if (stdout != nullptr && buffer[0] != L'\0')
    {
        std::fputws(buffer, stdout);
    }

    ForwardFormattedWideToConsole(buffer);
    return result;
}

int editor_fwprintf(FILE* stream, const wchar_t* format, ...)
{
    if (!format)
        return -1;

    // Preserve real file output for non-console FILE* streams
    if (stream != nullptr && stream != stdout && stream != stderr)
    {
        va_list fileArgs;
        va_start(fileArgs, format);
        int fileResult = std::vfwprintf(stream, format, fileArgs);
        va_end(fileArgs);
        return fileResult;
    }

    wchar_t buffer[4096] = {};
    va_list args;
    va_start(args, format);
    int result = FormatWideLog(buffer, _countof(buffer), format, args);
    va_end(args);

    if (stream != nullptr && buffer[0] != L'\0')
    {
        std::fputws(buffer, stream);
    }

    ForwardFormattedWideToConsole(buffer);
    return result;
}
}

// ConsoleStreamBuf implementation
ConsoleStreamBuf::ConsoleStreamBuf(std::ostream& stream, bool isStdout)
    : m_stream(stream), m_oldBuf(stream.rdbuf(this)), m_isStdout(isStdout)
{}

ConsoleStreamBuf::~ConsoleStreamBuf()
{
    // Restore original buffer
    m_stream.rdbuf(m_oldBuf);
}

std::streambuf::int_type ConsoleStreamBuf::overflow(int_type c)
{
    if (traits_type::eq_int_type(c, traits_type::eof()))
    {
        return traits_type::not_eof(c);
    }

    const char  ch        = traits_type::to_char_type(c);
    bool        flushLine = false;
    std::string line;
    {
        std::lock_guard<std::mutex> lock(g_consoleMutex);
        m_buffer.push_back(ch);

        // If we hit a newline, flush the buffer
        if (ch == '\n')
        {
            line = m_buffer;
            while (!line.empty() && (line.back() == '\n' || line.back() == '\r'))
                line.pop_back();

            // Also write to original stream
            if (m_oldBuf)
            {
                m_oldBuf->sputn(m_buffer.c_str(), static_cast<std::streamsize>(m_buffer.size()));
            }

            m_buffer.clear();
            flushLine = !line.empty();
        }
    }

    // Release g_consoleMutex before calling LogGame to avoid self-deadlock
    if (flushLine)
    {
        g_MuEditorConsoleUI.LogGame(line);
    }
    return c;
}

std::streamsize ConsoleStreamBuf::xsputn(const char* s, std::streamsize n)
{
    if (!s || n <= 0)
        return 0;

    for (std::streamsize i = 0; i < n; ++i)
    {
        overflow(traits_type::to_int_type(s[i]));
    }
    return n;
}

CMuEditorConsoleUI::CMuEditorConsoleUI() : m_pStdoutBuf(nullptr), m_pStderrBuf(nullptr)
{
    Initialize();
}

CMuEditorConsoleUI::~CMuEditorConsoleUI()
{
    Shutdown();
}

CMuEditorConsoleUI& CMuEditorConsoleUI::GetInstance()
{
    static CMuEditorConsoleUI instance;
    return instance;
}

void CMuEditorConsoleUI::CleanupOldLogs()
{
    try
    {
        namespace fs = std::filesystem;

        // Get current time
        time_t       now    = time(0);
        const time_t maxAge = 14 * 24 * 60 * 60;  // 14 days in seconds

        // Ensure MuEditor directory exists
        fs::path logDir = "MuEditor";
        if (!fs::exists(logDir))
        {
            fs::create_directories(logDir);
        }

        // Find all log files matching pattern in MuEditor folder
        std::vector<fs::path> logFiles;
        for (const auto& entry : fs::directory_iterator(logDir))
        {
            if (entry.is_regular_file())
            {
                std::string filename = entry.path().filename().string();
                if (filename.find("MuEditor_") == 0 && filename.find(".log") != std::string::npos)
                {
                    logFiles.push_back(entry.path());
                }
            }
        }

        // Delete logs older than 14 days
        for (const auto& logPath : logFiles)
        {
            auto ftime = fs::last_write_time(logPath);
            auto sctp  = std::chrono::time_point_cast<std::chrono::system_clock::duration>(
                ftime - fs::file_time_type::clock::now() + std::chrono::system_clock::now());
            time_t fileTime = std::chrono::system_clock::to_time_t(sctp);

            if (now > fileTime && (now - fileTime) > maxAge)
            {
                fs::remove(logPath);
            }
        }
    }
    catch (const std::exception&)
    {
        // Silently fail if cleanup encounters any errors
    }
}

void CMuEditorConsoleUI::Initialize()
{
    // Clean up old log files and ensure "MuEditor" directory exists BEFORE opening today's log
    CleanupOldLogs();

    // Create log file with date only (one log per day)
    time_t now = time(0);
    tm     timeinfo{};
    localtime_s(&timeinfo, &now);

    std::ostringstream filename;
    filename << "MuEditor_" << std::setfill('0') << std::setw(4) << (timeinfo.tm_year + 1900) << std::setfill('0')
             << std::setw(2) << (timeinfo.tm_mon + 1) << std::setfill('0') << std::setw(2) << timeinfo.tm_mday
             << ".log";

    m_strLogFilePath = (std::filesystem::path("MuEditor") / filename.str()).string();

    // Open in append mode to continue writing to today's log
    m_logFile.open(m_strLogFilePath, std::ios::app);

    if (m_logFile.is_open())
    {
        m_logFile << "=== MU Editor Log Started ===" << std::endl;
        m_logFile << "Log file: " << m_strLogFilePath << std::endl;
        m_logFile << "===========================" << std::endl << std::endl;
        m_logFile.flush();
    }

    // Redirect stdout and stderr to capture all console output
    m_pStdoutBuf = new ConsoleStreamBuf(std::cout, true);
    m_pStderrBuf = new ConsoleStreamBuf(std::cerr, false);
}

void CMuEditorConsoleUI::Shutdown()
{
    // Restore original stream buffers first
    if (m_pStdoutBuf)
    {
        delete m_pStdoutBuf;
        m_pStdoutBuf = nullptr;
    }
    if (m_pStderrBuf)
    {
        delete m_pStderrBuf;
        m_pStderrBuf = nullptr;
    }

    std::lock_guard<std::mutex> lock(g_consoleMutex);
    if (m_logFile.is_open())
    {
        m_logFile << std::endl << "=== MU Editor Log Ended ===" << std::endl;
        m_logFile.close();
    }
}

void CMuEditorConsoleUI::WriteToLogFile(const std::string& message)
{
    // Caller must hold g_consoleMutex
    if (m_logFile.is_open())
    {
        m_logFile << message << std::endl;
        m_logFile.flush();
    }
}

std::string CMuEditorConsoleUI::GetEditorLog() const
{
    std::lock_guard<std::mutex> lock(g_consoleMutex);
    return m_strEditorConsole;
}

std::string CMuEditorConsoleUI::GetGameLog() const
{
    std::lock_guard<std::mutex> lock(g_consoleMutex);
    return m_strGameConsole;
}

void CMuEditorConsoleUI::ClearEditorLog()
{
    std::lock_guard<std::mutex> lock(g_consoleMutex);
    m_strEditorConsole.clear();
}

void CMuEditorConsoleUI::ClearGameLog()
{
    std::lock_guard<std::mutex> lock(g_consoleMutex);
    m_strGameConsole.clear();
}

void CMuEditorConsoleUI::LogEditor(const std::string& message)
{
    // Add timestamp
    time_t now = time(0);
    tm     timeinfo{};
    localtime_s(&timeinfo, &now);
    char timestamp[32] = {};
    strftime(timestamp, sizeof(timestamp), "[%H:%M:%S]", &timeinfo);

    std::string fullMessage = std::string(timestamp) + " " + message;

    std::lock_guard<std::mutex> lock(g_consoleMutex);
    m_strEditorConsole += fullMessage;
    m_strEditorConsole += "\n";

    // Write to log file
    WriteToLogFile(fullMessage);
}

void CMuEditorConsoleUI::LogGame(const std::string& message)
{
    std::lock_guard<std::mutex> lock(g_consoleMutex);

    m_strGameConsole += message;
    m_strGameConsole += "\n";

    // Write to log file with prefix
    WriteToLogFile("[GAME] " + message);
}

void CMuEditorConsoleUI::Render()
{
    std::string logFilePathCopy;
    {
        std::lock_guard<std::mutex> lock(g_consoleMutex);
        m_renderEditorConsole = m_strEditorConsole;
        m_renderGameConsole   = m_strGameConsole;
        logFilePathCopy       = m_strLogFilePath;
    }

    ImGuiIO& io          = ImGui::GetIO();
    ImVec2   bottom_pos  = ImVec2(0, io.DisplaySize.y - 200);
    ImVec2   bottom_size = ImVec2(io.DisplaySize.x, 200);

    ImGui::SetNextWindowPos(bottom_pos, ImGuiCond_Always);
    ImGui::SetNextWindowSize(bottom_size, ImGuiCond_Always);

    ImGuiWindowFlags flags =
        ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_NoResize | ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoCollapse;

    ImGui::PushStyleColor(ImGuiCol_WindowBg, ImVec4(0.1f, 0.1f, 0.1f, 0.9f));

    if (ImGui::Begin("Console", nullptr, flags))
    {
        // Check if hovering this window or any of its children
        if (ImGui::IsWindowHovered(ImGuiHoveredFlags_ChildWindows | ImGuiHoveredFlags_AllowWhenBlockedByActiveItem))
        {
            g_MuEditorCore.SetHoveringUI(true);
        }

        // Split horizontally
        float split_width = ImGui::GetContentRegionAvail().x * 0.5f;

        // Editor Console (Left)
        ImGui::BeginChild("EditorConsole", ImVec2(split_width - 5, 0), true);

        // Fixed header - not scrollable
        ImGui::TextColored(ImVec4(0.8f, 0.8f, 0.8f, 1.0f), "Editor Console");
        ImGui::SameLine();
        ImGui::SetCursorPosX(ImGui::GetCursorPosX() + 20);
        if (ImGui::SmallButton("Copy"))
        {
            ImGui::SetClipboardText(m_renderEditorConsole.c_str());
        }
        ImGui::SameLine();
        if (ImGui::SmallButton("Clear"))
        {
            ClearEditorLog();
            m_renderEditorConsole.clear();
        }
        ImGui::Separator();

        // Scrollable content area
        ImGui::BeginChild("EditorConsoleContent", ImVec2(0, 0), false);
        ImGui::InputTextMultiline("##EditorConsoleText", m_renderEditorConsole.data(), m_renderEditorConsole.size() + 1,
                                  ImVec2(-1, -1), ImGuiInputTextFlags_ReadOnly);
        ImGui::EndChild();

        ImGui::EndChild();

        ImGui::SameLine();

        // Game Console (Right)
        ImGui::BeginChild("GameConsole", ImVec2(0, 0), true);

        // Fixed header - not scrollable
        ImGui::TextColored(ImVec4(0.8f, 0.8f, 0.8f, 1.0f), "Game Console");
        ImGui::SameLine();
        ImGui::SetCursorPosX(ImGui::GetCursorPosX() + 20);
        if (ImGui::SmallButton("Copy##Game"))
        {
            ImGui::SetClipboardText(m_renderGameConsole.c_str());
        }
        ImGui::SameLine();
        if (ImGui::SmallButton("Clear##Game"))
        {
            ClearGameLog();
            m_renderGameConsole.clear();
        }
        ImGui::Separator();

        // Scrollable content area
        ImGui::BeginChild("GameConsoleContent", ImVec2(0, 0), false);
        ImGui::InputTextMultiline("##GameConsoleText", m_renderGameConsole.data(), m_renderGameConsole.size() + 1,
                                  ImVec2(-1, -1), ImGuiInputTextFlags_ReadOnly);
        ImGui::EndChild();

        ImGui::EndChild();

        // Show log file path at bottom
        ImGui::Separator();
        ImGui::Text("Log file: %s", logFilePathCopy.c_str());
    }
    ImGui::End();
    ImGui::PopStyleColor();
}

#endif  // _EDITOR
