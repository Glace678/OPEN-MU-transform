#pragma once

#include "stdafx.h"

#include <coreclr_delegates.h>

#include "PacketFunctions_ChatServer.h"
#include "PacketFunctions_ConnectServer.h"
#include "PacketFunctions_ClientToServer.h"

#include <atomic>
#include <cwchar>

#ifdef _WIN32
#include "Core/Platform/WinCompat.h"
#define symLoad GetProcAddress
#else
#include "dlfcn.h"
#include <array>
#include <string>
#ifdef __APPLE__
#include <mach-o/dyld.h>
#include <vector>
#else
#include <unistd.h>
#endif
#define symLoad dlsym
#endif

#ifdef _WIN32
inline constexpr char MUniqueClientLibraryFileName[] = "MUnique.Client.Library.dll";
#elif defined(__APPLE__)
inline constexpr char MUniqueClientLibraryFileName[] = "MUnique.Client.Library.dylib";
#elif defined(__ANDROID__) || defined(__OHOS__)
inline constexpr char MUniqueClientLibraryFileName[] = "libMUnique.Client.Library.so";
#else
inline constexpr char MUniqueClientLibraryFileName[] = "MUnique.Client.Library.so";
#endif

// Construct-on-first-use: the library handle is loaded lazily on first access.
// The inline dotnet_* symbol globals (defined in other translation units) load
// through this handle during their own dynamic initialization, so a plain inline
// global here would risk a static-initialization-order fiasco. A function-local
// static is initialized on first call instead, which is well-defined. The macro
// keeps every existing call site (`munique_client_library_handle`) unchanged.
#ifdef _WIN32
inline HINSTANCE get_munique_client_library_handle()
{
    static const HINSTANCE handle = []() -> HINSTANCE {
        HINSTANCE h = LoadLibraryW(L"MUnique.Client.Library.dll");
        return h;
    }();
    return handle;
}
#else
inline void* get_munique_client_library_handle()
{
    // Native AOT emits a platform-native shared library next to the executable.
    // Resolve that executable directory first so launching from another working
    // directory still works, then fall back to the platform loader search path.
    // Not const-qualified return: dlsym() takes a non-const void* handle.
    static void* const handle = []() -> void* {
#ifdef __APPLE__
        std::array<char, 4096> executableBuffer {};
        uint32_t bufferSize = static_cast<uint32_t>(executableBuffer.size());
        std::string executablePath;
        if (_NSGetExecutablePath(executableBuffer.data(), &bufferSize) == 0)
        {
            executablePath.assign(executableBuffer.data());
        }
        else if (bufferSize > executableBuffer.size())
        {
            std::vector<char> expandedBuffer(bufferSize);
            if (_NSGetExecutablePath(expandedBuffer.data(), &bufferSize) == 0)
            {
                executablePath.assign(expandedBuffer.data());
            }
        }
#else
        std::array<char, 4096> executableBuffer {};
        const ssize_t length = ::readlink(
            "/proc/self/exe", executableBuffer.data(), executableBuffer.size() - 1);
        const std::string executablePath = length > 0
            ? std::string(executableBuffer.data(), static_cast<size_t>(length))
            : std::string();
#endif
        const std::string::size_type slash = executablePath.find_last_of('/');
        if (slash != std::string::npos)
        {
            std::string libraryPath = executablePath.substr(0, slash + 1);
            libraryPath += MUniqueClientLibraryFileName;
            if (void* loadedLibrary = dlopen(libraryPath.c_str(), RTLD_LAZY))
            {
                return loadedLibrary;
            }
        }
        return dlopen(MUniqueClientLibraryFileName, RTLD_LAZY);
    }();
    return handle;
}
#endif
#define munique_client_library_handle get_munique_client_library_handle()

namespace DotNetBridge
{
void ReportDotNetError(const char* detail);
bool IsManagedLibraryAvailable();

template<typename T>
T LoadManagedSymbol(const char* name)
{
    if (!IsManagedLibraryAvailable())
    {
        return nullptr;
    }

    const auto symbol = reinterpret_cast<T>(symLoad(munique_client_library_handle, name));
    if (!symbol)
    {
        ReportDotNetError(name);
    }

    return symbol;
}
}

using DotNetBridge::LoadManagedSymbol;

class Connection
{
private:
    static void OnPacketReceivedS(int32_t handle, int32_t size, BYTE* data);
    static void OnDisconnectedS(int32_t handle);

    PacketFunctions_ChatServer* _chatServer = { };
    PacketFunctions_ConnectServer* _connectServer = { };
    PacketFunctions_ClientToServer* _gameServer = { };

    // PROTO-12: read on the main thread, written from managed callback threads
    // (OnDisconnected), so it must be atomic.
    std::atomic<int32_t> _handle{0};
    void(*_packetHandler)(int32_t, const BYTE*, int32_t);

    // Called from the static managed callbacks with the bridge mutex held.
    void OnDisconnected();
    void OnPacketReceived(const BYTE* data, const int32_t length);

public:
    Connection(const wchar_t* host, int32_t port, bool isEncrypted, void(*packetHandler)(int32_t, const BYTE*, int32_t));
    ~Connection();

    bool IsConnected();
    void Send(const BYTE* data, const int32_t length);
    void Close();

    // PLAT-5: synchronous teardown - managed disconnect, erase the bridge map
    // entry (waiting out any callback in flight), delete PacketFunctions.
    // Idempotent. The object may be delete'd immediately after this returns.
    void SynchronousShutdown();

    int32_t GetHandle() const { return _handle; }

    PacketFunctions_ChatServer* ToChatServer() const { return _chatServer; }
    PacketFunctions_ConnectServer* ToConnectServer() const { return _connectServer; }
    PacketFunctions_ClientToServer* ToGameServer() const { return _gameServer; }
};
