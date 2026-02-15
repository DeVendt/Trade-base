# Fully Automated Futures Strategy

## Overview

The **Fully Automated Futures Strategy** is a headless trading system that operates completely autonomously on CME futures markets through NinjaTrader. Once started, it requires zero manual intervention - the AI handles everything from entry to exit.

## Philosophy

**"Set it and forget it"**

The strategy is designed for traders who want:
- No emotional decision-making
- Consistent execution 24/5
- AI-optimized entries and exits
- Automatic risk management
- Hands-free operation

## Supported Instruments

### Primary Markets
| Symbol | Name | Recommended Sessions |
|--------|------|---------------------|
| ES | E-mini S&P 500 | RTH (09:30-16:00 ET), Overnight |
| NQ | E-mini NASDAQ-100 | RTH, Overnight |
| YM | E-mini Dow | RTH |
| CL | Crude Oil | RTH (09:00-14:30 ET) |
| GC | Gold | RTH (08:20-13:30 ET) |

### Contract Specifications
```csharp
public static class FuturesSpecs
{
    public static readonly Dictionary<string, FutureSpec> Contracts = new()
    {
        ["ES"] = new FutureSpec 
        { 
            TickSize = 0.25m, 
            TickValue = 12.50m, 
            PointValue = 50m,
            TradingHours = (TimeSpan.FromHours(18), TimeSpan.FromHours(17)),
            RTH = (TimeSpan.FromHours(9.5), TimeSpan.FromHours(16))
        },
        ["NQ"] = new FutureSpec 
        { 
            TickSize = 0.25m, 
            TickValue = 5m, 
            PointValue = 20m,
            TradingHours = (TimeSpan.FromHours(18), TimeSpan.FromHours(17)),
            RTH = (TimeSpan.FromHours(9.5), TimeSpan.FromHours(16))
        },
        // ... etc
    };
}
```

## Strategy Architecture

```
┌────────────────────────────────────────────────────────────────┐
│                  FULLY AUTOMATED STRATEGY                       │
│                                                                 │
│  ┌────────────────────────────────────────────────────────┐    │
│  │                  AI DECISION LAYER                      │    │
│  │                                                         │    │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐     │    │
│  │  │  Direction  │  │  Volatility │  │   Regime    │     │    │
│  │  │  Predictor  │  │  Predictor  │  │   Detector  │     │    │
│  │  │             │  │             │  │             │     │    │
│  │  │ • Long/     │  │ • High/     │  │ • Trending  │     │    │
│  │  │   Short/    │  │   Low/      │  │ • Ranging   │     │    │
│  │  │   Neutral   │  │   Medium    │  │ • Volatile  │     │    │
│  │  │             │  │             │  │             │     │    │
│  │  │ Confidence: │  │ ATR:        │  │ Confidence: │     │    │
│  │  │ > 65%       │  │ > 2 pts     │  │ > 70%       │     │    │
│  │  └─────────────┘  └─────────────┘  └─────────────┘     │    │
│  └────────────────────────────────────────────────────────┘    │
│                            │                                    │
│                            ▼                                    │
│  ┌────────────────────────────────────────────────────────┐    │
│  │              STRATEGY EXECUTION ENGINE                  │    │
│  │                                                         │    │
│  │  Entry Logic:                                           │    │
│  │  - All models agree (direction + confidence)            │    │
│  │  - Regime is favorable (trending or low vol)            │    │
│  │  - Risk limits not exceeded                             │    │
│  │  - Market hours (optional)                              │    │
│  │                                                         │    │
│  │  Exit Logic:                                            │    │
│  │  - Target hit (3x ATR)                                  │    │
│  │  - Stop hit (1.5x ATR)                                  │    │
│  │  - AI exit signal (confidence drop)                     │    │
│  │  - Trailing stop triggered                              │    │
│  │  - Max hold time reached                                │    │
│  │                                                         │    │
│  │  Position Management:                                   │    │
│  │  - Scale in on confirmation (pyramid)                   │    │
│  │  - Scale out at targets (1/3, 1/3, 1/3)                 │    │
│  │  - Move to breakeven after 1R profit                    │    │
│  └────────────────────────────────────────────────────────┘    │
│                            │                                    │
│                            ▼                                    │
│  ┌────────────────────────────────────────────────────────┐    │
│  │              RISK MANAGEMENT SYSTEM                     │    │
│  │                                                         │    │
│  │  Position Sizing:                                       │    │
│  │  - Kelly Criterion adjusted (fractional)                │    │
│  │  - Volatility-based sizing (lower size in high vol)     │    │
│  │  - Account heat monitoring (< 20% total risk)           │    │
│  │                                                         │    │
│  │  Hard Limits:                                           │    │
│  │  - Max 1% risk per trade                                │    │
│  │  - Max 3% daily loss (circuit breaker)                  │    │
│  │  - Max 3 concurrent positions                           │    │
│  │  - No trading if win rate < 40% (last 20 trades)        │    │
│  └────────────────────────────────────────────────────────┘    │
└────────────────────────────────────────────────────────────────┘
```

## Entry Signals

### Long Entry (Buy)
All conditions must be met:
1. **Direction Prediction**: AI predicts UP with confidence ≥ 65%
2. **Volatility**: Not in extreme volatility (> 4x normal ATR)
3. **Regime**: Either trending up OR ranging (avoid choppy)
4. **Risk Check**: Position size ≤ 1% account risk
5. **Daily Loss Check**: Current day P&L > -2%
6. **Max Positions**: < 3 open positions

### Short Entry (Sell Short)
Same conditions as long, but:
1. **Direction Prediction**: AI predicts DOWN with confidence ≥ 65%

### Entry Order Flow
```csharp
public async Task<EntryResult> TryEnterLongAsync(EntryContext context)
{
    // 1. AI Prediction
    var prediction = await _aiPredictor.PredictAsync(context.Features);
    if (prediction.Direction != Direction.Up || prediction.Confidence < 0.65)
        return EntryResult.Rejected("Insufficient confidence");
    
    // 2. Regime Check
    var regime = await _regimeDetector.DetectAsync(context.MarketData);
    if (regime.Type == MarketRegime.Choppy)
        return EntryResult.Rejected("Avoiding choppy market");
    
    // 3. Risk Check
    var riskCheck = await _riskManager.ValidateEntryAsync(
        symbol: context.Symbol,
        direction: Direction.Up,
        stopDistance: context.ATR * 1.5
    );
    if (!riskCheck.IsValid)
        return EntryResult.Rejected(riskCheck.Reason);
    
    // 4. Calculate Position Size
    var positionSize = _riskManager.CalculatePositionSize(
        accountValue: context.AccountValue,
        riskPercent: 1.0,
        stopDistance: context.ATR * 1.5,
        tickValue: context.TickValue
    );
    
    // 5. Submit Order with OCO
    var entryOrder = await _execution.SubmitOrderAsync(new OrderRequest
    {
        Symbol = context.Symbol,
        Action = OrderAction.Buy,
        OrderType = OrderType.Market,  // Or Limit for better fills
        Quantity = positionSize,
        OCO = new OCOBracket
        {
            StopLoss = context.Price - (context.ATR * 1.5),
            TakeProfit = context.Price + (context.ATR * 3.0)
        }
    });
    
    return EntryResult.Success(entryOrder.OrderId);
}
```

## Exit Signals

### Primary Exits
1. **Take Profit**: 3x ATR target hit
2. **Stop Loss**: 1.5x ATR stop hit
3. **AI Exit**: Confidence drops below 55% (early exit)

### Secondary Exits
4. **Trailing Stop**: 1x ATR trailing stop activated after 1R profit
5. **Time Stop**: Exit after 4 hours if not profitable
6. **End of Session**: Exit 5 min before market close

### Scale-Out Logic
```csharp
// 3-tier exit strategy
if (unrealizedPnL > profitTarget * 0.33)
{
    // Scale out 1/3 at first target
    await ScaleOutAsync(quantity: positionSize / 3);
    
    // Move stop to breakeven
    await MoveStopToBreakevenAsync();
}

if (unrealizedPnL > profitTarget * 0.66)
{
    // Scale out another 1/3
    await ScaleOutAsync(quantity: positionSize / 3);
    
    // Activate trailing stop
    await ActivateTrailingStopAsync(trailingDistance: atr * 1.0);
}

// Last 1/3 runs to target or trailing stop
```

## Position Sizing

### Base Formula (Kelly Criterion Modified)
```csharp
public int CalculatePositionSize(
    double accountValue,
    double riskPercent,
    double stopDistanceTicks,
    double tickValue)
{
    // Risk amount in dollars
    double riskAmount = accountValue * (riskPercent / 100);
    
    // Risk per contract
    double riskPerContract = stopDistanceTicks * tickValue;
    
    // Raw position size
    int contracts = (int)(riskAmount / riskPerContract);
    
    // Kelly adjustment (use 25% of Kelly for safety)
    double winRate = GetRecentWinRate(20);  // Last 20 trades
    double avgWin = GetAverageWin();
    double avgLoss = GetAverageLoss();
    double kelly = (winRate * avgWin - (1 - winRate) * avgLoss) / avgWin;
    double halfKelly = Math.Max(0, kelly * 0.25);
    
    // Apply Kelly factor
    contracts = (int)(contracts * (1 + halfKelly));
    
    // Limits
    contracts = Math.Min(contracts, MaxContractsPerTrade);
    contracts = Math.Max(contracts, 1);
    
    return contracts;
}
```

### Volatility Adjustment
```csharp
// Reduce size in high volatility
if (currentATR > avgATR * 2)
{
    contracts = (int)(contracts * 0.5);  // 50% reduction
}
else if (currentATR > avgATR * 1.5)
{
    contracts = (int)(contracts * 0.75);  // 25% reduction
}
```

## Risk Management

### Daily Circuit Breakers
```csharp
public class CircuitBreakers
{
    // Hard stops - no exceptions
    public bool ShouldHaltTrading(AccountSnapshot account)
    {
        // Daily loss limit
        if (account.DailyPnL < -account.NetLiquidation * 0.03)
            return true;
        
        // Consecutive loss limit
        if (account.ConsecutiveLosses >= 5)
            return true;
        
        // Win rate degradation (last 20 trades)
        if (account.RecentWinRate < 0.30 && account.RecentTrades >= 10)
            return true;
        
        // Max drawdown from high
        if (account.PeakToTroughDrawdown > 0.10)
            return true;
        
        return false;
    }
}
```

### Per-Trade Risk
- Maximum 1% of account per trade
- Maximum 3 concurrent positions
- No overnight positions (optional)
- No trading into major news events

## Configuration

### appsettings.json
```json
{
  "FullyAutomatedStrategy": {
    "Symbol": "ES",
    "Account": "Sim101",
    
    "AI": {
      "EntryConfidenceThreshold": 0.65,
      "ExitConfidenceThreshold": 0.55,
      "ModelPath": "models/es_ensemble.onnx"
    },
    
    "Risk": {
      "RiskPerTradePercent": 1.0,
      "MaxDailyLossPercent": 3.0,
      "MaxPositions": 3,
      "MaxContractsPerTrade": 10
    },
    
    "Exits": {
      "StopLossATRMultiplier": 1.5,
      "TakeProfitATRMultiplier": 3.0,
      "UseTrailingStop": true,
      "TrailingStopATRMultiplier": 1.0,
      "TimeStopMinutes": 240,
      "ScaleOutEnabled": true,
      "ScaleOutLevels": [0.33, 0.66]
    },
    
    "Filters": {
      "RequireRTH": false,
      "AvoidHighVolatility": true,
      "AvoidChoppyMarkets": true,
      "NewsEventBufferMinutes": 30
    }
  }
}
```

## Running the Strategy

### 1. Build and Deploy
```bash
# Build the strategy
dotnet build -c Release

# Deploy to NinjaTrader
Copy-Item bin/Release/*.dll "C:\NinjaTrader 8\bin\Custom\"
```

### 2. Start Headless
```bash
# Run as console application
TradeBase.exe --mode headless --strategy FullyAutomated --symbol ES

# Or install as Windows Service
sc create TradeBase binPath= "C:\TradeBase\TradeBase.exe --mode service"
sc start TradeBase
```

### 3. Monitor via Discord
All activity is logged to Discord:
- Entry/exit notifications
- P&L updates
- Risk alerts
- Daily summaries

## Performance Expectations

### Realistic Targets (ES Futures)
| Metric | Conservative | Moderate | Aggressive |
|--------|-------------|----------|------------|
| Win Rate | 45-50% | 50-55% | 55-60% |
| Profit Factor | 1.3-1.5 | 1.5-1.8 | 1.8-2.2 |
| Trades/Day | 2-4 | 4-8 | 8-15 |
| Expectancy | $50-100 | $100-200 | $200-400 |
| Max Drawdown | 8-12% | 10-15% | 15-20% |

### Monthly Returns (Hypothetical)
- Conservative: 2-4%
- Moderate: 4-8%
- Aggressive: 8-15%

*Past performance does not guarantee future results*

## Troubleshooting

### Strategy Not Entering
Check:
- AI model loaded correctly
- Confidence threshold not too high
- Risk limits not exceeded
- Market regime filter not too strict

### Too Many Losses
Check:
- Recent market conditions (trending vs ranging)
- AI model accuracy (may need retraining)
- Stop loss distance (not too tight)
- Position sizing (not too large)

### Discord Not Receiving Alerts
Check:
- Webhook URL configured
- Network connectivity
- Discord channel permissions

## Advanced Topics

### Multi-Symbol Trading
Run multiple instances:
```bash
TradeBase.exe --symbol ES &
TradeBase.exe --symbol NQ &
TradeBase.exe --symbol CL &
```

### Custom AI Models
Replace the default ONNX model:
```csharp
_aiPredictor.LoadModel("models/my_custom_model.onnx");
```

### Backtesting
```bash
TradeBase.exe --mode backtest --symbol ES --start 2024-01-01 --end 2024-12-31
```
