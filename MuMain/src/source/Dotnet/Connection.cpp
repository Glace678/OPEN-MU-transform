#include "stdafx.h"
#include <atomic>
#include <map>
#include <mutex>

#include "Connection.h"

#include "PacketBindings_ChatServer.h"
#include "PacketBindings_ConnectServer.h"
#include "PacketBindings_ClientToServer.h"

namespace
{
std::recursive_mutex           g_connectionsMutex;
std::map<int32_t, Connection*> connections;
}  // namespace

namespace DotNetBridge
{
std::atomic<bool> g_dotnetErrorDisplayed{false};

void ReportDotNetError(const char* detail)
{
    if (g_dotnetErrorDisplayed.exchange(true, std::memory_order_acq_rel))
    {
        return;
    }

    wchar_t buffer[512];
    std::swprintf(
        buffer, std::size(buffer),
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

typedef void(CORECLR_DELEGATE_CALLTYPE* onPacketReceived)(int32_t, int32_t, BYTE*);
typedef void(CORECLR_DELEGATE_CALLTYPE* onDisconnected)(int32_t);

typedef int32_t(CORECLR_DELEGATE_CALLTYPE* Connect)(const wchar_t*, int32_t, BYTE, onPacketReceived, onDisconnected);
typedef void(CORECLR_DELEGATE_CALLTYPE* Disconnect)(int32_t);
typedef void(CORECLR_DELEGATE_CALLTYPE* BeginReceive)(int32_t);
typedef void(CORECLR_DELEGATE_CALLTYPE* Send)(int32_t, const BYTE*, int32_t);

Connect dotnet_connect = LoadManagedSymbol<Connect>("ConnectionManager_Connect");

Disconnect dotnet_disconnect = LoadManagedSymbol<Disconnect>("ConnectionManager_Disconnect");

BeginReceive dotnet_beginreceive = LoadManagedSymbol<BeginReceive>("ConnectionManager_BeginReceive");

Send dotnet_send = LoadManagedSymbol<Send>("ConnectionManager_Send");

void CORECLR_DELEGATE_CALLTYPE Connection::OnPacketReceivedS(const int32_t handle, const int32_t size, BYTE* data)
{
    std::lock_guard<std::recursive_mutex> lock(g_connectionsMutex);
    const auto                            it = connections.find(handle);
    if (it == connections.end())
    {
        return;
    }

    if (Connection* connection = it->second)
    {
        connection->OnPacketReceived(data, size);
    }
}

void CORECLR_DELEGATE_CALLTYPE Connection::OnDisconnectedS(const int32_t handle)
{
    std::lock_guard<std::recursive_mutex> lock(g_connectionsMutex);
    const auto                            it = connections.find(handle);
    if (it == connections.end())
    {
        return;
    }

    if (Connection* connection = it->second)
    {
        connection->OnDisconnected();
    }
}

Connection::Connection(const wchar_t* host, int32_t port, bool isEncrypted,
                       void (*packetHandler)(int32_t, const BYTE*, int32_t))
{
    this->_packetHandler = packetHandler;
    if (!dotnet_connect)
    {
        ReportDotNetError("ConnectionManager_Connect");
        this->_handle.store(0, std::memory_order_release);
        return;
    }

    const int32_t newHandle = dotnet_connect(host, port, isEncrypted ? 1 : 0, &OnPacketReceivedS, &OnDisconnectedS);
    this->_handle.store(newHandle, std::memory_order_release);

    if (newHandle > 0)
    {
        _chatServer    = new PacketFunctions_ChatServer();
        _connectServer = new PacketFunctions_ConnectServer();
        _gameServer    = new PacketFunctions_ClientToServer();

        _chatServer->SetHandle(newHandle);
        _connectServer->SetHandle(newHandle);
        _gameServer->SetHandle(newHandle);

        {
            std::lock_guard<std::recursive_mutex> lock(g_connectionsMutex);
            connections[newHandle] = this;
        }

        if (dotnet_beginreceive)
        {
            dotnet_beginreceive(newHandle);
        }
    }
}

Connection::~Connection()
{
    Close();

    SafeDelete(_chatServer);
    SafeDelete(_connectServer);
    SafeDelete(_gameServer);
}

bool Connection::IsConnected()
{
    return this->_handle.load(std::memory_order_acquire) > 0;
}

void Connection::Send(const BYTE* data, const int32_t size)
{
    const int32_t handle = this->_handle.load(std::memory_order_acquire);
    if (handle <= 0 || data == nullptr || size <= 0)
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
    int32_t handle = 0;
    {
        std::lock_guard<std::recursive_mutex> lock(g_connectionsMutex);
        handle = this->_handle.exchange(0, std::memory_order_acq_rel);
        if (handle > 0)
        {
            const auto it = connections.find(handle);
            if (it != connections.end() && it->second == this)
            {
                connections.erase(it);
            }
        }
    }

    if (handle > 0 && dotnet_disconnect)
    {
        dotnet_disconnect(handle);
    }
}

void Connection::OnDisconnected()
{
    std::lock_guard<std::recursive_mutex> lock(g_connectionsMutex);
    const int32_t                         handle = this->_handle.exchange(0, std::memory_order_acq_rel);
    if (handle <= 0)
    {
        return;
    }

    const auto it = connections.find(handle);
    if (it != connections.end() && it->second == this)
    {
        connections.erase(it);
    }
}

void Connection::OnPacketReceived(const BYTE* data, const int32_t size)
{
    if (data == nullptr || size <= 0 || !this->_packetHandler)
    {
        return;
    }

    wprintf(L"Received packet, size %d", size);
    this->_packetHandler(this->_handle.load(std::memory_order_acquire), data, size);
}
