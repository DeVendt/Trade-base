# Trading Strategy Framework

## Overview

The Strategy Framework provides a flexible, extensible architecture for implementing trading strategies. It separates strategy logic from execution, enabling easy development, testing, and optimization of AI-driven strategies.

## Strategy Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    STRATEGY FRAMEWORK ARCHITECTURE                           │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                    STRATEGY ORCHESTRATOR                             │    │
│  │  - Manages strategy lifecycle                                        │    │
│  │  - Routes market data to strategies                                  │    │
│  │  - Collects strategy signals                                         │    │
│  │  - Handles resource allocation                                       │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                    │                                         │
│              ┌─────────────────────┼─────────────────────┐                   │
│              ▼                     ▼                     ▼                   │
│  ┌─────────────────┐   ┌─────────────────┐   ┌─────────────────┐            │
│  │   Strategy A    │   │   Strategy B    │   │   Strategy N    │            │
│  │                 │   │                 │   │                 │            │
│  │ ┌───────────┐   │   │ ┌───────────┐   │   │ ┌───────────┐   │            │
│  │ │  Signal   │   │   │ │  Signal   │   │   │ │  Signal   │   │            │
│  │ │ Generator │   │   │ │ Generator │   │   │ │ Generator │   │            │
│  │ └─────┬─────┘   │   │ └─────┬─────┘   │   │ └─────┬─────┘   │            │
│  │       │         │   │       │         │   │       │         │            │
│  │ ┌─────▼─────┐   │   │ ┌─────▼─────┐   │   │ ┌─────▼─────┐   │            │
│  │ │    AI     │   │   │ │    AI     │   │   │ │    AI     │   │            │
│  │ │  Engine   │◄──┼───┼►│  Engine   │◄──┼───┼►│  Engine   │   │            │
│  │ └─────┬─────┘   │   │ └─────┬─────┘   │   │ └─────┬─────┘   │            │
│  │       │         │   │       │         │   │       │         │            │
│  │ ┌─────▼─────┐   │   │ ┌─────▼─────┐   │   │ ┌─────▼─────┐   │            │
│  │ │   Risk    │   │   │ │   Risk    │   │   │ │   Risk    │   │            │
│  │ │  Filter   │   │   │ │  Filter   │   │   │ │  Filter   │   │            │
│  │ └─────┬─────┘   │   │ └─────┬─────┘   │   │ └─────┬─────┘   │            │
│  │       │         │   │       │         │   │       │         │            │
│  │ ┌─────▼─────┐   │   │ ┌─────▼─────┐   │   │ ┌─────▼─────┐   │            │
│  │ │  Signal   │   │   │ │  Signal   │   │   │ │  Signal   │   │            │
│  │ │  Output   │   │   │ │  Output   │   │   │ │  Output   │   │            │
│  │ └───────────┘   │   │ └───────────┘   │   │ └───────────┘   │            │
│  └─────────────────┘   └─────────────────┘   └─────────────────┘            │
│                                                                              │
│  Each strategy implements:                                                   │
│  - IStrategy interface                                                       │
│  - Custom signal generation logic                                            │
│  - AI integration for decision enhancement                                   │
│  - Risk-aware position sizing                                                │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Strategy Interface

```csharp
public interface IStrategy : IDisposable
{
    string Id { get; }
    string Name { get; }
    StrategyType Type { get; }
    StrategyState State { get; }
    
    // Lifecycle
    Task InitializeAsync(StrategyConfiguration config, IServiceProvider services);
    Task StartAsync();
    Task StopAsync();
    Task PauseAsync();
    Task ResumeAsync();
    
    // Data handling
    Task OnMarketDataAsync(MarketData data);
    Task OnBarAsync(Bar bar);
    Task OnTickAsync(Tick tick);
    
    // Position updates
    Task OnPositionUpdateAsync(Position position);
    Task OnOrderUpdateAsync(Order order);
    
    // Signal output
    event EventHandler<StrategySignalEventArgs> SignalGenerated;
}

public abstract class StrategyBase : IStrategy
{
    protected ILogger Logger { get; set; }
    protected IExecutionAdapter Execution { get; set; }
    protected IRiskEngine Risk { get; set; }
    protected IMarketDataCache MarketData { get; set; }
    protected IAIDecisionEngine AI { get; set; }
    protected IPositionManager PositionManager { get; set; }
    
    protected StrategyConfiguration Config { get; private set; }
    protected StrategyState State { get; private set; }
    
    public abstract string Name { get; }
    public abstract StrategyType Type { get; }
    public string Id { get; } = Guid.NewGuid().ToString();
    
    public virtual async Task InitializeAsync(StrategyConfiguration config, IServiceProvider services)
    {
        Config = config;
        Logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(GetType());
        Execution = services.GetRequiredService<IExecutionAdapter>();
        Risk = services.GetRequiredService<IRiskEngine>();
        MarketData = services.GetRequiredService<IMarketDataCache>();
        AI = services.GetRequiredService<IAIDecisionEngine>();
        PositionManager = services.GetRequiredService<IPositionManager>();
        
        State = StrategyState.Initialized;
        Logger.LogInformation("Strategy {Name} initialized", Name);
    }
    
    public virtual async Task StartAsync()
    {
        State = StrategyState.Running;
        Logger.LogInformation("Strategy {Name} started", Name);
    }
    
    public virtual async Task StopAsync()
    {
        State = StrategyState.Stopped;
        Logger.LogInformation("Strategy {Name} stopped", Name);
    }
    
    public event EventHandler<StrategySignalEventArgs> SignalGenerated;
    
    protected virtual void EmitSignal(StrategySignal signal)
    {
        SignalGenerated?.Invoke(this, new StrategySignalEventArgs { Signal = signal });
    }
    
    public abstract Task OnBarAsync(Bar bar);
    public abstract Task OnTickAsync(Tick tick);
}
```

## Strategy Types

### 1. Trend Following Strategy

```csharp
public class TrendFollowingStrategy : StrategyBase
{
    public override string Name => "AI Trend Follower";
    public override StrategyType Type => StrategyType.TrendFollowing;
    
    private readonly Dictionary<string, TrendState> _trendStates = new();
    
    public override async Task OnBarAsync(Bar bar)
    {
        if (State != StrategyState.Running)
            return;
            
        var instrument = bar.Instrument;
        
        // Get required data
        var bars = await MarketData.GetBarsAsync(instrument, TimeSpan.FromMinutes(5), 50);
        if (bars.Count < 20)
            return;
            
        // Calculate trend indicators
        var sma20 = CalculateSMA(bars, 20);
        var sma50 = CalculateSMA(bars, 50);
        var atr = CalculateATR(bars, 14);
        
        // Get AI decision
        var features = ExtractFeatures(bars, bar);
        var aiDecision = await AI.EvaluateAsync(features);
        
        // Check current position
        var position = await PositionManager.GetPositionAsync(instrument);
        
        // Generate signal based on AI + technical confirmation
        var signal = GenerateSignal(bar, sma20, sma50, atr, aiDecision, position);
        
        if (signal != null)
        {
            EmitSignal(signal);
        }
    }
    
    private StrategySignal GenerateSignal(Bar bar, double sma20, double sma50, double atr, 
        AIDecision ai, Position position)
    {
        // Must have sufficient AI confidence
        if (ai.Confidence < Config.MinConfidence)
            return null;
            
        var trendUp = sma20 > sma50 && bar.Close > sma20;
        var trendDown = sma20 < sma50 && bar.Close < sma20;
        
        // Long signal
        if (ai.Action == TradeAction.Buy && trendUp && (position?.Side != PositionSide.Long))
        {
            return new StrategySignal
            {
                Instrument = bar.Instrument,
                Direction = TradeDirection.Long,
                EntryPrice = bar.Close,
                StopLoss = bar.Close - (atr * 2),
                TakeProfit = bar.Close + (atr * 4),
                AIConfidence = ai.Confidence,
                ExpectedReturn = ai.ExpectedReturn,
                RiskScore = ai.RiskScore,
                Rationale = $"AI Buy ({ai.Confidence:P}) + Uptrend confirmed"
            };
        }
        
        // Short signal
        if (ai.Action == TradeAction.Sell && trendDown && (position?.Side != PositionSide.Short))
        {
            return new StrategySignal
            {
                Instrument = bar.Instrument,
                Direction = TradeDirection.Short,
                EntryPrice = bar.Close,
                StopLoss = bar.Close + (atr * 2),
                TakeProfit = bar.Close - (atr * 4),
                AIConfidence = ai.Confidence,
                ExpectedReturn = ai.ExpectedReturn,
                RiskScore = ai.RiskScore,
                Rationale = $"AI Sell ({ai.Confidence:P}) + Downtrend confirmed"
            };
        }
        
        // Exit signal
        if (position != null)
        {
            var shouldExit = (position.Side == PositionSide.Long && ai.Action == TradeAction.Sell) ||
                           (position.Side == PositionSide.Short && ai.Action == TradeAction.Buy);
                           
            if (shouldExit && ai.Confidence > 0.7)
            {
                return new StrategySignal
                {
                    Instrument = bar.Instrument,
                    Direction = TradeDirection.Close,
                    ExitReason = $"AI reversal signal ({ai.Confidence:P})"
                };
            }
        }
        
        return null;
    }
}
```

### 2. Mean Reversion Strategy

```csharp
public class MeanReversionStrategy : StrategyBase
{
    public override string Name => "AI Mean Reversion";
    public override StrategyType Type => StrategyType.MeanReversion;
    
    public override async Task OnBarAsync(Bar bar)
    {
        if (State != StrategyState.Running)
            return;
            
        var bars = await MarketData.GetBarsAsync(bar.Instrument, TimeSpan.FromMinutes(5), 30);
        if (bars.Count < 20)
            return;
            
        // Calculate mean reversion indicators
        var sma = CalculateSMA(bars, 20);
        var stdDev = CalculateStdDev(bars, 20);
        var zScore = (bar.Close - sma) / stdDev;
        var rsi = CalculateRSI(bars, 14);
        var bollingerPosition = CalculateBollingerPosition(bar.Close, sma, stdDev);
        
        // Get AI decision
        var features = ExtractFeatures(bars, bar);
        features.ZScore = zScore;
        features.RSI = rsi;
        features.BollingerPosition = bollingerPosition;
        
        var aiDecision = await AI.EvaluateAsync(features);
        
        // Only trade extreme deviations with AI confirmation
        if (Math.Abs(zScore) < 1.5)
            return;  // Not extended enough
            
        var position = await PositionManager.GetPositionAsync(bar.Instrument);
        
        // Oversold + AI confirms reversal up
        if (zScore < -2 && rsi < 30 && aiDecision.Action == TradeAction.Buy)
        {
            EmitSignal(new StrategySignal
            {
                Instrument = bar.Instrument,
                Direction = TradeDirection.Long,
                EntryPrice = bar.Close,
                StopLoss = bar.Close - (stdDev * 2),
                TakeProfit = sma,  // Target: return to mean
                AIConfidence = aiDecision.Confidence,
                Rationale = $"Mean reversion: Z={zScore:F2}, RSI={rsi:F0}, AI={aiDecision.Confidence:P}"
            });
        }
        
        // Overbought + AI confirms reversal down
        if (zScore > 2 && rsi > 70 && aiDecision.Action == TradeAction.Sell)
        {
            EmitSignal(new StrategySignal
            {
                Instrument = bar.Instrument,
                Direction = TradeDirection.Short,
                EntryPrice = bar.Close,
                StopLoss = bar.Close + (stdDev * 2),
                TakeProfit = sma,
                AIConfidence = aiDecision.Confidence,
                Rationale = $"Mean reversion: Z={zScore:F2}, RSI={rsi:F0}, AI={aiDecision.Confidence:P}"
            });
        }
    }
}
```

### 3. Breakout Strategy

```csharp
public class BreakoutStrategy : StrategyBase
{
    public override string Name => "AI Breakout";
    public override StrategyType Type => StrategyType.Breakout;
    
    private readonly Dictionary<string, PriceLevels> _levels = new();
    
    public override async Task OnBarAsync(Bar bar)
    {
        if (State != StrategyState.Running)
            return;
            
        var bars = await MarketData.GetBarsAsync(bar.Instrument, TimeSpan.FromMinutes(5), 50);
        
        // Calculate support/resistance levels
        var high20 = bars.Take(20).Max(b => b.High);
        var low20 = bars.Take(20).Min(b => b.Low);
        var volumeAvg = bars.Take(20).Average(b => b.Volume);
        
        // Check for breakout with volume confirmation
        var isBreakoutUp = bar.Close > high20 && bar.Volume > volumeAvg * 1.5;
        var isBreakoutDown = bar.Close < low20 && bar.Volume > volumeAvg * 1.5;
        
        if (!isBreakoutUp && !isBreakoutDown)
            return;
            
        // Get AI confirmation
        var features = ExtractFeatures(bars, bar);
        var aiDecision = await AI.EvaluateAsync(features);
        
        if (aiDecision.Confidence < Config.MinConfidence)
            return;
            
        var position = await PositionManager.GetPositionAsync(bar.Instrument);
        
        if (isBreakoutUp && aiDecision.Action == TradeAction.Buy && 
            position?.Side != PositionSide.Long)
        {
            EmitSignal(new StrategySignal
            {
                Instrument = bar.Instrument,
                Direction = TradeDirection.Long,
                EntryPrice = bar.Close,
                StopLoss = low20,
                TakeProfit = bar.Close + (bar.Close - low20) * 2,
                AIConfidence = aiDecision.Confidence,
                Rationale = $"Breakout above {high20:F2} with volume, AI={aiDecision.Confidence:P}"
            });
        }
        
        if (isBreakoutDown && aiDecision.Action == TradeAction.Sell &&
            position?.Side != PositionSide.Short)
        {
            EmitSignal(new StrategySignal
            {
                Instrument = bar.Instrument,
                Direction = TradeDirection.Short,
                EntryPrice = bar.Close,
                StopLoss = high20,
                TakeProfit = bar.Close - (high20 - bar.Close) * 2,
                AIConfidence = aiDecision.Confidence,
                Rationale = $"Breakout below {low20:F2} with volume, AI={aiDecision.Confidence:P}"
            });
        }
    }
}
```

## Signal Structure

```csharp
public class StrategySignal
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string StrategyId { get; set; }
    public string Instrument { get; set; }
    
    // Trade direction
    public TradeDirection Direction { get; set; }
    
    // Entry parameters
    public double EntryPrice { get; set; }
    public OrderType EntryOrderType { get; set; } = OrderType.Limit;
    public double? LimitPrice { get; set; }
    public double? StopPrice { get; set; }
    
    // Risk parameters
    public double StopLoss { get; set; }
    public double? TakeProfit { get; set; }
    public double? TrailingStopDistance { get; set; }
    
    // AI metrics
    public double AIConfidence { get; set; }
    public double ExpectedReturn { get; set; }
    public double RiskScore { get; set; }
    public MarketRegime DetectedRegime { get; set; }
    
    // Position management
    public int? ScaleInQuantity { get; set; }
    public string ExitReason { get; set; }
    
    // Context
    public string Rationale { get; set; }
    public FeatureVector Features { get; set; }
}

public enum TradeDirection
{
    Long,    // Buy to open
    Short,   // Sell to open
    Close,   // Close existing position
    ScaleIn, // Add to position
    ScaleOut // Reduce position
}
```

## Strategy Orchestrator

```csharp
public interface IStrategyOrchestrator
{
    Task RegisterStrategyAsync(IStrategy strategy);
    Task UnregisterStrategyAsync(string strategyId);
    Task StartStrategyAsync(string strategyId);
    Task StopStrategyAsync(string strategyId);
    Task StartAllAsync();
    Task StopAllAsync();
    
    IReadOnlyList<IStrategy> RunningStrategies { get; }
    event EventHandler<StrategySignalEventArgs> StrategySignal;
}

public class StrategyOrchestrator : IStrategyOrchestrator
{
    private readonly ConcurrentDictionary<string, IStrategy> _strategies = new();
    private readonly IMarketDataAdapter _marketData;
    private readonly IExecutionAdapter _execution;
    private readonly ILogger<StrategyOrchestrator> _logger;
    
    public StrategyOrchestrator(
        IMarketDataAdapter marketData,
        IExecutionAdapter execution,
        ILogger<StrategyOrchestrator> logger)
    {
        _marketData = marketData;
        _execution = execution;
        _logger = logger;
        
        // Subscribe to market data
        _marketData.BarReceived += OnBarReceived;
        _marketData.TickReceived += OnTickReceived;
    }
    
    public async Task RegisterStrategyAsync(IStrategy strategy)
    {
        if (_strategies.TryAdd(strategy.Id, strategy))
        {
            strategy.SignalGenerated += OnStrategySignal;
            _logger.LogInformation("Strategy {Name} registered with ID {Id}", strategy.Name, strategy.Id);
        }
    }
    
    private async void OnBarReceived(object sender, BarEventArgs e)
    {
        foreach (var strategy in _strategies.Values.Where(s => s.State == StrategyState.Running))
        {
            try
            {
                await strategy.OnBarAsync(e.Bar);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing bar in strategy {Strategy}", strategy.Name);
            }
        }
    }
    
    private async void OnStrategySignal(object sender, StrategySignalEventArgs e)
    {
        var strategy = (IStrategy)sender;
        
        _logger.LogInformation(
            "Signal from {Strategy}: {Direction} {Instrument} @ {Price:F2} (Confidence: {Confidence:P})",
            strategy.Name, e.Signal.Direction, e.Signal.Instrument, 
            e.Signal.EntryPrice, e.Signal.AIConfidence);
            
        // Forward to risk engine and execution
        StrategySignal?.Invoke(this, e);
    }
    
    public IReadOnlyList<IStrategy> RunningStrategies => 
        _strategies.Values.Where(s => s.State == StrategyState.Running).ToList();
        
    public event EventHandler<StrategySignalEventArgs> StrategySignal;
}
```

## Strategy Factory

```csharp
public interface IStrategyFactory
{
    IStrategy CreateStrategy(string strategyType, StrategyConfiguration config);
}

public class StrategyFactory : IStrategyFactory
{
    private readonly IServiceProvider _services;
    private readonly Dictionary<string, Type> _strategyTypes = new();
    
    public StrategyFactory(IServiceProvider services)
    {
        _services = services;
        
        // Register built-in strategies
        RegisterStrategyType<TrendFollowingStrategy>("trend");
        RegisterStrategyType<MeanReversionStrategy>("meanreversion");
        RegisterStrategyType<BreakoutStrategy>("breakout");
    }
    
    public void RegisterStrategyType<T>(string key) where T : IStrategy
    {
        _strategyTypes[key.ToLower()] = typeof(T);
    }
    
    public IStrategy CreateStrategy(string strategyType, StrategyConfiguration config)
    {
        if (!_strategyTypes.TryGetValue(strategyType.ToLower(), out var type))
        {
            throw new ArgumentException($"Unknown strategy type: {strategyType}");
        }
        
        var strategy = (IStrategy)ActivatorUtilities.CreateInstance(_services, type);
        strategy.InitializeAsync(config, _services).Wait();
        
        return strategy;
    }
}
```

## Configuration

```json
{
  "Strategies": [
    {
      "Type": "trend",
      "Name": "ES Trend Follower",
      "Enabled": true,
      "Instruments": ["ES 03-25"],
      "Configuration": {
        "AnalysisTimeframes": ["00:05:00", "00:15:00"],
        "MaxRiskPerTrade": 0.01,
        "MaxDailyRisk": 0.03,
        "MaxPositionSize": 5,
        "MinConfidence": 0.65,
        "UseTrailingStop": true,
        "TrailingStopDistance": 0.015
      }
    },
    {
      "Type": "meanreversion",
      "Name": "NQ Mean Reversion",
      "Enabled": true,
      "Instruments": ["NQ 03-25"],
      "Configuration": {
        "AnalysisTimeframes": ["00:05:00"],
        "MaxRiskPerTrade": 0.01,
        "MaxPositionSize": 3,
        "MinConfidence": 0.70,
        "EntryTimeoutBars": 2
      }
    }
  ]
}
```
