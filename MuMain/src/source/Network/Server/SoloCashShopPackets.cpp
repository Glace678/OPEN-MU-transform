#include "stdafx.h"

#ifdef KJH_ADD_INGAMESHOP_UI_SYSTEM
#include "SoloCashShopPackets.h"
#include "Core/Input/GamepadService.h"
#include "Data/GameConfig/GameConfig.h"
#include "Dotnet/Connection.h"
#include "GameShop/InGameShopSystem.h"
#include "GameShop/MsgBoxIGSCommon.h"
#include "GameShop/NewUIInGameShop.h"
#include "I18N/All.h"
#include "UI/NewUI/NewUISystem.h"

namespace Network::SoloCashShop
{
    namespace
    {
        constexpr std::uint8_t ShopCode = 0xD2;
        constexpr std::uint8_t CatalogBegin = 0xF0;
        constexpr std::uint8_t CatalogOffer = 0xF1;
        constexpr std::uint8_t CatalogEnd = 0xF2;
        constexpr std::uint8_t Exchange = 0xF3;
        constexpr std::uint8_t Delete = 0x0A;
        constexpr int OfferLength = 18 + GameShop::SoloCatalog::NameBytes;

        std::uint16_t Read16(std::span<const std::uint8_t> data)
        {
            return static_cast<std::uint16_t>(data[0] | (static_cast<unsigned>(data[1]) << 8));
        }

        std::uint32_t Read32(std::span<const std::uint8_t> data)
        {
            return Read16(data) | (static_cast<std::uint32_t>(Read16(data.subspan(2))) << 16);
        }

        void ReadOffer(std::span<const std::uint8_t> packet)
        {
            GameShop::SoloCatalog::Offer offer;
            offer.id = Read32(packet.subspan(4));
            offer.itemCode = Read16(packet.subspan(8));
            offer.level = packet[10];
            offer.category = packet[11];
            offer.price = static_cast<std::int32_t>(Read32(packet.subspan(12)));
            offer.quantity = Read16(packet.subspan(16));
            const auto name = packet.subspan(18, GameShop::SoloCatalog::NameBytes);
            const auto end = std::find(name.begin(), name.end(), 0);
            if (end == name.end()
                || MultiByteToWideChar(CP_UTF8, 0, reinterpret_cast<const char*>(name.data()),
                    static_cast<int>(end - name.begin()), offer.name.data(), offer.name.size() - 1) <= 0)
            {
                g_InGameShopSystem->BeginSoloCatalog(-1);
                return;
            }
            g_InGameShopSystem->AppendSoloOffer(offer);
        }

        void ShowResult(std::uint8_t operation, bool success)
        {
            Core::Input::GamepadService::Instance().PublishHaptic(success
                ? Core::Haptics::HapticEvent::TransactionSucceeded
                : Core::Haptics::HapticEvent::OperationFailed, static_cast<double>(SDL_GetTicks()));
            if (operation == Delete && success)
            {
                g_pInGameShop->UpdateStorageItemList();
                return;
            }

            CMsgBoxIGSCommon* box = nullptr;
            SEASON3B::CreateMessageBox(MSGBOX_LAYOUT_CLASS(CMsgBoxIGSCommonLayout), &box);
            box->Initialize(I18N::Game::SoloShopTitle, success
                ? I18N::Game::SoloShopExchangeSucceeded : I18N::Game::SoloShopOperationFailed);
        }
    }

    bool HandlePacket(std::span<const std::uint8_t> packet)
    {
        if (packet.size() < 4 || packet[0] != 0xC1 || packet[1] != packet.size() || packet[2] != ShopCode)
            return false;

        const auto operation = packet[3];
        if (operation == Delete)
        {
            if (packet.size() == 5)
                ShowResult(operation, packet[4] == 0);
            return true;
        }
        if (!GameConfig::GetInstance().IsSoloBalanceEnabled())
            return false;

        switch (operation)
        {
        case CatalogBegin:
            g_InGameShopSystem->BeginSoloCatalog(packet.size() == 8 && Read16(packet.subspan(4)) == 1
                ? Read16(packet.subspan(6)) : -1);
            return true;
        case CatalogOffer:
            if (packet.size() == OfferLength)
                ReadOffer(packet);
            else
                g_InGameShopSystem->BeginSoloCatalog(-1);
            return true;
        case CatalogEnd:
            if (packet.size() != 4)
                g_InGameShopSystem->BeginSoloCatalog(-1);
            g_InGameShopSystem->FinishSoloCatalog();
            return true;
        case Exchange:
            if (packet.size() == 5)
                ShowResult(operation, packet[4] == 0);
            return true;
        default:
            return false;
        }
    }

    void RequestExchange()
    {
        if (!GameConfig::GetInstance().IsSoloBalanceEnabled() || !SocketClient || !SocketClient->IsConnected())
            return;
        const std::array<BYTE, 4> packet { 0xC1, 4, ShopCode, Exchange };
        SocketClient->Send(packet.data(), static_cast<int>(packet.size()));
    }
}
#endif
