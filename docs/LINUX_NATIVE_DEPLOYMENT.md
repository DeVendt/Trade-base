# True Linux-Native Futures Trading

## Executive Summary

**Mission:** Deploy anywhere on Linux, zero Windows dependencies, one market per instance.

**Best Options for Native Linux:**
1. **Interactive Brokers (IBKR)** - Official Linux support, .NET API
2. **Tradovate** - REST/WebSocket API, pure HTTP, no platform
3. **CQG** - Linux-compatible API
4. **Trading Technologies (TT)** - Linux SDK available

**Recommended:** Interactive Brokers - proven, reliable, excellent API

---

## Architecture: One Market Per Instance

```
┌─────────────────────────────────────────────────────────────┐
│                 DEPLOY ANYWHERE (Linux Container)            │
│                                                              │
│  ┌─────────────────────────────────────────────────────┐   │
│  │           Trading Instance: ES Market               │   │
│  │                                                      │   │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  │   │
│  │  │  Strategy   │  │  Risk Mgr   │  │  Position   │  │   │
│  │  │   Engine    │  │             │  │   Tracker   │  │   │
│  │  │   (ES only) │  │  (1% risk)  │  │             │  │   │
│  │  └──────┬──────┘  └──────┬──────┘  └─────────────┘  │   │
│  │         │                │                          │   │
│  │         └────────────────┼──────────────────────────┘   │
│  │                          │                             │   │
│  │  ┌───────────────────────▼───────────────────────┐    │   │
│  │  │     IBKR Adapter (Linux-native .NET)          │    │   │
│  │  │     └─> IB Gateway/TWS (Java, runs on Linux)  │    │   │
│  │  └───────────────────────┬───────────────────────┘    │   │
│  └──────────────────────────┼────────────────────────────┘   │
│                             │                                  │
│                             ▼ TCP/SSL                          │
│                       ┌──────────┐                             │
│                       │  IBKR    │                             │
│                       │  Servers │                             │
│                       └──────────┘                             │
│                             │                                  │
│                             ▼                                  │
│                       ┌──────────┐                             │
│                       │   CME    │                             │
│                       └──────────┘                             │
└─────────────────────────────────────────────────────────────┘
```

**Multi-Market Deployment:**
```
Docker Compose Stack
├── es-trader (ES only)      → Port 8081
├── nq-trader (NQ only)      → Port 8082
├── ym-trader (YM only)      → Port 8083
├── shared-db (PostgreSQL)   → Port 5432
└── redis (state/cache)      → Port 6379
```

---

## Option 1: Interactive Brokers (RECOMMENDED)

### Why IBKR?

✅ **Official Linux support** - TWS and Gateway run natively  
✅ **Excellent .NET API** - C# client library maintained by IB  
✅ **Low commissions** - $0.25-$0.85 per contract (futures)  
✅ **Regulated** - US-based, publicly traded, SIPC insured  
✅ **Paper trading** - Free demo account for testing  
✅ **Deploy anywhere** - Docker container runs on any Linux host  

### Architecture

```
┌──────────────────────────────────────────────┐
│            Docker Container (Linux)          │
│                                              │
│  ┌──────────────────────────────────────┐   │
│  │  IB Gateway (Java, headless)         │   │
│  │  └─> Listens on 127.0.0.1:7497       │   │
│  └──────────────────────────────────────┘   │
│                   │                          │
│  ┌────────────────▼─────────────────────┐   │
│  │  Trading Engine (.NET 8 on Linux)    │   │
│  │  ├─ Connects to IB Gateway           │   │
│  │  ├─ Strategy logic                   │   │
│  │  └─ Risk management                  │   │
│  └──────────────────────────────────────┘   │
└──────────────────────────────────────────────┘
```

### Implementation

#### 1. IB Gateway Docker Setup

```dockerfile
# Dockerfile.ibgateway
FROM ubuntu:22.04

# Install dependencies
RUN apt-get update && apt-get install -y \
    wget \
    openjdk-17-jre-headless \
    xvfb \
    && rm -rf /var/lib/apt/lists/*

# Download IB Gateway
RUN wget -q https://download2.interactivebrokers.com/installers/ibgateway/stable-standalone/ibgateway-stable-standalone-linux-x64.sh \
    -O /tmp/ibgateway-installer.sh \
    && chmod +x /tmp/ibgateway-installer.sh

# Install IB Gateway
RUN /tmp/ibgateway-installer.sh -q -dir /opt/ibgateway

# Copy config
COPY ibgateway-config.ini /opt/ibgateway/config.ini

EXPOSE 7497

# Start IB Gateway in headless mode
CMD ["/opt/ibgateway/ibgateway", "-Djava.awt.headless=true"]
```

#### 2. C# IB Adapter

```csharp
// src/IBKRAdapter/IBConnection.cs
using IBApi;
using Microsoft.Extensions.Logging;
using TradeBase.Core.Interfaces;
using TradeBase.Core.Models;

namespace TradeBase.IBKRAdapter;

public class IBConnection : IMarketDataSubscriber, IOrderExecutor, IAccountTracker, EWrapper
{
    private readonly EClientSocket _client;
    private readonly ILogger<IBConnection> _logger;
    private readonly int _clientId;
    
    private readonly Dictionary<int, Order> _pendingOrders = new();
    private readonly Dictionary<string, Position> _positions = new();
    private readonly Dictionary<string, PriceUpdate> _lastPrices = new();
    
    public bool IsConnected => _client.IsConnected();
    public bool IsSubscribed => _lastPrices.Count > 0;
    
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
    
    public IBConnection(ILogger<IBConnection> logger, int clientId = 1)
    {
        _logger = logger;
        _clientId = clientId;
        _client = new EClientSocket(this);
    }
    
    public async Task<bool> ConnectAsync(string host = "127.0.0.1", int port = 7497)
    {
        try
        {
            _logger.LogInformation("Connecting to IB Gateway at {Host}:{Port}", host, port);
            _client.eConnect(host, port, _clientId);
            
            // Wait for connection
            await Task.Delay(1000);
            
            if (_client.IsConnected())
            {
                _logger.LogInformation("Connected to IB Gateway");
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to IB Gateway");
            return false;
        }
    }
    
    public Task DisconnectAsync()
    {
        _client.eDisconnect();
        return Task.CompletedTask;
    }
    
    // Market Data
    public Task<bool> SubscribeMarketDataAsync(string symbol, DataType dataType, CancellationToken cancellationToken = default)
    {
        // IB uses numeric contract IDs, create contract
        var contract = CreateContract(symbol);
        var reqId = GetNextRequestId();
        
        _client.reqMktData(reqId, contract, "", false, false, null);
        
        _logger.LogInformation("Subscribed to market data for {Symbol}", symbol);
        return Task.FromResult(true);
    }
    
    public Task<bool> UnsubscribeMarketDataAsync(string symbol, DataType dataType, CancellationToken cancellationToken = default)
    {
        var reqId = GetRequestIdForSymbol(symbol);
        if (reqId.HasValue)
        {
            _client.cancelMktData(reqId.Value);
        }
        return Task.FromResult(true);
    }
    
    public Task<bool> SubscribeBarDataAsync(string symbol, int intervalMinutes, CancellationToken cancellationToken = default)
    {
        var contract = CreateContract(symbol);
        var reqId = GetNextRequestId();
        
        // IB uses seconds for bar interval
        var intervalSeconds = intervalMinutes * 60;
        _client.reqRealTimeBars(reqId, contract, intervalSeconds, "TRADES", true, null);
        
        return Task.FromResult(true);
    }
    
    public Task<bool> UnsubscribeBarDataAsync(string symbol, int intervalMinutes, CancellationToken cancellationToken = default)
    {
        var reqId = GetRequestIdForSymbol(symbol);
        if (reqId.HasValue)
        {
            _client.cancelRealTimeBars(reqId.Value);
        }
        return Task.FromResult(true);
    }
    
    public Task<IReadOnlyList<string>> GetSubscribedSymbolsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<string>>(_lastPrices.Keys.ToList());
    }
    
    // Order Execution
    public Task<Order> SubmitOrderAsync(Order order, CancellationToken cancellationToken = default)
    {
        var contract = CreateContract(order.Symbol);
        var ibOrder = CreateIBOrder(order);
        var orderId = GetNextOrderId();
        
        _pendingOrders[orderId] = order;
        _client.placeOrder(orderId, contract, ibOrder);
        
        OrderSubmitted?.Invoke(this, new OrderEventArgs { Order = order });
        return Task.FromResult(order);
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
    
    public async Task<Order> SubmitOCOBracketAsync(string symbol, OrderAction action, int quantity, double entryPrice, double stopPrice, double targetPrice, string account, CancellationToken cancellationToken = default)
    {
        // IB supports OCO via parent/child orders
        var parentOrder = new Order
        {
            Symbol = symbol,
            Action = action,
            OrderType = OrderType.Limit,
            Quantity = quantity,
            LimitPrice = entryPrice,
            Account = account
        };
        
        // Submit parent
        await SubmitOrderAsync(parentOrder, cancellationToken);
        
        // TODO: Submit stop and target as child orders with OCO
        
        return parentOrder;
    }
    
    public Task<bool> CancelOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        if (int.TryParse(orderId, out var id))
        {
            _client.cancelOrder(id);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }
    
    public Task<bool> ModifyOrderAsync(string orderId, double? newLimitPrice = null, double? newStopPrice = null, int? newQuantity = null, CancellationToken cancellationToken = default)
    {
        // IB requires cancel and resubmit for modifications
        // Find order and resubmit with changes
        if (int.TryParse(orderId, out var id) && _pendingOrders.TryGetValue(id, out var order))
        {
            if (newLimitPrice.HasValue) order.LimitPrice = newLimitPrice;
            if (newQuantity.HasValue) order.Quantity = newQuantity.Value;
            
            // Cancel and resubmit
            _client.cancelOrder(id);
            _ = SubmitOrderAsync(order, cancellationToken);
            
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }
    
    public Task<Order?> GetOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        if (int.TryParse(orderId, out var id) && _pendingOrders.TryGetValue(id, out var order))
        {
            return Task.FromResult<Order?>(order);
        }
        return Task.FromResult<Order?>(null);
    }
    
    public Task<IReadOnlyList<Order>> GetWorkingOrdersAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Order>>(
            _pendingOrders.Values.Where(o => o.State == OrderState.Working).ToList()
        );
    }
    
    public Task<IReadOnlyList<Order>> GetOrdersForSymbolAsync(string symbol, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Order>>(
            _pendingOrders.Values.Where(o => o.Symbol == symbol).ToList()
        );
    }
    
    // Account Tracking
    public Task<AccountInfo?> GetAccountInfoAsync(string accountId, CancellationToken cancellationToken = default)
    {
        // Request account updates
        _client.reqAccountUpdates(true, accountId);
        
        // Return cached info
        // TODO: Cache account info from callbacks
        return Task.FromResult<AccountInfo?>(null);
    }
    
    public Task<IReadOnlyList<AccountInfo>> GetAllAccountsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<AccountInfo>>(new List<AccountInfo>());
    }
    
    public Task<Position?> GetPositionAsync(string symbol, string accountId, CancellationToken cancellationToken = default)
    {
        _positions.TryGetValue(symbol, out var position);
        return Task.FromResult(position);
    }
    
    public Task<IReadOnlyList<Position>> GetAllPositionsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Position>>(_positions.Values.ToList());
    }
    
    public Task<IReadOnlyList<Position>> GetPositionsForAccountAsync(string accountId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Position>>(_positions.Values.ToList());
    }
    
    // EWrapper Implementation (IB Callbacks)
    public void tickPrice(int tickerId, int field, double price, TickAttrib attribs)
    {
        // field: 1=bid, 2=ask, 4=last, 6=high, 7=low, 9=close
        var symbol = GetSymbolForRequestId(tickerId);
        if (string.IsNullOrEmpty(symbol)) return;
        
        if (!_lastPrices.ContainsKey(symbol))
        {
            _lastPrices[symbol] = new PriceUpdate
            {
                Symbol = symbol,
                DataType = DataType.Last,
                Timestamp = DateTime.UtcNow
            };
        }
        
        var update = _lastPrices[symbol];
        
        switch (field)
        {
            case 1: // Bid
                // update.Bid = price;
                break;
            case 2: // Ask
                // update.Ask = price;
                break;
            case 4: // Last
                update.Price = price;
                PriceUpdateReceived?.Invoke(this, new PriceUpdateEventArgs { Update = update });
                break;
        }
    }
    
    public void tickSize(int tickerId, int field, decimal size)
    {
        // Volume updates
    }
    
    public void orderStatus(int orderId, string status, decimal filled, decimal remaining, double avgFillPrice, int permId, int parentId, double lastFillPrice, int clientId, string whyHeld, double mktCapPrice)
    {
        if (!_pendingOrders.TryGetValue(orderId, out var order)) return;
        
        order.State = status switch
        {
            "PendingSubmit" => OrderState.Pending,
            "PreSubmitted" => OrderState.Pending,
            "Submitted" => OrderState.Working,
            "Filled" => OrderState.Filled,
            "Cancelled" => OrderState.Cancelled,
            "Inactive" => OrderState.Cancelled,
            _ => order.State
        };
        
        order.FilledQuantity = (int)filled;
        order.AvgFillPrice = avgFillPrice;
        
        if (order.State == OrderState.Filled)
        {
            OrderFilled?.Invoke(this, new OrderEventArgs { Order = order });
        }
    }
    
    public void openOrder(int orderId, Contract contract, IBApi.Order order, OrderState orderState)
    {
        // Order opened
    }
    
    public void updatePortfolio(Contract contract, decimal position, double marketPrice, double marketValue, double averageCost, double unrealizedPNL, double realizedPNL, string accountName)
    {
        var pos = new Position
        {
            Symbol = contract.Symbol,
            Account = accountName,
            Direction = position > 0 ? PositionDirection.Long : position < 0 ? PositionDirection.Short : PositionDirection.Flat,
            Quantity = Math.Abs((int)position),
            AveragePrice = averageCost,
            UnrealizedPnL = unrealizedPNL
        };
        
        _positions[contract.Symbol] = pos;
        PositionChanged?.Invoke(this, new PositionEventArgs { Position = pos });
    }
    
    public void updateAccountValue(string key, string value, string currency, string accountName)
    {
        // Account value updates
    }
    
    public void realtimeBar(int reqId, long time, double open, double high, double low, double close, decimal volume, decimal WAP, int count)
    {
        var symbol = GetSymbolForRequestId(reqId);
        if (string.IsNullOrEmpty(symbol)) return;
        
        var bar = new BarUpdate
        {
            Symbol = symbol,
            Time = DateTimeOffset.FromUnixTimeSeconds(time).DateTime,
            Open = open,
            High = high,
            Low = low,
            Close = close,
            Volume = (long)volume
        };
        
        BarUpdateReceived?.Invoke(this, new BarUpdateEventArgs { Update = bar });
    }
    
    public void error(Exception e)
    {
        _logger.LogError(e, "IB API Error");
    }
    
    public void error(string str)
    {
        _logger.LogError("IB API Error: {Error}", str);
    }
    
    public void error(int id, int errorCode, string errorMsg, string advancedOrderRejectJson)
    {
        _logger.LogError("IB API Error [{Id}]: Code={Code}, Msg={Message}", id, errorCode, errorMsg);
    }
    
    public void currentTime(long time)
    {
        // Time sync
    }
    
    // Other EWrapper methods (required but not used)
    public void connectionClosed()
    {
        _logger.LogWarning("IB connection closed");
    }
    
    public void connectAck()
    {
        _logger.LogInformation("IB connection acknowledged");
    }
    
    public void nextValidId(int orderId)
    {
        // Next valid order ID received
    }
    
    // ... (other EWrapper methods implemented as empty)
    public void managedAccounts(string accountsList) { }
    public void deltaNeutralValidation(int reqId, DeltaNeutralContract deltaNeutralContract) { }
    public void accountSummary(int reqId, string account, string tag, string value, string currency) { }
    public void accountSummaryEnd(int reqId) { }
    public void position(string account, Contract contract, decimal pos, double avgCost) { }
    public void positionEnd() { }
    public void accountUpdateMulti(int requestId, string account, string modelCode, string key, string value, string currency) { }
    public void accountUpdateMultiEnd(int requestId) { }
    public void positionMulti(int requestId, string account, string modelCode, Contract contract, decimal pos, double avgCost) { }
    public void positionMultiEnd(int requestId) { }
    public void securityDefinitionOptionalParameter(int reqId, string exchange, int underlyingConId, string tradingClass, string multiplier, HashSet<string> expirations, HashSet<double> strikes) { }
    public void securityDefinitionOptionalParameterEnd(int reqId) { }
    public void softDollarTiers(int reqId, SoftDollarTier[] tiers) { }
    public void tickOptionComputation(int tickerId, int field, int tickAttrib, double impliedVol, double delta, double optPrice, double pvDividend, double gamma, double vega, double theta, double undPrice) { }
    public void tickGeneric(int tickerId, int field, double value) { }
    public void tickString(int tickerId, int field, string value) { }
    public void tickEFP(int tickerId, int tickType, double basisPoints, string formattedBasisPoints, double impliedFuture, int holdDays, string futureLastTradeDate, double dividendImpact, double dividendsToLastTradeDate) { }
    public void tickSnapshotEnd(int tickerId) { }
    public void marketDataType(int reqId, int marketDataType) { }
    public void commissionReport(CommissionReport commissionReport) { }
    public void execDetails(int reqId, Contract contract, Execution execution) { }
    public void execDetailsEnd(int reqId) { }
    public void historicalData(int reqId, Bar bar) { }
    public void historicalDataEnd(int reqId, string startDateStr, string endDateStr) { }
    public void historicalDataUpdate(int reqId, Bar bar) { }
    public void historicalTicks(int reqId, HistoricalTick[] ticks, bool done) { }
    public void historicalTicksBidAsk(int reqId, HistoricalTickBidAsk[] ticks, bool done) { }
    public void historicalTicksLast(int reqId, HistoricalTickLast[] ticks, bool done) { }
    public void updateMktDepth(int tickerId, int position, int operation, int side, double price, decimal size) { }
    public void updateMktDepthL2(int tickerId, int position, string marketMaker, int operation, int side, double price, decimal size, bool isSmartDepth) { }
    public void fundamentalData(int reqId, string data) { }
    public void scannerParameters(string xml) { }
    public void scannerData(int reqId, int rank, ContractDetails contractDetails, string distance, string benchmark, string projection, string legsStr) { }
    public void scannerDataEnd(int reqId) { }
    public void receiveFA(int faDataType, string faXmlData) { }
    public void verifyMessageAPI(string apiData) { }
    public void verifyCompleted(bool isSuccessful, string errorText) { }
    public void verifyAndAuthMessageAPI(string apiData, string xyzChallenge) { }
    public void verifyAndAuthCompleted(bool isSuccessful, string errorText) { }
    public void displayGroupList(int reqId, string groups) { }
    public void displayGroupUpdated(int reqId, string contractInfo) { }
    
    // Helper methods
    private Contract CreateContract(string symbol)
    {
        return new Contract
        {
            Symbol = symbol,
            SecType = "FUT",  // Futures
            Exchange = "GLOBEX",  // CME Globex
            Currency = "USD"
        };
    }
    
    private IBApi.Order CreateIBOrder(Order order)
    {
        var ibOrder = new IBApi.Order
        {
            Action = order.Action == OrderAction.Buy ? "BUY" : "SELL",
            TotalQuantity = order.Quantity,
            OrderType = order.OrderType switch
            {
                OrderType.Market => "MKT",
                OrderType.Limit => "LMT",
                OrderType.StopMarket => "STP",
                OrderType.StopLimit => "STP LMT",
                _ => "MKT"
            },
            LmtPrice = order.LimitPrice ?? 0,
            AuxPrice = order.StopPrice ?? 0
        };
        
        return ibOrder;
    }
    
    private int _nextRequestId = 1000;
    private int GetNextRequestId() => Interlocked.Increment(ref _nextRequestId);
    
    private int _nextOrderId = 1000;
    private int GetNextOrderId() => Interlocked.Increment(ref _nextOrderId);
    
    private readonly Dictionary<int, string> _requestIdToSymbol = new();
    
    private string? GetSymbolForRequestId(int reqId)
    {
        lock (_requestIdToSymbol)
        {
            return _requestIdToSymbol.TryGetValue(reqId, out var symbol) ? symbol : null;
        }
    }
    
    private int? GetRequestIdForSymbol(string symbol)
    {
        lock (_requestIdToSymbol)
        {
            return _requestIdToSymbol.FirstOrDefault(x => x.Value == symbol).Key;
        }
    }
}
```

---

## Option 2: Tradovate

### Why Tradovate?

✅ **Pure REST/WebSocket API** - No platform required  
✅ **HTTP-based** - Runs anywhere, any language  
✅ **Modern** - JSON API, real-time WebSocket streams  
✅ **Lower margins** - Good for small accounts  

### API Endpoints

```
Authentication:
  POST https://demo.tradovate.com/auth/oauth/token

Market Data (WebSocket):
  wss://md.tradovate.com/v1/websocket

Order Execution:
  POST https://live.tradovate.com/v1/order/placeorder
  POST https://live.tradovate.com/v1/order/cancelorder

Account Info:
  GET https://live.tradovate.com/v1/account/list
```

### Simple Implementation

```csharp
public class TradovateClient
{
    private readonly HttpClient _http;
    private readonly ClientWebSocket _ws;
    private string _accessToken = "";
    
    public async Task<bool> AuthenticateAsync(string username, string password)
    {
        var response = await _http.PostAsJsonAsync(
            "https://demo.tradovate.com/auth/oauth/token",
            new { name = username, password = password });
        
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        _accessToken = result?.AccessToken ?? "";
        
        return !string.IsNullOrEmpty(_accessToken);
    }
    
    public async Task SubscribeMarketDataAsync(string symbol)
    {
        await _ws.ConnectAsync(
            new Uri("wss://md.tradovate.com/v1/websocket"), 
            CancellationToken.None);
        
        await _ws.SendAsync(
            Encoding.UTF8.GetBytes($@"""{{""mdMarketDataSubscribe"":{{""symbol"":""{symbol}""}}}}"""),
            WebSocketMessageType.Text, 
            true, 
            CancellationToken.None);
    }
}
```

---

## Deployment: One Market Per Instance

### Docker Compose Example

```yaml
# docker-compose.yml
version: '3.8'

services:
  # Shared infrastructure
  postgres:
    image: postgres:15-alpine
    environment:
      POSTGRES_USER: tradebase
      POSTGRES_PASSWORD: ${DB_PASSWORD}
      POSTGRES_DB: tradebase
    volumes:
      - postgres-data:/var/lib/postgresql/data
    networks:
      - trading-network

  redis:
    image: redis:7-alpine
    networks:
      - trading-network

  # ES Trader Instance
  es-trader:
    build:
      context: .
      dockerfile: Dockerfile.trader
    environment:
      - SYMBOL=ES
      - IB_GATEWAY_HOST=es-ibgateway
      - IB_GATEWAY_PORT=7497
      - DB_HOST=postgres
      - REDIS_HOST=redis
      - RISK_PER_TRADE=1.0
      - MAX_POSITIONS=1
    depends_on:
      - es-ibgateway
      - postgres
      - redis
    networks:
      - trading-network
    restart: unless-stopped

  es-ibgateway:
    build:
      context: .
      dockerfile: Dockerfile.ibgateway
    environment:
      - IB_ACCOUNT=${IB_ACCOUNT}
      - IB_PASSWORD=${IB_PASSWORD}
      - TRADING_MODE=paper
    expose:
      - "7497"
    networks:
      - trading-network
    restart: unless-stopped

  # NQ Trader Instance
  nq-trader:
    build:
      context: .
      dockerfile: Dockerfile.trader
    environment:
      - SYMBOL=NQ
      - IB_GATEWAY_HOST=nq-ibgateway
      - IB_GATEWAY_PORT=7497
      - DB_HOST=postgres
      - REDIS_HOST=redis
    depends_on:
      - nq-ibgateway
      - postgres
      - redis
    networks:
      - trading-network
    restart: unless-stopped

  nq-ibgateway:
    build:
      context: .
      dockerfile: Dockerfile.ibgateway
    environment:
      - IB_ACCOUNT=${IB_ACCOUNT}
      - IB_PASSWORD=${IB_PASSWORD}
      - TRADING_MODE=paper
    expose:
      - "7497"
    networks:
      - trading-network
    restart: unless-stopped

networks:
  trading-network:
    driver: bridge

volumes:
  postgres-data:
```

---

## Requirements Summary

### System Requirements

```
Minimum:
- Linux (Ubuntu 20.04+, Debian 11+, CentOS 8+)
- 2 CPU cores
- 4GB RAM
- 20GB storage
- Internet connection

Recommended per instance:
- 1 CPU core
- 2GB RAM
- 10GB storage
```

### Software Requirements

```
Runtime:
- .NET 8.0 SDK or Runtime
- Docker 20.10+ (for containerized deployment)
- Docker Compose 2.0+ (for multi-instance)

For IBKR:
- OpenJDK 17+ (for IB Gateway)
- Xvfb (for headless GUI)

For Tradovate:
- No additional requirements (pure HTTP)
```

### Network Requirements

```
Outbound Ports:
- 443/tcp (HTTPS - API calls)
- 7497/tcp (IB Gateway - local only)
- 7496/tcp (IB TWS - local only)

No inbound ports required (outbound only)
```

---

## Cost Comparison

| Provider | API Cost | Commissions | Data Fees | Total/Month |
|----------|----------|-------------|-----------|-------------|
| **Interactive Brokers** | Free | $0.25-$0.85/contract | $0 (waived with $30 comm) | **$30+** ✅ |
| **Tradovate** | Free | $0.25-$0.79/contract | Included | **$0** |
| **CQG** | $$$ | Exchange + routing | $$$ | **$500+** |
| **Trading Technologies** | $$$ | Exchange + routing | $$$ | **$1000+** |

**Winner for Linux + Budget:** Tradovate or Interactive Brokers

---

## Implementation Roadmap

### Phase 1: IBKR Adapter (Week 1)
- [ ] Implement IBConnection with all interfaces
- [ ] Create IB Gateway Docker container
- [ ] Test with paper trading account
- [ ] Deploy single instance (ES only)

### Phase 2: Multi-Market (Week 2)
- [ ] Create Docker Compose for multi-instance
- [ ] Add NQ and YM instances
- [ ] Shared database for analytics
- [ ] Redis for state management

### Phase 3: Tradovate Alternative (Week 3)
- [ ] Implement Tradovate REST client
- [ ] WebSocket market data handler
- [ ] Compare performance vs IBKR
- [ ] Choose primary broker

### Phase 4: Production (Week 4)
- [ ] Deploy to cloud VPS
- [ ] Add monitoring/alerting
- [ ] Discord notifications
- [ ] Live trading (small size)

---

## Quick Start

```bash
# 1. Clone repo
git clone https://github.com/DeVendt/Trade-base.git
cd Trade-base

# 2. Configure
cp .env.example .env
# Edit .env with your IB credentials

# 3. Deploy single instance (ES)
docker-compose -f docker-compose.es.yml up -d

# 4. Check logs
docker-compose logs -f es-trader

# 5. Deploy multi-market
docker-compose -f docker-compose.multi.yml up -d
```
