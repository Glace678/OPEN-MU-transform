#include "stdafx.h"
#include <map>
#include <mutex>

#include "Connection.h"

#include "PacketBindings_ChatServer.h"
#include "PacketBindings_ConnectServer.h"
#include "PacketBindings_ClientToServer.h"

std::map<int32_t, Connection*> connections;

// PROTO-12: the managed library invokes our static callbacks on network /
// thread-pool threads, while the main thread inserts (constructor) and erases
// (disconnect / shutdown). Every find/insert/erase on `connections` must run
// under this mutex. The static callbacks additionally hold it across the whole
// user callback so DeleteSocket's teardown cannot free a Connection mid-callback.
std::mutex connectionsMutex;

namespace DotNetBridge
{
bool g_dotnetErrorDisplayed = false;

void ReportDotNetError(const char* detail)
{
    if (g_dotnetErrorDisplayed)
    {
        return;
    }
    g_dotnetErrorDisplayed = true;

    wchar_t buffer[512];
    std::swprintf(buffer, std::size(buffer),
        L"Failed to initialize the managed client library (%hs). The game client cannot connect to the server.",
        detail ? detail : "unknown error");
#ifdef _WIN32
    MessageBoxW(nullptr, buffer, L"MuMainClient", MB_ICONERROR | MB_OK);
#else
    wprintf(L"%ls\n", buffer);
#endif
}

bool IsManagedLibraryAvailable()
{
    if (munique_client_library_handle)
    {
        return true;
    }

    ReportDotNetError(MUniqueClientLibraryFileName);
    return false;
}
}

using DotNetBridge::ReportDotNetError;
using DotNetBridge::IsManagedLibraryAvailable;

using onPacketReceived = void(int32_t, int32_t, BYTE*);
using onDisconnected = void(int32_t);

typedef int32_t(CORECLR_DELEGATE_CALLTYPE* Connect)(const wchar_t*, int32_t, BYTE, onPacketReceived, onDisconnected);
typedef void(CORECLR_DELEGATE_CALLTYPE* Disconnect)(int32_t);
typedef void(CORECLR_DELEGATE_CALLTYPE* BeginReceive)(int32_t);
typedef void(CORECLR_DELEGATE_CALLTYPE* Send)(int32_t, const BYTE*, int32_t);

Connect dotnet_connect = LoadManagedSymbol<Connect>("ConnectionManager_Connect");

Disconnect dotnet_disconnect = LoadManagedSymbol<Disconnect>("ConnectionManager_Disconnect");

BeginReceive dotnet_beginreceive = LoadManagedSymbol<BeginReceive>("ConnectionManager_BeginReceive");

Send dotnet_send = LoadManagedSymbol<Send>("ConnectionManager_Send");

void Connection::OnPacketReceivedS(const int32_t handle, const int32_t size, BYTE* data)
{
    // Held across the whole callback (see mutex comment above): teardown erases
    // under the same lock and only deletes afterwards, so this Connection is
    // alive until the callback returns.
    //
    // Lifetime contract: `data` points into a buffer the managed side rented
    // from MemoryPool<byte>.Shared. It is valid ONLY during this synchronous
    // call - it is returned to the pool as soon as this function returns. The
    // handler must copy anything it needs (HandleIncomingPacket copies into the
    // incoming queue) and must never retain the pointer.
    std::lock_guard<std::mutex> lock(connectionsMutex);

    const auto it = connections.find(handle);
    if (it == connections.end())
    {
        return;
    }

    if (Connection* connection = it->second)
    {
        connection->OnPacketReceived(data, size);
    }
}

void Connection::OnDisconnectedS(const int32_t handle)
{
    // Same lock-across-callback rule as OnPacketReceivedS.
    std::lock_guard<std::mutex> lock(connectionsMutex);

    const auto it = connections.find(handle);
    if (it == connections.end())
    {
        return;
    }

    if (Connection* connection = it->second)
    {
        connection->OnDisconnected();
    }
}

Connection::Connection(const wchar_t* host, int32_t port, bool isEncrypted, void(*packetHandler)(int32_t, const BYTE*, int32_t))
{
    this->_packetHandler = packetHandler;
    if (!dotnet_connect)
    {
        ReportDotNetError("ConnectionManager_Connect");
        this->_handle.store(0);
        return;
    }

    const int32_t handle = dotnet_connect(host, port, isEncrypted ? 1 : 0, &OnPacketReceivedS, &OnDisconnectedS);
    this->_handle.store(handle);

    if (IsConnected())
    {
        // Register before BeginReceive: the first managed callback can only
        // fire once BeginReceive runs, at which point the map entry exists.
        {
            std::lock_guard<std::mutex> lock(connectionsMutex);
            connections[handle] = this;
        }

        if (dotnet_beginreceive)
        {
            dotnet_beginreceive(handle);
        }

        _chatServer = new PacketFunctions_ChatServer();
        _connectServer = new PacketFunctions_ConnectServer();
        _gameServer = new PacketFunctions_ClientToServer();

        _chatServer->SetHandle(handle);
        _connectServer->SetHandle(handle);
        _gameServer->SetHandle(handle);
    }
}

Connection::~Connection()
{
    // Robustness for direct deletes (e.g. failed CreateSocket). The normal
    // path is DeleteSocket() -> SynchronousShutdown() -> delete.
    SynchronousShutdown();
}

void Connection::SynchronousShutdown()
{
    // PLAT-5, in the required order:
    //   1. Tell the managed side to close the socket and stop dispatching.
    //   2. Erase the map entry under the bridge lock. The lock waits out any
    //      callback currently running, and callbacks arriving afterwards miss
    //      in the map, so no callback can touch this object past this point.
    //   3. Release the PacketFunctions. The caller may then delete this object.
    // Idempotent: a handle already cleared means shutdown ran before.
    const int32_t handle = this->_handle.exchange(0);

    if (handle > 0)
    {
        if (dotnet_disconnect)
        {
            dotnet_disconnect(handle);
        }

        std::lock_guard<std::mutex> lock(connectionsMutex);
        connections.erase(handle);
    }

    SafeDelete(_chatServer);
    SafeDelete(_connectServer);
    SafeDelete(_gameServer);
}

bool Connection::IsConnected()
{
    return this->_handle.load() > 0;
}

void Connection::Send(const BYTE* data, const int32_t size)
{
    const int32_t handle = this->_handle.load();
    if (handle <= 0)
    {
        return;
    }

    if (!dotnet_send)
    {
        ReportDotNetError("ConnectionManager_Send");
        return;
    }

    dotnet_send(handle, data, size);
}

void Connection::Close()
{
    // Graceful close while the object stays registered: a later managed
    // OnDisconnected callback erases the entry and zeroes the handle. Full
    // unregister-and-free is SynchronousShutdown (used by DeleteSocket).
    const int32_t handle = this->_handle.load();
    if (handle <= 0)
    {
        return;
    }

    if (dotnet_disconnect)
    {
        dotnet_disconnect(handle);
    }
}

void Connection::OnDisconnected()
{
    // Called from OnDisconnectedS with connectionsMutex already held - do NOT
    // lock it again here.
    const int32_t handle = this->_handle.exchange(0);
    if (handle > 0)
    {
        connections.erase(handle);
    }
}

void Connection::OnPacketReceived(const BYTE* data, const int32_t size)
{
    this->_packetHandler(this->_handle, data, size);
}
