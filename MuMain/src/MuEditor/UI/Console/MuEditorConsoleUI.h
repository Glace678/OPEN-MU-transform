#pragma once

#ifdef _EDITOR

#include <string>
#include <fstream>
#include <sstream>
#include <iostream>

// Custom stream buffer that captures output and forwards to ImGui console
class ConsoleStreamBuf : public std::streambuf
{
public:
    ConsoleStreamBuf(std::ostream& stream, bool isStdout);
    ~ConsoleStreamBuf();

protected:
    virtual int_type        overflow(int_type c) override;
    virtual std::streamsize xsputn(const char* s, std::streamsize n) override;

private:
    std::ostream&   m_stream;
    std::streambuf* m_oldBuf;
    std::string     m_buffer;
    bool            m_isStdout;
};

class CMuEditorConsoleUI
{
public:
    static CMuEditorConsoleUI& GetInstance();

    void Initialize();
    void Shutdown();

    void LogEditor(const std::string& message);
    void LogGame(const std::string& message);

    std::string GetEditorLog() const;
    std::string GetGameLog() const;

    void ClearEditorLog();
    void ClearGameLog();

    void Render();

private:
    CMuEditorConsoleUI();
    ~CMuEditorConsoleUI();

    void WriteToLogFile(const std::string& message);
    void CleanupOldLogs();  // Delete log files older than 14 days

    std::string   m_strEditorConsole;
    std::string   m_strGameConsole;
    std::string   m_renderEditorConsole;
    std::string   m_renderGameConsole;
    std::ofstream m_logFile;
    std::string   m_strLogFilePath;

    // Stream redirection
    ConsoleStreamBuf* m_pStdoutBuf;
    ConsoleStreamBuf* m_pStderrBuf;
};

#define g_MuEditorConsoleUI CMuEditorConsoleUI::GetInstance()

#endif  // _EDITOR
