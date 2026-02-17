using TradeBase.Core.Models;

namespace TradeBase.Core.Interfaces;

/// <summary>
/// Interface for NinjaTrader connection management
/// </summary>
public interface INinjaTraderConnection : IAsyncDisposable
{
    ConnectionState State { get; }
    bool IsConnected { get; }
    DateTime? LastConnectedAt { get; }
    DateTime? LastDisconnectedAt { get; }
    
    // Events
    event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
    event EventHandler<ErrorEventArgs>? ErrorOccurred;
    
    // Connection lifecycle
    Task<bool> ConnectAsync(CancellationToken cancellationToken = default);
    Task<bool> DisconnectAsync(CancellationToken cancellationToken = default);
    Task<bool> ReconnectAsync(CancellationToken cancellationToken = default);
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for market data subscription
/// </summary>
public interface IMarketDataSubscriber
{
    bool IsSubscribed { get; }
    
    // Events
    event EventHandler<PriceUpdateEventArgs>? PriceUpdateReceived;
    event EventHandler<BarUpdateEventArgs>? BarUpdateReceived;
    
    // Subscription management
    Task<bool> SubscribeMarketDataAsync(string symbol, DataType dataType, CancellationToken cancellationToken = default);
    Task<bool> UnsubscribeMarketDataAsync(string symbol, DataType dataType, CancellationToken cancellationToken = default);
    Task<bool> SubscribeBarDataAsync(string symbol, int intervalMinutes, CancellationToken cancellationToken = default);
    Task<bool> UnsubscribeBarDataAsync(string symbol, int intervalMinutes, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetSubscribedSymbolsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for order execution
/// </summary>
public interface IOrderExecutor
{
    // Events
    event EventHandler<OrderEventArgs>? OrderSubmitted;
    event EventHandler<OrderEventArgs>? OrderFilled;
    event EventHandler<OrderEventArgs>? OrderCancelled;
    event EventHandler<OrderEventArgs>? OrderRejected;
    event EventHandler<OrderEventArgs>? OrderModified;
    
    // Order operations
    Task<Order> SubmitOrderAsync(Order order, CancellationToken cancellationToken = default);
    Task<Order> SubmitMarketOrderAsync(string symbol, OrderAction action, int quantity, string account, CancellationToken cancellationToken = default);
    Task<Order> SubmitLimitOrderAsync(string symbol, OrderAction action, int quantity, double limitPrice, string account, CancellationToken cancellationToken = default);
    Task<Order> SubmitOCOBracketAsync(string symbol, OrderAction action, int quantity, double entryPrice, double stopPrice, double targetPrice, string account, CancellationToken cancellationToken = default);
    Task<bool> CancelOrderAsync(string orderId, CancellationToken cancellationToken = default);
    Task<bool> ModifyOrderAsync(string orderId, double? newLimitPrice = null, double? newStopPrice = null, int? newQuantity = null, CancellationToken cancellationToken = default);
    
    // Order queries
    Task<Order?> GetOrderAsync(string orderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Order>> GetWorkingOrdersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Order>> GetOrdersForSymbolAsync(string symbol, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for account and position tracking
/// </summary>
public interface IAccountTracker
{
    // Events
    event EventHandler<PositionEventArgs>? PositionChanged;
    event EventHandler<AccountEventArgs>? AccountInfoUpdated;
    
    // Account queries
    Task<AccountInfo?> GetAccountInfoAsync(string accountId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountInfo>> GetAllAccountsAsync(CancellationToken cancellationToken = default);
    
    // Position queries
    Task<Position?> GetPositionAsync(string symbol, string accountId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Position>> GetAllPositionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Position>> GetPositionsForAccountAsync(string accountId, CancellationToken cancellationToken = default);
}

// Event args classes
public class ConnectionStateChangedEventArgs : EventArgs
{
    public ConnectionState OldState { get; set; }
    public ConnectionState NewState { get; set; }
    public string? Reason { get; set; }
}

public class PriceUpdateEventArgs : EventArgs
{
    public required PriceUpdate Update { get; set; }
}

public class BarUpdateEventArgs : EventArgs
{
    public required BarUpdate Update { get; set; }
}

public class OrderEventArgs : EventArgs
{
    public required Order Order { get; set; }
    public string? Message { get; set; }
}

public class PositionEventArgs : EventArgs
{
    public required Position Position { get; set; }
    public PositionDirection? PreviousDirection { get; set; }
    public int? PreviousQuantity { get; set; }
}

public class AccountEventArgs : EventArgs
{
    public required AccountInfo Account { get; set; }
}

public class ErrorEventArgs : EventArgs
{
    public required string Error { get; set; }
    public Exception? Exception { get; set; }
    public bool IsFatal { get; set; } = false;
}
