# Headless Strategy Design

## Overview

This document describes the design principles and architecture for building **fully automated, headless trading strategies** that integrate with NinjaTrader's .NET DLL for futures trading.

## What is "Headless"?

A headless trading system:
- **No User Interface**: Runs as background service or console application
- **Fully Automated**: Makes all decisions without human intervention
- **24/5 Operation**: Runs continuously during market hours
- **Self-Contained**: Handles data, analysis, execution, and monitoring internally
- **Remote Manageable**: Controlled via configuration files and Discord notifications

## Headless Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                      HEADLESS TRADING SYSTEM                         │
│                                                                      │
│   No GUI │ No Manual Input │ Fully Automated │ 24/5 Operation       │
│                                                                      │
├─────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │                    STRATEGY HOST                             │    │
│  │                                                              │    │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐  │    │
│  │  │  Lifecycle  │  │   Config    │  │   Health Monitor    │  │    │
│  │  │   Manager   │  │   Reload    │  │   & Heartbeat       │  │    │
│  │  │             │  │             │  │                     │  │    │
│  │  │ • Start     │  │ • Hot       │  │ • Connection check  │  │    │
│  │  │ • Stop      │  │   reload    │  │ • Latency monitor   │  │    │
│  │  │ • Error     │  │ • Validate  │  │ • Alert on issues   │  │    │
│  │  │   recovery  │  │             │  │                     │  │    │
│  │  └─────────────┘  └─────────────┘  └─────────────────────┘  │    │
│  └─────────────────────────────────────────────────────────────┘    │
│                              │                                       │
│                              ▼                                       │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │              FULLY AUTOMATED STRATEGY                        │    │
│  │                                                              │    │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐  │    │
│  │  │  AI Brain   │  │  Signal     │  │   Risk Manager      │  │    │
│  │  │             │  │  Generator  │  │                     │  │    │
│  │  │ • Predict   │──→│             │──→│ • Size positions    │  │    │
│  │  │ • Classify  │  │ • Filter    │  │ • Set stops         │  │    │
│  │  │ • Regime    │  │ • Confirm   │  │ • Check limits      │  │    │
│  │  │   detect    │  │ • Time      │  │ • Circuit breakers  │  │    │
│  │  └─────────────┘  └─────────────┘  └─────────────────────┘  │    │
│  │                                                              │    │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐  │    │
│  │  │  Execution  │  │  Position   │  │   Event Logger      │  │    │
│  │  │   Engine    │  │  Manager    │  │                     │  │    │
│  │  │             │  │             │  │ • All trades        │  │    │
│  │  │ • Submit    │  │ • Scale in  │  │ • All decisions     │  │    │
│  │  │ • Modify    │  │ • Scale out │  │ • Performance       │  │    │
│  │  │ • Cancel    │  │ • Breakeven │  │ • Errors            │  │    │
│  │  └─────────────┘  └─────────────┘  └─────────────────────┘  │    │
│  └─────────────────────────────────────────────────────────────┘    │
│                              │                                       │
│                              ▼                                       │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │              NINJATRADER DLL ADAPTER                         │    │
│  │                                                              │    │
│  │  • Connection management           • Order execution         │    │
│  │  • Market data subscription        • Fill handling           │    │
│  │  • Account monitoring              • Error recovery          │    │
│  └─────────────────────────────────────────────────────────────┘    │
│                              │                                       │
│                              ▼                                       │
│                       ┌──────────┐                                   │
│                       │  Broker  │                                   │
│                       │  (CME)   │                                   │
│                       └──────────┘                                   │
└─────────────────────────────────────────────────────────────────────┘
```

## Strategy Interface

All headless strategies implement this interface:

```csharp
public interface IHeadlessStrategy : IAsyncDisposable
{
    string Name { get; }
    string Symbol { get; }
    StrategyState State { get; }
    
    // Lifecycle
    Task<StartResult> StartAsync(StrategyConfig config);
    Task StopAsync();
    Task<StrategyStats> GetStatsAsync();
    
    // Events
    event EventHandler<TradeEventArgs> TradeExecuted;
    event EventHandler<PositionEventArgs> PositionChanged;
    event EventHandler<AlertEventArgs> AlertTriggered;
}

public enum StrategyState
{
    Stopped,
    Starting,
    Running,
    Paused,
    Error,
    ShuttingDown
}
```

## The "Hands-Free" Promise

### What the Strategy Handles Automatically

#### 1. Market Analysis
```csharp
// Automatic - no user input needed
var analysis = await _aiAnalyzer.AnalyzeAsync(marketData);
// • Direction prediction
// • Volatility estimation
// • Regime detection
// • Support/resistance levels
```

#### 2. Entry Decisions
```csharp
// Automatic - evaluates every tick/bar
if (ShouldEnterLong(analysis))
{
    await EnterPositionAsync(
        direction: Direction.Long,
        confidence: analysis.Confidence,
        // Size calculated by risk manager
        // Stops set by parameters
        // Orders submitted automatically
    );
}
```

#### 3. Position Management
```csharp
// Automatic - monitors open positions
foreach (var position in OpenPositions)
{
    // Scale out at targets
    if (position.UnrealizedPnL > ProfitTarget * 0.33)
        await ScaleOutAsync(position, 1/3);
    
    // Move to breakeven
    if (position.UnrealizedPnL > position.RiskAmount)
        await MoveToBreakevenAsync(position);
    
    // AI exit signal
    if (analysis.ExitConfidence > ExitThreshold)
        await ClosePositionAsync(position);
}
```

#### 4. Risk Management
```csharp
// Automatic - enforced on every action
var riskCheck = await _riskManager.ValidateAsync(action);
if (!riskCheck.IsValid)
{
    LogRejection(riskCheck.Reason);
    await NotifyDiscordAsync($"Risk limit: {riskCheck.Reason}");
    return;  // Action blocked
}
```

#### 5. Error Recovery
```csharp
// Automatic - handles connection issues
try
{
    await _ninjaTrader.ExecuteAsync(order);
}
catch (ConnectionLostException)
{
    await RecoverConnectionAsync();
    // Retry order
    await _ninjaTrader.ExecuteAsync(order);
}
```

## Configuration-Driven

All behavior controlled via configuration files:

```json
{
  "Strategy": {
    "Name": "FullyAutomatedES",
    "Symbol": "ES",
    "Account": "Sim101",
    
    "AI": {
      "EntryThreshold": 0.65,
      "ExitThreshold": 0.55,
      "ModelPath": "models/es_ensemble.onnx"
    },
    
    "Risk": {
      "MaxRiskPerTrade": 1.0,
      "MaxDailyLoss": 3.0,
      "MaxPositions": 3
    },
    
    "Execution": {
      "OrderType": "Market",
      "UseOCO": true,
      "SlippageTolerance": 2
    }
  }
}
```

**No code changes needed to adjust strategy!**

## Monitoring & Observability

Since there's no UI, monitoring is critical:

### 1. Structured Logging
```csharp
_logger.LogInformation(
    "Trade executed: {Symbol} {Direction} @ {Price} " +
    "P&L: {PnL:C} Duration: {Duration}s",
    symbol, direction, price, pnl, duration);
```

Output:
```
[2024-02-15 09:45:23 INF] Trade executed: ES Long @ 4500.50 P&L: $625.00 Duration: 180s
```

### 2. Metrics Export
```csharp
// Prometheus metrics
_tradeCounter.Inc();
_pnlHistogram.Observe(trade.PnL);
_latencyGauge.Set(executionLatency);
```

### 3. Discord Notifications
```csharp
// Real-time alerts
await _discord.NotifyTradeAsync(trade);
await _discord.NotifyRiskAlertAsync(alert);
await _discord.NotifyDailySummaryAsync(stats);
```

### 4. Health Checks
```csharp
public class StrategyHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(...)
    {
        // Check: Connected to NinjaTrader?
        // Check: Receiving market data?
        // Check: Orders executing?
        // Check: Within risk limits?
    }
}
```

## Deployment Patterns

### Pattern 1: Windows Service (Production)

```csharp
public class TradingService : BackgroundService
{
    private readonly IHeadlessStrategy _strategy;
    
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await _strategy.StartAsync(_config);
        
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(1000, ct);
            // Heartbeat, health checks, etc.
        }
        
        await _strategy.StopAsync();
    }
}
```

**Install:**
```powershell
sc create TradeBase binPath= "TradeBase.exe"
sc start TradeBase
```

### Pattern 2: Console Application (Development)

```bash
# Run interactively
TradeBase.exe --mode console --symbol ES --verbose

# See real-time logs
# Press 'Q' to quit gracefully
# Press 'P' to pause
# Press 'S' to show stats
```

### Pattern 3: Docker Container

```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:8.0
COPY bin/Release/ /app
WORKDIR /app
ENTRYPOINT ["dotnet", "TradeBase.dll", "--mode", "headless"]
```

```bash
docker run -d \
  --name tradebase \
  -v /config:/config \
  -e CONFIG_PATH=/config/appsettings.json \
  tradebase:latest
```

## Error Handling Philosophy

### The Strategy Never Crashes

```csharp
public async Task RunAsync()
{
    while (!_shutdownRequested)
    {
        try
        {
            await ProcessMarketDataAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in main loop");
            
            // 1. Log to database
            await _errorLog.LogAsync(ex);
            
            // 2. Notify Discord
            await _discord.NotifyErrorAsync(ex);
            
            // 3. Attempt recovery
            await AttemptRecoveryAsync(ex);
            
            // 4. Continue running (never crash)
            await Task.Delay(1000);
        }
    }
}
```

### Recovery Strategies

| Error Type | Recovery Action |
|------------|-----------------|
| Connection lost | Reconnect with exponential backoff |
| Order rejected | Log and continue (don't retry) |
| Invalid data | Skip tick, wait for next |
| Out of memory | Graceful shutdown with position closure |
| Disk full | Alert only, continue trading |

## Testing Headless Strategies

### Unit Tests
```csharp
[Fact]
public async Task Should_Enter_Long_When_Confidence_High()
{
    // Arrange
    var strategy = CreateStrategy();
    var marketData = CreateBullishMarketData();
    
    // Act
    await strategy.OnMarketDataAsync(marketData);
    
    // Assert
    _mockExecution.Verify(x => x.SubmitOrderAsync(
        It.Is<OrderRequest>(o => o.Action == OrderAction.Buy)));
}
```

### Integration Tests
```csharp
[Fact]
public async Task Should_Execute_Full_Trade_Lifecycle()
{
    // Connect to NinjaTrader (Sim account)
    // Run strategy for 1 hour
    // Verify trades executed
    // Verify P&L tracked
}
```

### Paper Trading
```bash
# Run with fake money for 2 weeks
TradeBase.exe --mode paper --symbol ES --duration 14d

# Validate:
# - Win rate > 45%
# - Profit factor > 1.2
# - Max drawdown < 10%
# - No unexpected errors
```

## Best Practices

### DO
- ✅ Log everything
- ✅ Validate all inputs
- ✅ Handle all errors gracefully
- ✅ Use circuit breakers
- ✅ Monitor continuously
- ✅ Start with paper trading
- ✅ Have kill switch available

### DON'T
- ❌ Assume market data is valid
- ❌ Retry failed orders blindly
- ❌ Ignore connection issues
- ❌ Hardcode parameters
- ❌ Skip error handling
- ❌ Deploy without testing
- ❌ Trade without stop losses

## Security Considerations

### API Keys
```csharp
// Store securely
var apiKey = _configuration["NinjaTrader:ApiKey"];
// Loaded from environment variable or secrets manager
// Never hardcoded!
```

### Order Validation
```csharp
// Double-check before submitting
if (order.Quantity > MaxAllowedQuantity)
    throw new SecurityException("Order size exceeds limit!");
```

## Conclusion

A headless strategy should be:
- **Reliable**: Never crashes, handles all errors
- **Observable**: Logs, metrics, notifications
- **Configurable**: Behavior controlled by config
- **Safe**: Risk limits enforced automatically
- **Testable**: Comprehensive test coverage

**Remember**: You're building a system that will trade with real money. Test thoroughly, monitor constantly, and always have a way to shut it down quickly.
