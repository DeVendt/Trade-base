# NinjaTrader DLL Integration

## Overview

This document details how to integrate with NinjaTrader's .NET DLL for headless trading. NinjaTrader provides a .NET API that allows external applications to connect, receive market data, and execute trades.

## Integration Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                     NINJATRADER INTEGRATION LAYER                            │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                     NinjaTraderHost                                  │    │
│  │  - Manages NinjaTrader Client instance                              │    │
│  │  - Handles connection lifecycle                                      │    │
│  │  - Provides abstraction over NT API                                  │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                              │                                               │
│          ┌───────────────────┼───────────────────┐                          │
│          ▼                   ▼                   ▼                          │
│  ┌───────────────┐  ┌───────────────┐  ┌───────────────┐                   │
│  │ Account       │  │  Market Data  │  │  Execution    │                   │
│  │ Adapter       │  │  Adapter      │  │  Adapter      │                   │
│  │               │  │               │  │               │                   │
│  │ - Balance     │  │ - Price data  │  │ - Orders      │                   │
│  │ - Positions   │  │ - Bars        │  │ - Fills       │                   │
│  │ - P&L         │  │ - Level 2     │  │ - Order mgmt  │                   │
│  └───────────────┘  └───────────────┘  └───────────────┘                   │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Connection Management

### Connection Lifecycle

```csharp
public interface INinjaTraderConnection : IAsyncDisposable
{
    Task<ConnectionResult> ConnectAsync(ConnectionConfig config);
    Task DisconnectAsync();
    ConnectionState State { get; }
    event EventHandler<ConnectionStateChangedEventArgs> StateChanged;
    event EventHandler<ErrorEventArgs> Error;
}

public class NinjaTraderConnection : INinjaTraderConnection
{
    private Client _client;  // NinjaTrader.Client
    private ConnectionConfig _config;
    
    public async Task<ConnectionResult> ConnectAsync(ConnectionConfig config)
    {
        _config = config;
        _client = new Client();
        
        // Subscribe to events
        _client.ConnectionStatusUpdate += OnConnectionStatusUpdate;
        _client.Error += OnError;
        
        // Connect to NinjaTrader
        var result = await _client.ConnectAsync(
            host: config.Host,
            port: config.Port,
            apiKey: config.ApiKey
        );
        
        return result;
    }
}
```

### Connection Configuration

```csharp
public class ConnectionConfig
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 3692;  // NinjaTrader default
    public string ApiKey { get; set; }
    public string AccountName { get; set; }
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(5);
    public int MaxReconnectAttempts { get; set; } = 10;
}
```

## Market Data Adapter

### Real-Time Data Subscription

```csharp
public interface IMarketDataAdapter
{
    Task SubscribeAsync(string instrument, DataType dataType);
    Task UnsubscribeAsync(string instrument, DataType dataType);
    
    event EventHandler<TickEventArgs> TickReceived;
    event EventHandler<BarEventArgs> BarReceived;
    event EventHandler<MarketDepthEventArgs> MarketDepthReceived;
}

public class MarketDataAdapter : IMarketDataAdapter
{
    private readonly Client _client;
    
    public async Task SubscribeAsync(string instrument, DataType dataType)
    {
        var subscription = new MarketDataSubscription
        {
            Instrument = instrument,
            DataType = dataType,
            // Additional parameters based on data type
        };
        
        await _client.SubscribeMarketDataAsync(subscription);
    }
    
    private void OnMarketData(object sender, MarketDataEventArgs e)
    {
        switch (e.DataType)
        {
            case DataType.Last:
                TickReceived?.Invoke(this, new TickEventArgs
                {
                    Instrument = e.Instrument,
                    Price = e.Price,
                    Volume = e.Volume,
                    Timestamp = e.Time
                });
                break;
                
            case DataType.Bid:
            case DataType.Ask:
                // Process quote data
                break;
                
            case DataType.Bar:
                BarReceived?.Invoke(this, new BarEventArgs
                {
                    Instrument = e.Instrument,
                    Open = e.Open,
                    High = e.High,
                    Low = e.Low,
                    Close = e.Close,
                    Volume = e.Volume,
                    Timestamp = e.Time,
                    Period = e.Period
                });
                break;
        }
    }
}
```

### Data Types

```csharp
public enum DataType
{
    Last,       // Last trade price
    Bid,        // Best bid
    Ask,        // Best ask
    Bar,        // OHLCV bar
    MarketDepth // Level 2 data
}

public class Tick
{
    public string Instrument { get; set; }
    public double Price { get; set; }
    public long Volume { get; set; }
    public DateTime Timestamp { get; set; }
    public TickType Type { get; set; }  // Trade, Bid, Ask
}

public class Bar
{
    public string Instrument { get; set; }
    public double Open { get; set; }
    public double High { get; set; }
    public double Low { get; set; }
    public double Close { get; set; }
    public long Volume { get; set; }
    public DateTime Timestamp { get; set; }
    public TimeSpan Period { get; set; }
}
```

## Order Execution Adapter

### Order Operations

```csharp
public interface IExecutionAdapter
{
    Task<OrderResult> SubmitOrderAsync(OrderRequest request);
    Task<OrderResult> ModifyOrderAsync(string orderId, OrderModification modification);
    Task<OrderResult> CancelOrderAsync(string orderId);
    Task<OrderResult> CancelAllOrdersAsync(string instrument = null);
    
    event EventHandler<OrderUpdateEventArgs> OrderUpdated;
    event EventHandler<FillEventArgs> FillReceived;
    event EventHandler<PositionUpdateEventArgs> PositionUpdated;
}

public class ExecutionAdapter : IExecutionAdapter
{
    private readonly Client _client;
    
    public async Task<OrderResult> SubmitOrderAsync(OrderRequest request)
    {
        var ntOrder = new Order
        {
            Account = request.Account,
            Instrument = request.Instrument,
            Action = ConvertAction(request.Action),  // Buy/Sell
            OrderType = ConvertOrderType(request.OrderType),  // Market/Limit/Stop
            Quantity = request.Quantity,
            LimitPrice = request.LimitPrice,
            StopPrice = request.StopPrice,
            TimeInForce = ConvertTIF(request.TimeInForce),
            OCO = request.OCOId  // One-Cancels-Other
        };
        
        var result = await _client.SubmitOrderAsync(ntOrder);
        
        return new OrderResult
        {
            Success = result.Success,
            OrderId = result.OrderId,
            Error = result.ErrorMessage
        };
    }
    
    public async Task<OrderResult> ModifyOrderAsync(string orderId, OrderModification modification)
    {
        var change = new OrderChange
        {
            OrderId = orderId,
            Quantity = modification.NewQuantity,
            LimitPrice = modification.NewLimitPrice,
            StopPrice = modification.NewStopPrice
        };
        
        return await _client.ChangeOrderAsync(change);
    }
}
```

### Order Types

```csharp
public class OrderRequest
{
    public string Account { get; set; }
    public string Instrument { get; set; }
    public OrderAction Action { get; set; }  // Buy, Sell
    public OrderType OrderType { get; set; }  // Market, Limit, Stop, StopLimit
    public int Quantity { get; set; }
    public double? LimitPrice { get; set; }
    public double? StopPrice { get; set; }
    public TimeInForce TimeInForce { get; set; } = TimeInForce.GTC;
    public string OCOId { get; set; }  // OCO reference
    public string StrategyId { get; set; }  // Internal tracking
}

public enum OrderAction
{
    Buy,
    Sell,
    BuyToCover,
    SellShort
}

public enum OrderType
{
    Market,
    Limit,
    StopMarket,
    StopLimit,
    TrailingStop
}

public enum TimeInForce
{
    Day,
    GTC,  // Good Till Cancelled
    IOC,  // Immediate Or Cancel
    FOK   // Fill Or Kill
}
```

## Account Adapter

### Account Information

```csharp
public interface IAccountAdapter
{
    Task<AccountInfo> GetAccountInfoAsync(string accountName);
    Task<IEnumerable<Position>> GetPositionsAsync(string accountName);
    Task<IEnumerable<Order>> GetWorkingOrdersAsync(string accountName);
    Task<PnL> GetPnLAsync(string accountName, DateTime? from = null);
    
    event EventHandler<AccountUpdateEventArgs> AccountUpdated;
}

public class AccountAdapter : IAccountAdapter
{
    public async Task<AccountInfo> GetAccountInfoAsync(string accountName)
    {
        var account = await _client.GetAccountAsync(accountName);
        
        return new AccountInfo
        {
            Name = account.Name,
            BuyingPower = account.BuyingPower,
            CashValue = account.CashValue,
            NetLiquidation = account.NetLiquidation,
            RealizedPnL = account.RealizedPnLTD,
            UnrealizedPnL = account.UnrealizedPnLTD,
            MarginUsed = account.MarginUsed
        };
    }
    
    public async Task<IEnumerable<Position>> GetPositionsAsync(string accountName)
    {
        var positions = await _client.GetPositionsAsync(accountName);
        
        return positions.Select(p => new Position
        {
            Instrument = p.Instrument,
            Quantity = p.Quantity,
            AveragePrice = p.AveragePrice,
            MarketPrice = p.MarketPrice,
            UnrealizedPnL = p.UnrealizedPnL,
            OpenTime = p.OpenTime
        });
    }
}

public class Position
{
    public string Instrument { get; set; }
    public int Quantity { get; set; }  // Positive = long, Negative = short
    public double AveragePrice { get; set; }
    public double MarketPrice { get; set; }
    public double UnrealizedPnL { get; set; }
    public DateTime OpenTime { get; set; }
    public TimeSpan Duration => DateTime.UtcNow - OpenTime;
}
```

## Event Handling

### Event Types

```csharp
// Order Events
public class OrderUpdateEventArgs : EventArgs
{
    public string OrderId { get; set; }
    public OrderState State { get; set; }  // Working, Filled, Cancelled, Rejected
    public string Instrument { get; set; }
    public int FilledQuantity { get; set; }
    public int RemainingQuantity { get; set; }
    public double AverageFillPrice { get; set; }
    public string RejectReason { get; set; }
    public DateTime Timestamp { get; set; }
}

// Fill Events
public class FillEventArgs : EventArgs
{
    public string FillId { get; set; }
    public string OrderId { get; set; }
    public string Instrument { get; set; }
    public OrderAction Action { get; set; }
    public int Quantity { get; set; }
    public double Price { get; set; }
    public DateTime Timestamp { get; set; }
    public string Exchange { get; set; }
}

// Position Events
public class PositionUpdateEventArgs : EventArgs
{
    public string Instrument { get; set; }
    public int NewQuantity { get; set; }
    public double NewAveragePrice { get; set; }
    public double UnrealizedPnL { get; set; }
    public PositionChangeType ChangeType { get; set; }
}
```

## Error Handling

### Retry Logic

```csharp
public class NinjaTraderRetryPolicy
{
    private readonly int _maxRetries;
    private readonly TimeSpan _delay;
    
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        int attempt = 0;
        while (true)
        {
            try
            {
                return await operation();
            }
            catch (NinjaTraderException ex) when (IsRetryable(ex) && attempt < _maxRetries)
            {
                attempt++;
                await Task.Delay(_delay * Math.Pow(2, attempt));  // Exponential backoff
            }
        }
    }
    
    private bool IsRetryable(NinjaTraderException ex)
    {
        return ex.ErrorCode switch
        {
            ErrorCode.ConnectionLost => true,
            ErrorCode.Timeout => true,
            ErrorCode.RateLimit => true,
            ErrorCode.InsufficientFunds => false,
            ErrorCode.InvalidOrder => false,
            _ => false
        };
    }
}
```

## Configuration Example

```json
{
  "NinjaTrader": {
    "Host": "localhost",
    "Port": 3692,
    "ApiKey": "${NT_API_KEY}",
    "AccountName": "Sim101",
    "AutoReconnect": true,
    "ReconnectDelayMs": 5000,
    "MaxReconnectAttempts": 10,
    "Subscriptions": [
      {
        "Instrument": "ES",
        "DataTypes": ["Last", "Bar"],
        "BarPeriods": ["1 Min", "5 Min", "15 Min"]
      },
      {
        "Instrument": "NQ",
        "DataTypes": ["Last", "Bar"],
        "BarPeriods": ["1 Min", "5 Min"]
      }
    ]
  }
}
```

## Testing the Integration

### Mock Implementation

```csharp
public class MockNinjaTraderAdapter : IExecutionAdapter, IMarketDataAdapter, IAccountAdapter
{
    // Implementation for backtesting and unit testing
    // Simulates NinjaTrader behavior without actual connection
}
```

### Integration Tests

```csharp
[Fact]
public async Task Should_Subscribe_And_Receive_Tick_Data()
{
    var adapter = new MarketDataAdapter();
    await adapter.ConnectAsync(_testConfig);
    
    var ticks = new List<Tick>();
    adapter.TickReceived += (s, e) => ticks.Add(e.Tick);
    
    await adapter.SubscribeAsync("ES 03-25", DataType.Last);
    
    // Wait for data
    await Task.Delay(5000);
    
    Assert.NotEmpty(ticks);
}
```

## Deployment Considerations

### Co-location Options

1. **Same Machine**: Run on same PC as NinjaTrader (lowest latency)
2. **Local Network**: Run on separate machine in same network
3. **Cloud with VPN**: Connect to NinjaTrader via VPN (higher latency)

### Requirements

- NinjaTrader 8+ must be running
- "Allow external connections" enabled in NinjaTrader settings
- API key configured
- Firewall rules allowing connection
