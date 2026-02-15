# Risk Management System

## Overview

The Risk Management System is a critical component that ensures the trading platform operates within predefined risk parameters. It prevents catastrophic losses and enforces disciplined trading through automated checks at multiple levels.

## Risk Management Philosophy

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    RISK MANAGEMENT PRINCIPLES                                │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  1. CAPITAL PRESERVATION                                                     │
│     └── Never risk more than 1-2% per trade                                 │
│     └── Maximum daily loss limit (e.g., 3-5% of account)                    │
│                                                                              │
│  2. POSITION SIZING                                                          │
│     └── Volatility-adjusted position sizing                                 │
│     └── Kelly Criterion or fractional Kelly                                 │
│     └── Maximum position limits per instrument                              │
│                                                                              │
│  3. DIVERSIFICATION                                                          │
│     └── Limit correlated positions                                            │
│     └── Maximum total exposure                                                │
│                                                                              │
│  4. CIRCUIT BREAKERS                                                         │
│     └── Automatic trading halt on large drawdowns                           │
│     └── Cool-down periods after consecutive losses                          │
│                                                                              │
│  5. CONTINUOUS MONITORING                                                    │
│     └── Real-time portfolio heat monitoring                                   │
│     └── AI confidence correlation with outcomes                               │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Risk Check Points

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                      RISK CHECK POINTS                                       │
│                                                                              │
│   ┌─────────────┐                                                            │
│   │   SIGNAL    │ ◄── AI generates trading signal                           │
│   └──────┬──────┘                                                            │
│          │                                                                   │
│          ▼ ┌─────────────────────────────────────────────────────┐          │
│   ┌────────┴────────┐ Pre-Trade Risk Check                        │          │
│   │  Checkpoint 1   │ - Account-level limits                      │          │
│   │  (Pre-Filter)   │ - Daily loss limit                          │          │
│   └────────┬────────┘ - Circuit breaker status                    │          │
│            │         - Market conditions                         │          │
│            └─────────────────────────────────────────────────────┘          │
│            │                                                                 │
│            ▼ ┌─────────────────────────────────────────────────────┐        │
│   ┌──────────┴──────┐ Strategy Risk Check                          │        │
│   │  Checkpoint 2   │ - Strategy-specific limits                   │        │
│   │  (Strategy)     │ - Per-strategy daily loss                    │        │
│   └──────────┬──────┘ - Strategy correlation                        │        │
│              │       - AI confidence threshold                      │        │
│              └─────────────────────────────────────────────────────┘        │
│              │                                                               │
│              ▼ ┌─────────────────────────────────────────────────────┐      │
│   ┌────────────┴────┐ Position Risk Check                            │      │
│   │  Checkpoint 3   │ - Position sizing calculation                  │      │
│   │  (Position)     │ - Maximum position limits                      │      │
│   └────────────┬────┘ - Portfolio heat                               │      │
│                │     - Correlated exposure                            │      │
│                └─────────────────────────────────────────────────────┘      │
│                │                                                             │
│                ▼ ┌─────────────────────────────────────────────────────┐    │
│   ┌──────────────┴──┐ Order Risk Check                                 │    │
│   │  Checkpoint 4   │ - Order validation                               │    │
│   │  (Order)        │ - Margin check                                   │    │
│   └──────────────┬──┘ - Slippage estimation                             │    │
│                  │   - Final confirmation                               │    │
│                  └─────────────────────────────────────────────────────┘    │
│                  │                                                           │
│                  ▼                                                           │
│           ┌─────────────┐                                                    │
│           │   EXECUTE   │ ◄── Trade executed                                │
│           └─────────────┘                                                    │
│                                                                              │
│   Post-Trade:                                                                │
│   ┌─────────────┐    ┌─────────────┐    ┌─────────────┐                     │
│   │   Fill      │───▶│   Update    │───▶│   Check     │                     │
│   │   Received  │    │   Positions │    │   Limits    │                     │
│   └─────────────┘    └─────────────┘    └──────┬──────┘                     │
│                                                │                             │
│                                                ▼                             │
│                                         ┌─────────────┐                      │
│                                         │   ALERT     │ (if limit breached)  │
│                                         └─────────────┘                      │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Risk Engine Implementation

```csharp
public interface IRiskEngine
{
    Task<RiskCheckResult> ValidateSignalAsync(TradingSignal signal, PortfolioSnapshot portfolio);
    Task<RiskCheckResult> ValidateOrderAsync(OrderRequest order, PortfolioSnapshot portfolio);
    Task UpdatePortfolioStateAsync(PortfolioSnapshot portfolio);
    Task<bool> CheckCircuitBreakersAsync(string account);
    
    event EventHandler<RiskEventArgs> RiskEvent;
    event EventHandler<CircuitBreakerEventArgs> CircuitBreakerTriggered;
}

public class RiskEngine : IRiskEngine
{
    private readonly IRiskRepository _repository;
    private readonly ILogger<RiskEngine> _logger;
    private readonly RiskConfiguration _config;
    
    public async Task<RiskCheckResult> ValidateSignalAsync(
        TradingSignal signal, 
        PortfolioSnapshot portfolio)
    {
        // 1. Check account-level circuit breakers
        if (await CheckCircuitBreakersAsync(portfolio.Account))
        {
            return RiskCheckResult.Denied("Circuit breaker active");
        }
        
        // 2. Check daily loss limit
        if (portfolio.DailyPnL <= -portfolio.NetLiquidation * _config.MaxDailyLossPercent)
        {
            return RiskCheckResult.Denied("Daily loss limit reached");
        }
        
        // 3. Check AI confidence
        if (signal.AIConfidence < _config.MinAIConfidence)
        {
            return RiskCheckResult.Denied($"AI confidence {signal.AIConfidence:P} below threshold");
        }
        
        // 4. Check strategy risk
        var strategyResult = await CheckStrategyRiskAsync(signal, portfolio);
        if (!strategyResult.IsAllowed)
        {
            return strategyResult;
        }
        
        // 5. Calculate position size
        var positionSize = CalculatePositionSize(signal, portfolio);
        
        // 6. Check position limits
        var positionResult = await CheckPositionRiskAsync(signal, positionSize, portfolio);
        if (!positionResult.IsAllowed)
        {
            return positionResult;
        }
        
        return RiskCheckResult.Allowed(positionSize);
    }
    
    private decimal CalculatePositionSize(TradingSignal signal, PortfolioSnapshot portfolio)
    {
        // Base risk amount (1% of account)
        var riskAmount = portfolio.NetLiquidation * _config.MaxRiskPerTradePercent;
        
        // Adjust by AI confidence (Kelly Criterion inspired)
        // f* = (p*b - q) / b
        // where p = win probability (AI confidence), b = win/loss ratio
        var p = (double)signal.AIConfidence;
        var b = 2.0; // Assume 2:1 reward/risk ratio
        var q = 1 - p;
        var kellyFraction = (p * b - q) / b;
        var halfKelly = (decimal)Math.Max(0, kellyFraction * 0.5);  // Use half-Kelly for safety
        
        // Adjust by volatility (smaller size in high volatility)
        var volAdjustment = 1.0m / (1.0m + signal.VolatilityScore);
        
        // Adjust by market regime
        var regimeMultiplier = signal.MarketRegime switch
        {
            MarketRegime.TrendingUp or MarketRegime.TrendingDown => 1.0m,
            MarketRegime.Ranging => 0.8m,
            MarketRegime.Volatile => 0.5m,
            _ => 0.3m
        };
        
        var adjustedRisk = riskAmount * halfKelly * volAdjustment * regimeMultiplier;
        
        // Calculate contracts/shares based on stop distance
        var stopDistance = signal.EntryPrice * signal.StopLossPercent;
        var positionSize = adjustedRisk / (decimal)stopDistance;
        
        // Apply maximum position limit
        positionSize = Math.Min(positionSize, _config.MaxPositionSize);
        
        return Math.Floor(positionSize);
    }
}
```

## Risk Configuration

```csharp
public class RiskConfiguration
{
    // Account-level limits
    public decimal MaxDailyLossPercent { get; set; } = 0.03m;  // 3%
    public decimal MaxAccountDrawdownPercent { get; set; } = 0.10m;  // 10%
    public int MaxConsecutiveLosses { get; set; } = 5;
    public TimeSpan CoolDownPeriod { get; set; } = TimeSpan.FromMinutes(30);
    
    // Per-trade limits
    public decimal MaxRiskPerTradePercent { get; set; } = 0.01m;  // 1%
    public decimal MaxRiskPerTradeAbsolute { get; set; } = 1000;  // $1000
    public decimal MaxPositionSize { get; set; } = 10;  // Contracts/shares
    
    // Portfolio limits
    public int MaxOpenPositions { get; set; } = 5;
    public decimal MaxPortfolioHeat { get; set; } = 0.30m;  // 30% of account
    public int MaxCorrelatedPositions { get; set; } = 2;
    
    // AI thresholds
    public decimal MinAIConfidence { get; set; } = 0.60m;
    public decimal TargetAIConfidence { get; set; } = 0.80m;
    
    // Circuit breakers
    public List<CircuitBreakerRule> CircuitBreakerRules { get; set; } = new()
    {
        new CircuitBreakerRule 
        { 
            Name = "DailyLossLimit",
            Trigger = cb => cb.DailyLossPercent >= 0.05m,
            Action = CircuitBreakerAction.HaltTradingForDay
        },
        new CircuitBreakerRule
        {
            Name = "ConsecutiveLosses",
            Trigger = cb => cb.ConsecutiveLosses >= 5,
            Action = CircuitBreakerAction.HaltTradingForHours,
            Duration = TimeSpan.FromHours(2)
        },
        new CircuitBreakerRule
        {
            Name = "LargeDrawdown",
            Trigger = cb => cb.PeakToTroughDrawdown >= 0.08m,
            Action = CircuitBreakerAction.HaltTradingForDay
        },
        new CircuitBreakerRule
        {
            Name = "VolatilitySpike",
            Trigger = cb => cb.CurrentVolatility > cb.AverageVolatility * 3,
            Action = CircuitBreakerAction.ReducePositionSize,
            PositionSizeMultiplier = 0.5m
        }
    };
}

public class CircuitBreakerRule
{
    public string Name { get; set; }
    public Func<CircuitBreakerState, bool> Trigger { get; set; }
    public CircuitBreakerAction Action { get; set; }
    public TimeSpan? Duration { get; set; }
    public decimal? PositionSizeMultiplier { get; set; }
}

public enum CircuitBreakerAction
{
    HaltTrading,
    HaltTradingForDay,
    HaltTradingForHours,
    ReducePositionSize,
    RequireHigherConfidence,
    AlertOnly
}
```

## Circuit Breaker Implementation

```csharp
public class CircuitBreaker : ICircuitBreaker
{
    private readonly ConcurrentDictionary<string, CircuitBreakerState> _states = new();
    private readonly RiskConfiguration _config;
    private readonly ILogger<CircuitBreaker> _logger;
    
    public async Task<bool> CheckAsync(string account, PortfolioSnapshot portfolio)
    {
        var state = _states.GetOrAdd(account, _ => new CircuitBreakerState { Account = account });
        
        // Update state
        state.DailyLossPercent = (double)(-portfolio.DailyPnL / portfolio.NetLiquidation);
        state.ConsecutiveLosses = portfolio.ConsecutiveLosses;
        state.CurrentVolatility = await GetCurrentVolatilityAsync();
        
        // Check rules
        foreach (var rule in _config.CircuitBreakerRules)
        {
            if (rule.Trigger(state))
            {
                await TriggerCircuitBreakerAsync(state, rule);
                return true;
            }
        }
        
        return state.IsHalted;
    }
    
    private async Task TriggerCircuitBreakerAsync(CircuitBreakerState state, CircuitBreakerRule rule)
    {
        if (state.ActiveRules.Contains(rule.Name))
            return;  // Already triggered
            
        state.ActiveRules.Add(rule.Name);
        state.LastTriggered = DateTime.UtcNow;
        
        switch (rule.Action)
        {
            case CircuitBreakerAction.HaltTrading:
            case CircuitBreakerAction.HaltTradingForDay:
                state.IsHalted = true;
                state.ResumeTime = GetNextMarketOpen();
                break;
                
            case CircuitBreakerAction.HaltTradingForHours:
                state.IsHalted = true;
                state.ResumeTime = DateTime.UtcNow + rule.Duration.Value;
                break;
                
            case CircuitBreakerAction.ReducePositionSize:
                state.PositionSizeMultiplier = rule.PositionSizeMultiplier.Value;
                break;
                
            case CircuitBreakerAction.RequireHigherConfidence:
                state.RequiredConfidenceMultiplier = 1.2m;
                break;
        }
        
        _logger.LogCritical(
            "Circuit breaker {RuleName} triggered for account {Account}. Action: {Action}",
            rule.Name, state.Account, rule.Action);
            
        await NotifyAsync(state, rule);
    }
    
    public async Task ResetAsync(string account)
    {
        if (_states.TryGetValue(account, out var state))
        {
            state.IsHalted = false;
            state.ActiveRules.Clear();
            state.PositionSizeMultiplier = 1.0m;
            state.RequiredConfidenceMultiplier = 1.0m;
            
            _logger.LogInformation("Circuit breaker reset for account {Account}", account);
        }
    }
}

public class CircuitBreakerState
{
    public string Account { get; set; }
    public bool IsHalted { get; set; }
    public DateTime? ResumeTime { get; set; }
    public HashSet<string> ActiveRules { get; set; } = new();
    public DateTime? LastTriggered { get; set; }
    
    // Metrics
    public double DailyLossPercent { get; set; }
    public int ConsecutiveLosses { get; set; }
    public double PeakToTroughDrawdown { get; set; }
    public double CurrentVolatility { get; set; }
    public double AverageVolatility { get; set; }
    
    // Adjustments
    public decimal PositionSizeMultiplier { get; set; } = 1.0m;
    public decimal RequiredConfidenceMultiplier { get; set; } = 1.0m;
}
```

## Position Management

```csharp
public interface IPositionManager
{
    Task<Position> OpenPositionAsync(OpenPositionRequest request);
    Task<Position> ScaleInAsync(string positionId, int additionalQuantity);
    Task<Position> ScaleOutAsync(string positionId, int reduceQuantity);
    Task<Position> ClosePositionAsync(string positionId, string reason);
    Task UpdateStopLossAsync(string positionId, double newStopPrice);
    Task UpdateTakeProfitAsync(string positionId, double newTargetPrice);
}

public class PositionManager : IPositionManager
{
    private readonly IPositionRepository _repository;
    private readonly IExecutionAdapter _execution;
    private readonly IRiskEngine _risk;
    
    public async Task<Position> OpenPositionAsync(OpenPositionRequest request)
    {
        // Validate with risk engine
        var check = await _risk.ValidateSignalAsync(
            new TradingSignal 
            { 
                Instrument = request.Instrument,
                Direction = request.Direction,
                AIConfidence = request.AIConfidence
            }, 
            await GetPortfolioAsync());
            
        if (!check.IsAllowed)
        {
            throw new RiskException($"Position rejected: {check.Reason}");
        }
        
        // Calculate stop loss and take profit
        var stopLoss = request.Direction == TradeDirection.Long
            ? request.EntryPrice * (1 - request.StopLossPercent)
            : request.EntryPrice * (1 + request.StopLossPercent);
            
        var takeProfit = request.Direction == TradeDirection.Long
            ? request.EntryPrice * (1 + request.TakeProfitPercent)
            : request.EntryPrice * (1 - request.TakeProfitPercent);
        
        // Create position
        var position = new Position
        {
            Instrument = request.Instrument,
            Side = request.Direction == TradeDirection.Long ? PositionSide.Long : PositionSide.Short,
            Quantity = (int)check.AllowedSize,
            AverageEntryPrice = request.EntryPrice,
            InitialStopLoss = stopLoss,
            TakeProfit = takeProfit,
            RiskAmount = (decimal)(request.EntryPrice - stopLoss) * check.AllowedSize,
            OpenedAt = DateTime.UtcNow,
            State = PositionState.Opening
        };
        
        // Submit entry order
        var order = await _execution.SubmitOrderAsync(new OrderRequest
        {
            Instrument = request.Instrument,
            Action = request.Direction == TradeDirection.Long ? OrderAction.Buy : OrderAction.Sell,
            OrderType = request.OrderType,
            Quantity = position.Quantity,
            LimitPrice = request.LimitPrice,
            StopPrice = request.StopPrice
        });
        
        // Submit OCO bracket (stop loss + take profit)
        var ocoGroup = Guid.NewGuid().ToString();
        
        await _execution.SubmitOrderAsync(new OrderRequest
        {
            Instrument = request.Instrument,
            Action = request.Direction == TradeDirection.Long ? OrderAction.Sell : OrderAction.Buy,
            OrderType = OrderType.StopMarket,
            Quantity = position.Quantity,
            StopPrice = stopLoss,
            OCOId = ocoGroup
        });
        
        await _execution.SubmitOrderAsync(new OrderRequest
        {
            Instrument = request.Instrument,
            Action = request.Direction == TradeDirection.Long ? OrderAction.Sell : OrderAction.Buy,
            OrderType = OrderType.Limit,
            Quantity = position.Quantity,
            LimitPrice = takeProfit,
            OCOId = ocoGroup
        });
        
        await _repository.SaveAsync(position);
        return position;
    }
    
    public async Task<Position> ScaleInAsync(string positionId, int additionalQuantity)
    {
        var position = await _repository.GetAsync(positionId);
        if (position == null)
            throw new NotFoundException($"Position {positionId} not found");
            
        // Check if scaling is allowed
        var maxPosition = await _risk.GetMaxPositionSizeAsync(position.Instrument);
        if (position.Quantity + additionalQuantity > maxPosition)
        {
            throw new RiskException("Scale-in would exceed maximum position size");
        }
        
        position.State = PositionState.ScalingIn;
        
        // Submit additional entry order
        // ... implementation
        
        return position;
    }
    
    public async Task UpdateTrailingStopAsync(string positionId, double trailDistance)
    {
        var position = await _repository.GetAsync(positionId);
        
        // Calculate new stop based on current price and trail distance
        var newStop = position.Side == PositionSide.Long
            ? position.CurrentPrice * (1 - trailDistance)
            : position.CurrentPrice * (1 + trailDistance);
        
        // Only move stop in favorable direction
        if (position.Side == PositionSide.Long && newStop > position.TrailingStop)
        {
            position.TrailingStop = newStop;
            await UpdateStopLossOrderAsync(position, newStop);
        }
        else if (position.Side == PositionSide.Short && newStop < position.TrailingStop)
        {
            position.TrailingStop = newStop;
            await UpdateStopLossOrderAsync(position, newStop);
        }
        
        await _repository.SaveAsync(position);
    }
}
```

## Risk Monitoring Dashboard

```csharp
public class RiskMonitor
{
    // Real-time metrics
    public async Task<RiskDashboard> GetDashboardAsync(string account)
    {
        var portfolio = await GetPortfolioAsync(account);
        var circuitBreaker = await GetCircuitBreakerStateAsync(account);
        
        return new RiskDashboard
        {
            Account = account,
            Timestamp = DateTime.UtcNow,
            
            // Current status
            IsTradingAllowed = !circuitBreaker.IsHalted,
            ActiveCircuitBreakers = circuitBreaker.ActiveRules.ToList(),
            
            // Limits
            DailyLossLimit = portfolio.NetLiquidation * 0.03m,
            DailyLossCurrent = -portfolio.DailyPnL,
            DailyLossRemaining = portfolio.NetLiquidation * 0.03m + portfolio.DailyPnL,
            
            PerTradeRiskLimit = portfolio.NetLiquidation * 0.01m,
            MaxPositionSize = 10,
            MaxOpenPositions = 5,
            
            // Current utilization
            OpenPositions = portfolio.OpenPositionCount,
            PortfolioHeat = portfolio.PortfolioHeat,
            TotalExposure = portfolio.TotalExposure,
            AvailableBuyingPower = portfolio.BuyingPower,
            
            // Performance
            ConsecutiveLosses = portfolio.ConsecutiveLosses,
            MaxDrawdownToday = portfolio.MaxDrawdownToday,
            WinRateToday = await CalculateWinRateAsync(account, DateTime.UtcNow.Date),
            
            // AI metrics
            AverageConfidence = await GetAverageConfidenceAsync(account, DateTime.UtcNow.Date),
            ConfidenceAccuracy = await GetConfidenceAccuracyAsync(account)
        };
    }
}

public class RiskDashboard
{
    public string Account { get; set; }
    public DateTime Timestamp { get; set; }
    
    public bool IsTradingAllowed { get; set; }
    public List<string> ActiveCircuitBreakers { get; set; }
    
    public decimal DailyLossLimit { get; set; }
    public decimal DailyLossCurrent { get; set; }
    public decimal DailyLossRemaining { get; set; }
    public decimal DailyLossUtilization => DailyLossLimit > 0 ? DailyLossCurrent / DailyLossLimit : 0;
    
    public decimal PerTradeRiskLimit { get; set; }
    public decimal MaxPositionSize { get; set; }
    public int MaxOpenPositions { get; set; }
    
    public int OpenPositions { get; set; }
    public decimal PortfolioHeat { get; set; }
    public decimal TotalExposure { get; set; }
    public decimal AvailableBuyingPower { get; set; }
    
    public int ConsecutiveLosses { get; set; }
    public decimal MaxDrawdownToday { get; set; }
    public decimal WinRateToday { get; set; }
    
    public double AverageConfidence { get; set; }
    public double ConfidenceAccuracy { get; set; }
}
```
