using Grpc.Core;
using TradeBase.Rithmic.Proto;
using TradeBase.Core.Models;

namespace TradeBase.RithmicAdapter.Bridge.Services;

/// <summary>
/// gRPC service that exposes Rithmic market data to Linux clients
/// Runs on Windows (or Wine) and bridges to Rithmic R|API+
/// </summary>
public class MarketDataGrpcService : MarketDataService.MarketDataServiceBase
{
    private readonly IRithmicConnection _rithmic;
    private readonly ILogger<MarketDataGrpcService> _logger;
    private readonly Dictionary<string, IServerStreamWriter<PriceUpdate>> _priceSubscribers = new();
    private readonly Dictionary<string, IServerStreamWriter<BarUpdate>> _barSubscribers = new();

    public MarketDataGrpcService(IRithmicConnection rithmic, ILogger<MarketDataGrpcService> logger)
    {
        _rithmic = rithmic;
        _logger = logger;
        
        // Wire up Rithmic events to gRPC streams
        _rithmic.PriceUpdate += OnRithmicPriceUpdate;
        _rithmic.BarUpdate += OnRithmicBarUpdate;
    }

    public override async Task SubscribePrice(
        SymbolRequest request, 
        IServerStreamWriter<PriceUpdate> responseStream, 
        ServerCallContext context)
    {
        var key = $"{request.Symbol}:{request.Exchange}";
        _logger.LogInformation("Price subscription started for {Key}", key);
        
        lock (_priceSubscribers)
        {
            _priceSubscribers[key] = responseStream;
        }
        
        // Subscribe to Rithmic
        await _rithmic.SubscribeMarketDataAsync(request.Symbol, request.Exchange);
        
        try
        {
            // Keep stream alive until cancelled
            while (!context.CancellationToken.IsCancellationRequested)
            {
                await Task.Delay(1000, context.CancellationToken);
            }
        }
        finally
        {
            lock (_priceSubscribers)
            {
                _priceSubscribers.Remove(key);
            }
            await _rithmic.UnsubscribeMarketDataAsync(request.Symbol, request.Exchange);
            _logger.LogInformation("Price subscription ended for {Key}", key);
        }
    }

    public override async Task SubscribeBars(
        BarSubscriptionRequest request,
        IServerStreamWriter<BarUpdate> responseStream,
        ServerCallContext context)
    {
        var key = $"{request.Symbol}:{request.IntervalMinutes}";
        _logger.LogInformation("Bar subscription started for {Key}", key);
        
        lock (_barSubscribers)
        {
            _barSubscribers[key] = responseStream;
        }
        
        await _rithmic.SubscribeBarDataAsync(request.Symbol, request.IntervalMinutes);
        
        try
        {
            while (!context.CancellationToken.IsCancellationRequested)
            {
                await Task.Delay(1000, context.CancellationToken);
            }
        }
        finally
        {
            lock (_barSubscribers)
            {
                _barSubscribers.Remove(key);
            }
            await _rithmic.UnsubscribeBarDataAsync(request.Symbol, request.IntervalMinutes);
        }
    }

    public override Task<PriceUpdate> GetCurrentPrice(SymbolRequest request, ServerCallContext context)
    {
        var price = _rithmic.GetCurrentPrice(request.Symbol);
        return Task.FromResult(new PriceUpdate
        {
            Symbol = request.Symbol,
            Bid = price.Bid,
            Ask = price.Ask,
            Last = price.Last,
            Volume = price.Volume,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
    }

    private async void OnRithmicPriceUpdate(object? sender, RithmicPriceEventArgs e)
    {
        var key = $"{e.Symbol}:{e.Exchange}";
        
        lock (_priceSubscribers)
        {
            if (_priceSubscribers.TryGetValue(key, out var stream))
            {
                var update = new PriceUpdate
                {
                    Symbol = e.Symbol,
                    Bid = e.Bid,
                    Ask = e.Ask,
                    Last = e.Last,
                    Volume = e.Volume,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };
                
                // Fire and forget - don't block Rithmic callback
                _ = Task.Run(async () => 
                {
                    try
                    {
                        await stream.WriteAsync(update);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send price update to stream");
                    }
                });
            }
        }
    }

    private async void OnRithmicBarUpdate(object? sender, RithmicBarEventArgs e)
    {
        var key = $"{e.Symbol}:{e.IntervalMinutes}";
        
        lock (_barSubscribers)
        {
            if (_barSubscribers.TryGetValue(key, out var stream))
            {
                var update = new BarUpdate
                {
                    Symbol = e.Symbol,
                    Timestamp = new DateTimeOffset(e.Time).ToUnixTimeMilliseconds(),
                    Open = e.Open,
                    High = e.High,
                    Low = e.Low,
                    Close = e.Close,
                    Volume = e.Volume
                };
                
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await stream.WriteAsync(update);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send bar update to stream");
                    }
                });
            }
        }
    }
}

/// <summary>
/// Placeholder interface for Rithmic connection
/// Would be implemented using actual R|API+ in real code
/// </summary>
public interface IRithmicConnection
{
    bool IsConnected { get; }
    
    event EventHandler<RithmicPriceEventArgs>? PriceUpdate;
    event EventHandler<RithmicBarEventArgs>? BarUpdate;
    event EventHandler<RithmicOrderEventArgs>? OrderUpdate;
    event EventHandler<RithmicPositionEventArgs>? PositionUpdate;
    
    Task ConnectAsync(string username, string password, string server);
    Task DisconnectAsync();
    
    Task SubscribeMarketDataAsync(string symbol, string exchange);
    Task UnsubscribeMarketDataAsync(string symbol, string exchange);
    Task SubscribeBarDataAsync(string symbol, int intervalMinutes);
    Task UnsubscribeBarDataAsync(string symbol, int intervalMinutes);
    
    (double Bid, double Ask, double Last, long Volume) GetCurrentPrice(string symbol);
    
    Task<string> SubmitOrderAsync(RithmicOrder order);
    Task<bool> CancelOrderAsync(string orderId);
    Task<bool> ModifyOrderAsync(string orderId, RithmicOrderChanges changes);
}

// Event args for Rithmic events
public class RithmicPriceEventArgs : EventArgs
{
    public string Symbol { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;
    public double Bid { get; set; }
    public double Ask { get; set; }
    public double Last { get; set; }
    public long Volume { get; set; }
}

public class RithmicBarEventArgs : EventArgs
{
    public string Symbol { get; set; } = string.Empty;
    public int IntervalMinutes { get; set; }
    public DateTime Time { get; set; }
    public double Open { get; set; }
    public double High { get; set; }
    public double Low { get; set; }
    public double Close { get; set; }
    public long Volume { get; set; }
}

public class RithmicOrderEventArgs : EventArgs
{
    public string OrderId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int FilledQuantity { get; set; }
    public double AvgFillPrice { get; set; }
}

public class RithmicPositionEventArgs : EventArgs
{
    public string Symbol { get; set; } = string.Empty;
    public string Account { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public double AvgPrice { get; set; }
    public double UnrealizedPnL { get; set; }
}

public class RithmicOrder
{
    public string Symbol { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string OrderType { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public double? LimitPrice { get; set; }
    public double? StopPrice { get; set; }
    public string Account { get; set; } = string.Empty;
}

public class RithmicOrderChanges
{
    public int? Quantity { get; set; }
    public double? LimitPrice { get; set; }
    public double? StopPrice { get; set; }
}
