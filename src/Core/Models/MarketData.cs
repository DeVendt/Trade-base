namespace TradeBase.Core.Models;

/// <summary>
/// Represents a price update for a symbol
/// </summary>
public readonly record struct PriceUpdate(
    string Symbol,
    DataType DataType,
    double Price,
    long Volume,
    DateTime Timestamp
);

/// <summary>
/// Represents a bar (candlestick) update
/// </summary>
public readonly record struct BarUpdate(
    string Symbol,
    DateTime Time,
    double Open,
    double High,
    double Low,
    double Close,
    long Volume
);

/// <summary>
/// Represents a trade order
/// </summary>
public class Order
{
    public string OrderId { get; set; } = Guid.NewGuid().ToString();
    public string Symbol { get; set; } = string.Empty;
    public OrderAction Action { get; set; }
    public OrderType OrderType { get; set; }
    public int Quantity { get; set; }
    public double? LimitPrice { get; set; }
    public double? StopPrice { get; set; }
    public string Account { get; set; } = string.Empty;
    public OrderState State { get; set; } = OrderState.Pending;
    public int FilledQuantity { get; set; } = 0;
    public double AvgFillPrice { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FilledAt { get; set; }
    public string? ErrorMessage { get; set; }
    
    // OCO bracket orders
    public string? ParentOrderId { get; set; }
    public string? StopLossOrderId { get; set; }
    public string? TakeProfitOrderId { get; set; }
}

/// <summary>
/// Represents a position in a symbol
/// </summary>
public class Position
{
    public string Symbol { get; set; } = string.Empty;
    public string Account { get; set; } = string.Empty;
    public PositionDirection Direction { get; set; } = PositionDirection.Flat;
    public int Quantity { get; set; } = 0;
    public double AveragePrice { get; set; } = 0;
    public double UnrealizedPnL { get; set; } = 0;
    public double RealizedPnL { get; set; } = 0;
    public DateTime OpenedAt { get; set; }
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents account information
/// </summary>
public class AccountInfo
{
    public string AccountId { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public double BuyingPower { get; set; }
    public double CashValue { get; set; }
    public double RealizedPnL { get; set; }
    public double UnrealizedPnL { get; set; }
    public double TotalPnL => RealizedPnL + UnrealizedPnL;
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
}
