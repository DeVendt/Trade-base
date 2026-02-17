using Grpc.Core;
using Grpc.Net.Client;
using TradeBase.Core.Interfaces;
using TradeBase.Core.Models;
using TradeBase.Rithmic.Proto;

namespace TradeBase.RithmicAdapter.Client;

/// <summary>
/// Linux-native gRPC client for connecting to Rithmic Bridge
/// Implements the same interfaces as NinjaTrader adapter for easy swapping
/// </summary>
public class RithmicGrpcClient : IMarketDataSubscriber, IOrderExecutor, IAccountTracker, IAsyncDisposable
{
    private readonly GrpcChannel _channel;
    private readonly MarketDataService.MarketDataServiceClient _marketDataClient;
    private readonly OrderExecutionService.OrderExecutionServiceClient _orderClient;
    private readonly AccountService.AccountServiceClient _accountClient;
    private readonly ConnectionService.ConnectionServiceClient _connectionClient;
    private readonly ILogger<RithmicGrpcClient>? _logger;
    
    private readonly CancellationTokenSource _cts = new();
    private Task? _priceStreamTask;
    private Task? _orderStreamTask;
    
    // Events
    public event EventHandler<PriceUpdateEventArgs>? PriceUpdateReceived;
    public event EventHandler<BarUpdateEventArgs>? BarUpdateReceived;
    public event EventHandler<OrderEventArgs>? OrderSubmitted;
    public event EventHandler<OrderEventArgs>? OrderFilled;
    public event EventHandler<OrderEventArgs>? OrderCancelled;
    public event EventHandler<OrderEventArgs>? OrderRejected;
    public event EventHandler<OrderEventArgs>? OrderModified;
    public event EventHandler<PositionEventArgs>? PositionChanged;
    public event EventHandler<AccountEventArgs>? AccountInfoUpdated;
    
    public bool IsSubscribed => _priceStreamTask != null && !_priceStreamTask.IsCompleted;

    public RithmicGrpcClient(string host = "localhost", int port = 50051, ILogger<RithmicGrpcClient>? logger = null)
    {
        _logger = logger;
        
        // Configure gRPC channel
        var channelOptions = new GrpcChannelOptions
        {
            MaxReceiveMessageSize = null,  // Unlimited
            MaxSendMessageSize = null,
        };
        
        _channel = GrpcChannel.ForAddress($"http://{host}:{port}", channelOptions);
        
        _marketDataClient = new MarketDataService.MarketDataServiceClient(_channel);
        _orderClient = new OrderExecutionService.OrderExecutionServiceClient(_channel);
        _accountClient = new AccountService.AccountServiceClient(_channel);
        _connectionClient = new ConnectionService.ConnectionServiceClient(_channel);
        
        _logger?.LogInformation("Rithmic gRPC client initialized - connecting to {Host}:{Port}", host, port);
    }

    public async Task<bool> ConnectAsync(string username, string password, string server)
    {
        try
        {
            var response = await _connectionClient.ConnectAsync(new ConnectRequest
            {
                Username = username,
                Password = password,
                Server = server
            });
            
            if (response.Connected)
            {
                _logger?.LogInformation("Connected to Rithmic via bridge - Server: {Server}", server);
                
                // Start background streams
                _orderStreamTask = Task.Run(() => StreamOrderUpdatesAsync(_cts.Token));
                
                return true;
            }
            else
            {
                _logger?.LogError("Failed to connect: {Message}", response.Message);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Connection failed");
            return false;
        }
    }

    // IMarketDataSubscriber implementation
    public async Task<bool> SubscribeMarketDataAsync(string symbol, DataType dataType, CancellationToken cancellationToken = default)
    {
        try
        {
            _priceStreamTask = Task.Run(async () =>
            {
                using var call = _marketDataClient.SubscribePrice(new SymbolRequest 
                { 
                    Symbol = symbol,
                    Exchange = "CME"
                }, cancellationToken: cancellationToken);
                
                await foreach (var update in call.ResponseStream.ReadAllAsync(cancellationToken))
                {
                    var priceUpdate = new PriceUpdate
                    {
                        Symbol = update.Symbol,
                        DataType = dataType,
                        Price = update.Last,
                        Volume = update.Volume,
                        Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(update.Timestamp).DateTime
                    };
                    
                    PriceUpdateReceived?.Invoke(this, new PriceUpdateEventArgs { Update = priceUpdate });
                }
            }, cancellationToken);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to subscribe to market data");
            return false;
        }
    }

    public async Task<bool> UnsubscribeMarketDataAsync(string symbol, DataType dataType, CancellationToken cancellationToken = default)
    {
        // gRPC streams are cancelled by the token
        _cts.Cancel();
        return true;
    }

    public Task<bool> SubscribeBarDataAsync(string symbol, int intervalMinutes, CancellationToken cancellationToken = default)
    {
        // TODO: Implement bar subscription
        return Task.FromResult(true);
    }

    public Task<bool> UnsubscribeBarDataAsync(string symbol, int intervalMinutes, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public async Task<IReadOnlyList<string>> GetSubscribedSymbolsAsync(CancellationToken cancellationToken = default)
    {
        // TODO: Track subscriptions
        return new List<string>();
    }

    // IOrderExecutor implementation
    public async Task<Order> SubmitOrderAsync(Order order, CancellationToken cancellationToken = default)
    {
        var response = await _orderClient.SubmitOrderAsync(new OrderRequest
        {
            OrderId = order.OrderId,
            Symbol = order.Symbol,
            Action = order.Action.ToString().ToUpper(),
            OrderType = order.OrderType.ToString().ToUpper(),
            Quantity = order.Quantity,
            LimitPrice = order.LimitPrice ?? 0,
            StopPrice = order.StopPrice ?? 0,
            Account = order.Account,
            Tif = "DAY"
        }, cancellationToken: cancellationToken);
        
        order.State = MapOrderStatus(response.Status);
        OrderSubmitted?.Invoke(this, new OrderEventArgs { Order = order });
        
        return order;
    }

    public Task<Order> SubmitMarketOrderAsync(string symbol, OrderAction action, int quantity, string account, CancellationToken cancellationToken = default)
    {
        return SubmitOrderAsync(new Order
        {
            Symbol = symbol,
            Action = action,
            OrderType = OrderType.Market,
            Quantity = quantity,
            Account = account
        }, cancellationToken);
    }

    public Task<Order> SubmitLimitOrderAsync(string symbol, OrderAction action, int quantity, double limitPrice, string account, CancellationToken cancellationToken = default)
    {
        return SubmitOrderAsync(new Order
        {
            Symbol = symbol,
            Action = action,
            OrderType = OrderType.Limit,
            Quantity = quantity,
            LimitPrice = limitPrice,
            Account = account
        }, cancellationToken);
    }

    public Task<Order> SubmitOCOBracketAsync(string symbol, OrderAction action, int quantity, double entryPrice, double stopPrice, double targetPrice, string account, CancellationToken cancellationToken = default)
    {
        // Rithmic supports OCO natively
        return SubmitOrderAsync(new Order
        {
            Symbol = symbol,
            Action = action,
            OrderType = OrderType.OCO,
            Quantity = quantity,
            LimitPrice = entryPrice,
            StopPrice = stopPrice,
            Account = account
        }, cancellationToken);
    }

    public async Task<bool> CancelOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        var response = await _orderClient.CancelOrderAsync(new OrderIdRequest { OrderId = orderId }, cancellationToken: cancellationToken);
        return response.Status == "CANCELLED";
    }

    public async Task<bool> ModifyOrderAsync(string orderId, double? newLimitPrice = null, double? newStopPrice = null, int? newQuantity = null, CancellationToken cancellationToken = default)
    {
        var response = await _orderClient.ModifyOrderAsync(new ModifyOrderRequest
        {
            OrderId = orderId,
            NewLimitPrice = newLimitPrice ?? 0,
            NewStopPrice = newStopPrice ?? 0,
            NewQuantity = newQuantity ?? 0
        }, cancellationToken: cancellationToken);
        
        return response.Status == "WORKING";
    }

    public Task<Order?> GetOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        // TODO: Implement order query
        return Task.FromResult<Order?>(null);
    }

    public Task<IReadOnlyList<Order>> GetWorkingOrdersAsync(CancellationToken cancellationToken = default)
    {
        // TODO: Implement working orders query
        return Task.FromResult<IReadOnlyList<Order>>(new List<Order>());
    }

    public Task<IReadOnlyList<Order>> GetOrdersForSymbolAsync(string symbol, CancellationToken cancellationToken = default)
    {
        // TODO: Implement symbol orders query
        return Task.FromResult<IReadOnlyList<Order>>(new List<Order>());
    }

    // IAccountTracker implementation
    public async Task<AccountInfo?> GetAccountInfoAsync(string accountId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _accountClient.GetAccountInfoAsync(new AccountRequest { AccountId = accountId }, cancellationToken: cancellationToken);
            
            return new AccountInfo
            {
                AccountId = response.AccountId,
                AccountName = response.AccountId,
                BuyingPower = response.BuyingPower,
                CashValue = response.CashBalance,
                RealizedPnL = response.RealizedPnl,
                UnrealizedPnL = response.UnrealizedPnl
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get account info");
            return null;
        }
    }

    public Task<IReadOnlyList<AccountInfo>> GetAllAccountsAsync(CancellationToken cancellationToken = default)
    {
        // TODO: Implement multiple accounts
        return Task.FromResult<IReadOnlyList<AccountInfo>>(new List<AccountInfo>());
    }

    public Task<Position?> GetPositionAsync(string symbol, string accountId, CancellationToken cancellationToken = default)
    {
        // TODO: Implement position query
        return Task.FromResult<Position?>(null);
    }

    public Task<IReadOnlyList<Position>> GetAllPositionsAsync(CancellationToken cancellationToken = default)
    {
        // TODO: Implement all positions query
        return Task.FromResult<IReadOnlyList<Position>>(new List<Position>());
    }

    public Task<IReadOnlyList<Position>> GetPositionsForAccountAsync(string accountId, CancellationToken cancellationToken = default)
    {
        // TODO: Implement account positions query
        return Task.FromResult<IReadOnlyList<Position>>(new List<Position>());
    }

    private async Task StreamOrderUpdatesAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var call = _orderClient.StreamOrderUpdates(new Empty(), cancellationToken: cancellationToken);
            
            await foreach (var update in call.ResponseStream.ReadAllAsync(cancellationToken))
            {
                var order = new Order
                {
                    OrderId = update.OrderId,
                    State = MapOrderStatus(update.Status),
                    FilledQuantity = update.FilledQuantity,
                    AvgFillPrice = update.AvgFillPrice
                };
                
                var args = new OrderEventArgs { Order = order };
                
                switch (order.State)
                {
                    case OrderState.Filled:
                        OrderFilled?.Invoke(this, args);
                        break;
                    case OrderState.Cancelled:
                        OrderCancelled?.Invoke(this, args);
                        break;
                    case OrderState.Rejected:
                        OrderRejected?.Invoke(this, args);
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Order stream error");
        }
    }

    private static OrderState MapOrderStatus(string status)
    {
        return status switch
        {
            "PENDING" => OrderState.Pending,
            "WORKING" => OrderState.Working,
            "FILLED" => OrderState.Filled,
            "CANCELLED" => OrderState.Cancelled,
            "REJECTED" => OrderState.Rejected,
            "PARTIALLY_FILLED" => OrderState.PartiallyFilled,
            _ => OrderState.Pending
        };
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        
        try
        {
            await _connectionClient.DisconnectAsync(new Empty());
        }
        catch { }
        
        _channel.Dispose();
        _cts.Dispose();
    }
}
