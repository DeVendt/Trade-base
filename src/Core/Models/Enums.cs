namespace TradeBase.Core.Models;

/// <summary>
/// Represents a futures contract symbol
/// </summary>
public enum FuturesSymbol
{
    ES,  // E-mini S&P 500
    NQ,  // E-mini NASDAQ-100
    YM,  // E-mini Dow
    CL,  // Crude Oil
    GC,  // Gold
    ZB   // 30-Year T-Bond
}

/// <summary>
/// Order action (buy/sell)
/// </summary>
public enum OrderAction
{
    Buy,
    Sell
}

/// <summary>
/// Order types supported
/// </summary>
public enum OrderType
{
    Market,
    Limit,
    StopMarket,
    StopLimit,
    OCO  // One-Cancels-Other bracket
}

/// <summary>
/// Order state
/// </summary>
public enum OrderState
{
    Pending,
    Working,
    Filled,
    Cancelled,
    Rejected,
    PartiallyFilled
}

/// <summary>
/// Position direction
/// </summary>
public enum PositionDirection
{
    Flat,
    Long,
    Short
}

/// <summary>
/// Market data type
/// </summary>
public enum DataType
{
    Last,
    Bid,
    Ask,
    Volume
}

/// <summary>
/// Connection state
/// </summary>
public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Error
}

/// <summary>
/// Trading mode
/// </summary>
public enum TradingMode
{
    Paper,
    Live
}
