#include "MerchantPricePackets.h"

namespace Network::MerchantPrices
{
    QuoteCache& Cache()
    {
        static QuoteCache cache;
        return cache;
    }
}
