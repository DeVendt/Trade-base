using Microsoft.Extensions.Logging;
using TradeBase.Core.Interfaces;
using TradeBase.Core.Models;

namespace TradeBase.NinjaTraderAdapter.Connection;

/// <summary>
/// Configuration for NinjaTrader connection
/// </summary>
public class NinjaTraderConnectionConfig
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 3692;
    public string ApiKey { get; set; } = string.Empty;
    public string Account { get; set; } = "Sim101";
    public int MaxRetries { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 5000;
    public int HealthCheckIntervalMs { get; set; } = 30000;
    public bool AutoReconnect { get; set; } = true;
}

/// <summary>
/// Manages connection to NinjaTrader via DLL
/// </summary>
public class NinjaTraderConnection : INinjaTraderConnection, IMarketDataSubscriber, IOrderExecutor, IAccountTracker
{
    private readonly NinjaTraderConnectionConfig _config;
    private readonly ILogger<NinjaTraderConnection>? _logger;
    private readonly bool _isMockMode;
    
    private ConnectionState _state = ConnectionState.Disconnected;
    private readonly object _stateLock = new();
    private CancellationTokenSource? _healthCheckCts;
    private Task? _healthCheckTask;
    
    // Mock state for development
    private readonly Dictionary<string, HashSet<DataType>> _mockSubscriptions = new();
    private readonly Dictionary<string, Order> _mockOrders = new();
    private readonly Dictionary<string, Position> _mockPositions = new();
    private readonly Dictionary<string, AccountInfo> _mockAccounts = new();
    
    // Events
    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
    public event EventHandler<ErrorEventArgs>? ErrorOccurred;
    public event EventHandler<PriceUpdateEventArgs>? PriceUpdateReceived;
    public event EventHandler<BarUpdateEventArgs>? BarUpdateReceived;
    public event EventHandler<OrderEventArgs>? OrderSubmitted;
    public event EventHandler<OrderEventArgs>? OrderFilled;
    public event EventHandler<OrderEventArgs>? OrderCancelled;
    public event EventHandler<OrderEventArgs>? OrderRejected;
    public event EventHandler<OrderEventArgs>? OrderModified;
    public event EventHandler<PositionEventArgs>? PositionChanged;
    public event EventHandler<AccountEventArgs>? AccountInfoUpdated;
    
    public ConnectionState State 
    { 
        get { lock(_stateLock) return _state; }
        private set { lock(_stateLock) _state = value; }
    }
    
    public bool IsConnected => State == ConnectionState.Connected;
    public DateTime? LastConnectedAt { get; private set; }
    public DateTime? LastDisconnectedAt { get; private set; }
    public bool IsSubscribed => _mockSubscriptions.Count > 0;
    
    public NinjaTraderConnection(NinjaTraderConnectionConfig config, ILogger<NinjaTraderConnection>? logger = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger;
        
        // Check if we're on Linux or Windows
        _isMockMode = !OperatingSystem.IsWindows();
        
        if (_isMockMode)
        {
            _logger?.LogWarning("Running in MOCK mode - NinjaTrader DLL requires Windows. Using mock implementation for development.");
            InitializeMockData();
        }
    }
    
    private void InitializeMockData()
    {
        // Setup mock account
        _mockAccounts[_config.Account] = new AccountInfo
        {
            AccountId = _config.Account,
            AccountName = $"Mock {_config.Account}",
            BuyingPower = 100000,
            CashValue = 100000,
            RealizedPnL = 0,
            UnrealizedPnL = 0
        };
    }
    
    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected)
        {
            _logger?.LogInformation("Already connected to NinjaTrader");
            return true;
        }
        
        var oldState = State;
        State = ConnectionState.Connecting;
        OnConnectionStateChanged(oldState, State, "Initiating connection");
        
        try
        {
            if (_isMockMode)
            {
                // Simulate connection delay
                await Task.Delay(500, cancellationToken);
                _logger?.LogInformation("Mock connection established to {Account}", _config.Account);
            }
            else
            {
                // Real NT DLL connection would go here
                // NTDirect dll initialization
                await ConnectToNinjaTraderDllAsync(cancellationToken);
            }
            
            oldState = State;
            State = ConnectionState.Connected;
            LastConnectedAt = DateTime.UtcNow;
            OnConnectionStateChanged(oldState, State, "Connection established");
            
            // Start health check
            StartHealthCheckLoop();
            
            _logger?.LogInformation("Successfully connected to NinjaTrader account: {Account}", _config.Account);
            return true;
        }
        catch (Exception ex)
        {
            oldState = State;
            State = ConnectionState.Error;
            OnConnectionStateChanged(oldState, State, $"Connection failed: {ex.Message}");
            OnErrorOccurred($"Failed to connect: {ex.Message}", ex, true);
            return false;
        }
    }
    
    private async Task ConnectToNinjaTraderDllAsync(CancellationToken cancellationToken)
    {
        // This would contain the actual NTDirect.dll calls
        // For now, throw if not on Windows
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("NinjaTrader DLL requires Windows. Use mock mode for development.");
        }
        
        // Placeholder for actual DLL initialization
        await Task.CompletedTask;
        
        /* Actual implementation would be:
        var nt = new NTDirect.NinjaTrader();
        if (!nt.Connected)
        {
            nt.Connect(_config.Host, _config.Port, _config.ApiKey);
        }
        */
    }
    
    public async Task<bool> DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConnected && State != ConnectionState.Connecting)
        {
            return true;
        }
        
        StopHealthCheckLoop();
        
        var oldState = State;
        State = ConnectionState.Disconnected;
        LastDisconnectedAt = DateTime.UtcNow;
        OnConnectionStateChanged(oldState, State, "Disconnected by request");
        
        if (!_isMockMode)
        {
            // Real DLL disconnect
        }
        
        _logger?.LogInformation("Disconnected from NinjaTrader");
        return true;
    }
    
    public async Task<bool> ReconnectAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Attempting to reconnect...");
        
        var oldState = State;
        State = ConnectionState.Reconnecting;
        OnConnectionStateChanged(oldState, State, "Reconnecting");
        
        await DisconnectAsync(cancellationToken);
        
        for (int attempt = 1; attempt <= _config.MaxRetries; attempt++)
        {
            _logger?.LogInformation("Reconnection attempt {Attempt}/{MaxRetries}", attempt, _config.MaxRetries);
            
            if (await ConnectAsync(cancellationToken))
            {
                _logger?.LogInformation("Reconnection successful");
                return true;
            }
            
            if (attempt < _config.MaxRetries)
            {
                await Task.Delay(_config.RetryDelayMs, cancellationToken);
            }
        }
        
        OnErrorOccurred($"Failed to reconnect after {_config.MaxRetries} attempts", null, true);
        return false;
    }
    
    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConnected) return false;
        
        try
        {
            if (_isMockMode)
            {
                // Mock always healthy
                return true;
            }
            
            // Real health check via DLL
            // Check if NT is still responsive
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Health check failed");
            return false;
        }
    }
    
    private void StartHealthCheckLoop()
    {
        if (_config.HealthCheckIntervalMs <= 0) return;
        
        _healthCheckCts = new CancellationTokenSource();
        _healthCheckTask = Task.Run(async () =>
        {
            while (!_healthCheckCts.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_config.HealthCheckIntervalMs, _healthCheckCts.Token);
                    
                    if (!await HealthCheckAsync(_healthCheckCts.Token))
                    {
                        _logger?.LogWarning("Health check failed, triggering reconnect");
                        if (_config.AutoReconnect)
                        {
                            await ReconnectAsync(_healthCheckCts.Token);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Health check loop error");
                }
            }
        });
    }
    
    private void StopHealthCheckLoop()
    {
        _healthCheckCts?.Cancel();
        _healthCheckTask?.Wait(TimeSpan.FromSeconds(5));
        _healthCheckCts?.Dispose();
        _healthCheckCts = null;
    }
    
    // Market Data Implementation
    public Task<bool> SubscribeMarketDataAsync(string symbol, DataType dataType, CancellationToken cancellationToken = default)
    {
        if (!IsConnected) return Task.FromResult(false);
        
        if (!_mockSubscriptions.ContainsKey(symbol))
        {
            _mockSubscriptions[symbol] = new HashSet<DataType>();
        }
        _mockSubscriptions[symbol].Add(dataType);
        
        _logger?.LogInformation("Subscribed to {DataType} data for {Symbol}", dataType, symbol);
        
        // Start mock data feed
        _ = Task.Run(async () => await GenerateMockPriceDataAsync(symbol, cancellationToken), cancellationToken);
        
        return Task.FromResult(true);
    }
    
    public Task<bool> UnsubscribeMarketDataAsync(string symbol, DataType dataType, CancellationToken cancellationToken = default)
    {
        if (_mockSubscriptions.TryGetValue(symbol, out var dataTypes))
        {
            dataTypes.Remove(dataType);
            if (dataTypes.Count == 0)
            {
                _mockSubscriptions.Remove(symbol);
            }
        }
        
        _logger?.LogInformation("Unsubscribed from {DataType} data for {Symbol}", dataType, symbol);
        return Task.FromResult(true);
    }
    
    public Task<bool> SubscribeBarDataAsync(string symbol, int intervalMinutes, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Subscribed to {Interval}min bar data for {Symbol}", intervalMinutes, symbol);
        return Task.FromResult(true);
    }
    
    public Task<bool> UnsubscribeBarDataAsync(string symbol, int intervalMinutes, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Unsubscribed from {Interval}min bar data for {Symbol}", intervalMinutes, symbol);
        return Task.FromResult(true);
    }
    
    public Task<IReadOnlyList<string>> GetSubscribedSymbolsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<string>>(_mockSubscriptions.Keys.ToList());
    }
    
    private async Task GenerateMockPriceDataAsync(string symbol, CancellationToken cancellationToken)
    {
        var random = new Random();
        double basePrice = symbol switch
        {
            "ES" => 4500.0,
            "NQ" => 15500.0,
            "YM" => 35000.0,
            "CL" => 75.0,
            "GC" => 2000.0,
            _ => 100.0
        };
        
        while (!cancellationToken.IsCancellationRequested && _mockSubscriptions.ContainsKey(symbol))
        {
            // Simulate small price movements
            var change = (random.NextDouble() - 0.5) * 0.5;
            basePrice += change;
            
            var update = new PriceUpdate
            {
                Symbol = symbol,
                DataType = DataType.Last,
                Price = basePrice,
                Volume = random.Next(1, 100),
                Timestamp = DateTime.UtcNow
            };
            
            PriceUpdateReceived?.Invoke(this, new PriceUpdateEventArgs { Update = update });
            
            await Task.Delay(1000, cancellationToken); // 1 second updates
        }
    }
    
    // Order Execution Implementation (simplified for now)
    public Task<Order> SubmitOrderAsync(Order order, CancellationToken cancellationToken = default)
    {
        if (!IsConnected) throw new InvalidOperationException("Not connected to NinjaTrader");
        
        order.State = OrderState.Working;
        _mockOrders[order.OrderId] = order;
        
        OrderSubmitted?.Invoke(this, new OrderEventArgs { Order = order });
        
        // Simulate fill after short delay
        _ = Task.Run(async () =>
        {
            await Task.Delay(100, cancellationToken);
            order.State = OrderState.Filled;
            order.FilledQuantity = order.Quantity;
            order.FilledAt = DateTime.UtcNow;
            OrderFilled?.Invoke(this, new OrderEventArgs { Order = order });
        }, cancellationToken);
        
        return Task.FromResult(order);
    }
    
    public Task<Order> SubmitMarketOrderAsync(string symbol, OrderAction action, int quantity, string account, CancellationToken cancellationToken = default)
    {
        var order = new Order
        {
            Symbol = symbol,
            Action = action,
            OrderType = OrderType.Market,
            Quantity = quantity,
            Account = account
        };
        return SubmitOrderAsync(order, cancellationToken);
    }
    
    public Task<Order> SubmitLimitOrderAsync(string symbol, OrderAction action, int quantity, double limitPrice, string account, CancellationToken cancellationToken = default)
    {
        var order = new Order
        {
            Symbol = symbol,
            Action = action,
            OrderType = OrderType.Limit,
            Quantity = quantity,
            LimitPrice = limitPrice,
            Account = account
        };
        return SubmitOrderAsync(order, cancellationToken);
    }
    
    public Task<Order> SubmitOCOBracketAsync(string symbol, OrderAction action, int quantity, double entryPrice, double stopPrice, double targetPrice, string account, CancellationToken cancellationToken = default)
    {
        var order = new Order
        {
            Symbol = symbol,
            Action = action,
            OrderType = OrderType.OCO,
            Quantity = quantity,
            LimitPrice = entryPrice,
            Account = account
        };
        return SubmitOrderAsync(order, cancellationToken);
    }
    
    public Task<bool> CancelOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        if (_mockOrders.TryGetValue(orderId, out var order))
        {
            order.State = OrderState.Cancelled;
            OrderCancelled?.Invoke(this, new OrderEventArgs { Order = order });
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }
    
    public Task<bool> ModifyOrderAsync(string orderId, double? newLimitPrice = null, double? newStopPrice = null, int? newQuantity = null, CancellationToken cancellationToken = default)
    {
        if (_mockOrders.TryGetValue(orderId, out var order))
        {
            if (newLimitPrice.HasValue) order.LimitPrice = newLimitPrice;
            if (newQuantity.HasValue) order.Quantity = newQuantity.Value;
            OrderModified?.Invoke(this, new OrderEventArgs { Order = order });
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }
    
    public Task<Order?> GetOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        _mockOrders.TryGetValue(orderId, out var order);
        return Task.FromResult(order);
    }
    
    public Task<IReadOnlyList<Order>> GetWorkingOrdersAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Order>>(
            _mockOrders.Values.Where(o => o.State == OrderState.Working).ToList()
        );
    }
    
    public Task<IReadOnlyList<Order>> GetOrdersForSymbolAsync(string symbol, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Order>>(
            _mockOrders.Values.Where(o => o.Symbol == symbol).ToList()
        );
    }
    
    // Account Tracking Implementation
    public Task<AccountInfo?> GetAccountInfoAsync(string accountId, CancellationToken cancellationToken = default)
    {
        _mockAccounts.TryGetValue(accountId, out var account);
        return Task.FromResult(account);
    }
    
    public Task<IReadOnlyList<AccountInfo>> GetAllAccountsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<AccountInfo>>(_mockAccounts.Values.ToList());
    }
    
    public Task<Position?> GetPositionAsync(string symbol, string accountId, CancellationToken cancellationToken = default)
    {
        var key = $"{symbol}:{accountId}";
        _mockPositions.TryGetValue(key, out var position);
        return Task.FromResult(position);
    }
    
    public Task<IReadOnlyList<Position>> GetAllPositionsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Position>>(_mockPositions.Values.ToList());
    }
    
    public Task<IReadOnlyList<Position>> GetPositionsForAccountAsync(string accountId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Position>>(
            _mockPositions.Values.Where(p => p.Account == accountId).ToList()
        );
    }
    
    // Event helpers
    private void OnConnectionStateChanged(ConnectionState oldState, ConnectionState newState, string reason)
    {
        ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs 
        { 
            OldState = oldState, 
            NewState = newState, 
            Reason = reason 
        });
    }
    
    private void OnErrorOccurred(string error, Exception? ex = null, bool isFatal = false)
    {
        ErrorOccurred?.Invoke(this, new ErrorEventArgs 
        { 
            Error = error, 
            Exception = ex, 
            IsFatal = isFatal 
        });
    }
    
    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }
}
