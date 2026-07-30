using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OrderService.Application.Events;

public static class EventTypes
{
    public const string OrderCreated = "OrderCreated";
    public const string StockReserved = "StockReserved";
    public const string StockFailed = "StockFailed";
    public const string OrderConfirmed = "OrderConfirmed";
    public const string OrderFailed = "OrderFailed";
}