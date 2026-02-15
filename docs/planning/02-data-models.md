# Data Models and Domain Design

## Domain Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           DOMAIN ENTITIES                                    │
│                                                                              │
│  ┌────────────┐   ┌────────────┐   ┌────────────┐   ┌────────────┐         │
│  │ Instrument │   │   Market   │   │    Bar     │   │    Tick    │         │
│  │   (Asset)  │   │   Data     │   │   (OHLCV)  │   │   (Trade)  │         │
│  └────────────┘   └────────────┘   └────────────┘   └────────────┘         │
│                                                                              │
│  ┌────────────┐   ┌────────────┐   ┌────────────┐   ┌────────────┐         │
│  │  Strategy  │   │    AI      │   │    Risk    │   │ Portfolio  │         │
│  │            │   │  Decision  │   │   Limits   │   │            │         │
│  └────────────┘   └────────────┘   └────────────┘   └────────────┘         │
│                                                                              │
│  ┌────────────┐   ┌────────────┐   ┌────────────┐   ┌────────────┐         │
│  │    Order   │   │   Position │   │    Fill    │   │  P&L/Trade │         │
│  │            │   │            │   │            │   │  History   │         │
│  └────────────┘   └────────────┘   └────────────┘   └────────────┘         │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Core Entities

### Instrument

```csharp
public class Instrument
{
    public string Symbol { get; set; }
    public string Name { get; set; }
    public AssetType Type { get; set; }
    public Exchange Exchange { get; set; }
    public string Currency { get; set; }
    
    // Trading specifications
    public double TickSize { get; set; }
    public double PointValue { get; set; }
    public double MarginRequirement { get; set; }
    public TradingHours TradingHours { get; set; }
    
    // AI/Strategy config
    public decimal VolatilityScalingFactor { get; set; } = 1.0m;
    public decimal MaxPositionSize { get; set; }
}

public enum AssetType
{
    Future,
    Stock,
    Forex,
    Option,
    Crypto,
    Index
}

public class TradingHours
{
    public TimeSpan PreMarketStart { get; set; }
    public TimeSpan RegularStart { get; set; }
    public TimeSpan RegularEnd { get; set; }
    public TimeSpan PostMarketEnd { get; set; }
    public HashSet<DayOfWeek> TradingDays { get; set; }
    public TimeZoneInfo TimeZone { get; set; }
}
```

### Market Data

```csharp
public abstract class MarketData
{
    public string Instrument { get; set; }
    public DateTime Timestamp { get; set; }
    public DataSource Source { get; set; }
}

public class Tick : MarketData
{
    public double Price { get; set; }
    public long Volume { get; set; }
    public TickType Type { get; set; }
    public string Exchange { get; set; }
}

public class Bar : MarketData
{
    public double Open { get; set; }
    public double High { get; set; }
    public double Low { get; set; }
    public double Close { get; set; }
    public long Volume { get; set; }
    public TimeSpan Period { get; set; }  // 1m, 5m, 1h, etc.
    public int Trades { get; set; }  // Number of trades in bar
    
    // Computed properties
    public double Range => High - Low;
    public double Body => Math.Abs(Close - Open);
    public bool IsBullish => Close > Open;
}

public class MarketDepth : MarketData
{
    public List<PriceLevel> Bids { get; set; } = new();
    public List<PriceLevel> Asks { get; set; } = new();
    
    public class PriceLevel
    {
        public double Price { get; set; }
        public long Volume { get; set; }
        public int OrderCount { get; set; }
    }
}
```

### Strategy

```csharp
public class Strategy
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; }
    public string Description { get; set; }
    public StrategyType Type { get; set; }
    
    // Configuration
    public StrategyConfiguration Configuration { get; set; }
    
    // State
    public StrategyState State { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? StoppedAt { get; set; }
    
    // Statistics
    public StrategyPerformance Performance { get; set; }
    
    // Relationships
    public List<string> Instruments { get; set; } = new();
    public List<StrategyRule> Rules { get; set; } = new();
}

public class StrategyConfiguration
{
    // Timeframes
    public List<TimeSpan> AnalysisTimeframes { get; set; } = new();
    
    // Risk parameters
    public decimal MaxRiskPerTrade { get; set; } = 0.01m;  // 1% of account
    public decimal MaxDailyRisk { get; set; } = 0.03m;     // 3% of account
    public decimal MaxPositionSize { get; set; } = 5;      // Max contracts/shares
    
    // AI parameters
    public decimal MinConfidenceThreshold { get; set; } = 0.6m;
    public decimal TargetConfidence { get; set; } = 0.8m;
    
    // Entry/Exit
    public int EntryTimeoutBars { get; set; } = 3;
    public int MaxHoldingBars { get; set; } = 20;
    public bool UseTrailingStop { get; set; } = true;
    public decimal TrailingStopDistance { get; set; } = 0.01m;  // 1%
}

public enum StrategyState
{
    Created,
    Starting,
    Running,
    Paused,
    Stopping,
    Stopped,
    Error
}
```

## Trading Entities

### Order

```csharp
public class Order
{
    public string Id { get; set; }  // Internal ID
    public string BrokerOrderId { get; set; }  // NinjaTrader order ID
    public string ClientOrderId { get; set; }  // For tracking
    
    // References
    public string StrategyId { get; set; }
    public string Instrument { get; set; }
    public string Account { get; set; }
    
    // Order details
    public OrderAction Action { get; set; }
    public OrderType Type { get; set; }
    public int Quantity { get; set; }
    public double? LimitPrice { get; set; }
    public double? StopPrice { get; set; }
    public TimeInForce TimeInForce { get; set; }
    
    // OCO (One-Cancels-Other)
    public string OCOGroup { get; set; }
    
    // State
    public OrderState State { get; set; }
    public int FilledQuantity { get; set; }
    public int RemainingQuantity => Quantity - FilledQuantity;
    public double AverageFillPrice { get; set; }
    public List<Fill> Fills { get; set; } = new();
    
    // Timestamps
    public DateTime CreatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? FilledAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    
    // AI Context
    public AIDecisionContext AIContext { get; set; }
}

public class AIDecisionContext
{
    public double Confidence { get; set; }
    public double ExpectedReturn { get; set; }
    public double RiskScore { get; set; }
    public MarketRegime DetectedRegime { get; set; }
    public FeatureVector Features { get; set; }
    public string ModelVersion { get; set; }
}

public enum OrderState
{
    Pending,        // Created but not submitted
    Working,        // Submitted to market
    PartiallyFilled,
    Filled,
    CancelPending,
    Cancelled,
    Rejected,
    Expired
}
```

### Fill

```csharp
public class Fill
{
    public string Id { get; set; }
    public string OrderId { get; set; }
    public string Instrument { get; set; }
    
    public OrderAction Action { get; set; }
    public int Quantity { get; set; }
    public double Price { get; set; }
    
    public DateTime Timestamp { get; set; }
    public string Exchange { get; set; }
    public double Commission { get; set; }
    
    // Calculated
    public double Value => Quantity * Price;
    public PositionSide Side => Action == OrderAction.Buy || Action == OrderAction.BuyToCover 
        ? PositionSide.Long 
        : PositionSide.Short;
}
```

### Position

```csharp
public class Position
{
    public string Id { get; set; }
    public string Instrument { get; set; }
    public string Account { get; set; }
    public string StrategyId { get; set; }
    
    // Position details
    public PositionSide Side { get; set; }
    public int Quantity { get; set; }
    public double AverageEntryPrice { get; set; }
    public double? ExitPrice { get; set; }
    
    // Current market
    public double CurrentPrice { get; set; }
    public double UnrealizedPnL { get; set; }
    public double UnrealizedPnLPct => (CurrentPrice - AverageEntryPrice) / AverageEntryPrice;
    
    // P&L
    public double RealizedPnL { get; set; }
    public double TotalCommission { get; set; }
    
    // Timestamps
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public TimeSpan Duration => (ClosedAt ?? DateTime.UtcNow) - OpenedAt;
    
    // Risk
    public double InitialStopLoss { get; set; }
    public double? TrailingStop { get; set; }
    public double? TakeProfit { get; set; }
    public double RiskAmount { get; set; }  // $ at risk when entered
    
    // State
    public PositionState State { get; set; }
    public List<Fill> EntryFills { get; set; } = new();
    public List<Fill> ExitFills { get; set; } = new();
}

public enum PositionSide
{
    Long,   // Positive quantity
    Short,  // Negative quantity
    Flat    // Zero quantity
}

public enum PositionState
{
    Opening,      // Entry order working
    Open,         // Fully filled
    ScalingIn,    // Adding to position
    ScalingOut,   // Reducing position
    Closing,      // Exit order working
    Closed        // Fully exited
}
```

## AI Entities

### Feature Vector

```csharp
public class FeatureVector
{
    public string Instrument { get; set; }
    public DateTime Timestamp { get; set; }
    
    // Price features
    public double Returns1 { get; set; }
    public double Returns5 { get; set; }
    public double Returns10 { get; set; }
    public double Returns20 { get; set; }
    public double LogReturn { get; set; }
    public double Volatility { get; set; }
    
    // Trend features
    public double SMA10 { get; set; }
    public double SMA20 { get; set; }
    public double SMA50 { get; set; }
    public double EMA12 { get; set; }
    public double EMA26 { get; set; }
    public double MACD { get; set; }
    public double MACDSignal { get; set; }
    public double MACDHistogram { get; set; }
    
    // Momentum
    public double RSI14 { get; set; }
    public double StochasticK { get; set; }
    public double StochasticD { get; set; }
    public double WilliamsR { get; set; }
    public double CCI { get; set; }
    
    // Volatility
    public double ATR14 { get; set; }
    public double BollingerUpper { get; set; }
    public double BollingerMiddle { get; set; }
    public double BollingerLower { get; set; }
    public double BollingerWidth { get; set; }
    public double BollingerPosition { get; set; }
    
    // Volume
    public double Volume { get; set; }
    public double VolumeSMA20 { get; set; }
    public double RelativeVolume { get; set; }
    public double OBV { get; set; }
    public double VWAP { get; set; }
    public double MoneyFlowIndex { get; set; }
    
    // Market structure
    public double High20 { get; set; }
    public double Low20 { get; set; }
    public double Range20 { get; set; }
    public double PositionInRange { get; set; }
    
    // Normalize all features to 0-1 or -1 to 1 range for ML
    public double[] ToNormalizedArray()
    {
        // Implementation
        return Array.Empty<double>();
    }
}
```

### AIDecision

```csharp
public class AIDecision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; }
    public string Instrument { get; set; }
    public string StrategyId { get; set; }
    
    // Decision
    public TradeAction Action { get; set; }
    public double Confidence { get; set; }
    public decimal RecommendedPositionSize { get; set; }
    public TimeSpan? ExpectedHoldingTime { get; set; }
    
    // Model outputs
    public double[] ModelOutputs { get; set; }
    public MarketRegime DetectedRegime { get; set; }
    public double ExpectedReturn { get; set; }
    public double RiskScore { get; set; }
    
    // Input features (snapshot)
    public FeatureVector Features { get; set; }
    
    // Model metadata
    public string ModelVersion { get; set; }
    public TimeSpan InferenceTime { get; set; }
    
    // Decision rationale
    public string Rationale { get; set; }
    public List<string> Signals { get; set; } = new();
}

public enum TradeAction
{
    Buy,        // Enter long position
    Sell,       // Enter short position
    Hold,       // No action
    ScaleIn,    // Add to existing position
    ScaleOut,   // Reduce position
    Close       // Exit position completely
}

public enum MarketRegime
{
    TrendingUp,
    TrendingDown,
    Ranging,
    Volatile,
    Choppy,
    LowVolume,
    Unknown
}
```

## Risk Management Entities

```csharp
public class RiskProfile
{
    public string Account { get; set; }
    public decimal MaxAccountRiskPerDay { get; set; } = 0.03m;  // 3%
    public decimal MaxRiskPerTrade { get; set; } = 0.01m;       // 1%
    public int MaxOpenPositions { get; set; } = 5;
    public int MaxCorrelatedPositions { get; set; } = 2;
    public decimal MaxPositionSize { get; set; } = 10;
    
    // Circuit breakers
    public decimal DailyLossCircuitBreaker { get; set; } = 0.05m;  // 5%
    public int ConsecutiveLossesCircuitBreaker { get; set; } = 5;
}

public class RiskCheckResult
{
    public bool IsAllowed { get; set; }
    public string Reason { get; set; }
    public RiskViolationType? ViolationType { get; set; }
    public decimal AllowedSize { get; set; }
}

public enum RiskViolationType
{
    ExceedsMaxPositionSize,
    ExceedsDailyRisk,
    ExceedsPerTradeRisk,
    TooManyOpenPositions,
    CorrelatedPositions,
    DailyLossLimitReached,
    CircuitBreakerTriggered,
    MarginInsufficient,
    MarketConditionsUnfavorable
}

public class PortfolioSnapshot
{
    public DateTime Timestamp { get; set; }
    public string Account { get; set; }
    
    // Account metrics
    public decimal CashBalance { get; set; }
    public decimal BuyingPower { get; set; }
    public decimal NetLiquidation { get; set; }
    public decimal MarginUsed { get; set; }
    
    // P&L
    public decimal RealizedPnL { get; set; }
    public decimal UnrealizedPnL { get; set; }
    public decimal DailyPnL { get; set; }
    
    // Positions
    public List<Position> OpenPositions { get; set; } = new();
    public int OpenPositionCount => OpenPositions.Count;
    public decimal TotalExposure { get; set; }
    
    // Risk metrics
    public decimal PortfolioHeat => TotalExposure / NetLiquidation;
    public int ConsecutiveLosses { get; set; }
    public decimal MaxDrawdownToday { get; set; }
}
```

## Performance Entities

```csharp
public class TradeRecord
{
    public string Id { get; set; }
    public string StrategyId { get; set; }
    public string Instrument { get; set; }
    
    // Entry
    public DateTime EntryTime { get; set; }
    public double EntryPrice { get; set; }
    public int Quantity { get; set; }
    public PositionSide Side { get; set; }
    
    // Exit
    public DateTime? ExitTime { get; set; }
    public double? ExitPrice { get; set; }
    
    // P&L
    public double GrossPnL { get; set; }
    public double Commission { get; set; }
    public double NetPnL => GrossPnL - Commission;
    public double PnLPct { get; set; }
    
    // Metrics
    public double MAE { get; set; }  // Maximum adverse excursion
    public double MFE { get; set; }  // Maximum favorable excursion
    public TimeSpan Duration { get; set; }
    
    // Context
    public AIDecision EntryDecision { get; set; }
    public string ExitReason { get; set; }
}

public class StrategyPerformance
{
    public string StrategyId { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    
    // Trade statistics
    public int TotalTrades { get; set; }
    public int WinningTrades { get; set; }
    public int LosingTrades { get; set; }
    public decimal WinRate => TotalTrades > 0 ? (decimal)WinningTrades / TotalTrades : 0;
    
    // P&L
    public decimal GrossProfit { get; set; }
    public decimal GrossLoss { get; set; }
    public decimal NetProfit { get; set; }
    public decimal AverageWin => WinningTrades > 0 ? GrossProfit / WinningTrades : 0;
    public decimal AverageLoss => LosingTrades > 0 ? GrossLoss / LosingTrades : 0;
    public decimal ProfitFactor => GrossLoss != 0 ? GrossProfit / Math.Abs(GrossLoss) : 0;
    
    // Risk metrics
    public decimal MaxDrawdown { get; set; }
    public decimal SharpeRatio { get; set; }
    public decimal SortinoRatio { get; set; }
    
    // AI metrics
    public double AvgConfidence { get; set; }
    public double ConfidenceAccuracyCorrelation { get; set; }
}
```

## Database Schema (PostgreSQL/TimescaleDB)

```sql
-- Instruments table
CREATE TABLE instruments (
    symbol VARCHAR(20) PRIMARY KEY,
    name VARCHAR(100),
    asset_type VARCHAR(20),
    exchange VARCHAR(20),
    tick_size DECIMAL(18,8),
    point_value DECIMAL(18,8),
    margin_requirement DECIMAL(18,2),
    created_at TIMESTAMP DEFAULT NOW()
);

-- Market data hypertable (TimescaleDB)
CREATE TABLE market_data (
    time TIMESTAMPTZ NOT NULL,
    symbol VARCHAR(20) NOT NULL,
    open DECIMAL(18,8),
    high DECIMAL(18,8),
    low DECIMAL(18,8),
    close DECIMAL(18,8),
    volume BIGINT,
    period VARCHAR(10),
    PRIMARY KEY (time, symbol, period)
);

SELECT create_hypertable('market_data', 'time');

-- Orders table
CREATE TABLE orders (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    broker_order_id VARCHAR(50),
    strategy_id UUID,
    instrument VARCHAR(20),
    account VARCHAR(50),
    action VARCHAR(10),
    order_type VARCHAR(20),
    quantity INTEGER,
    limit_price DECIMAL(18,8),
    stop_price DECIMAL(18,8),
    state VARCHAR(20),
    filled_quantity INTEGER DEFAULT 0,
    average_fill_price DECIMAL(18,8),
    ai_confidence DECIMAL(4,3),
    created_at TIMESTAMP DEFAULT NOW(),
    submitted_at TIMESTAMP,
    filled_at TIMESTAMP,
    cancelled_at TIMESTAMP
);

-- Positions table
CREATE TABLE positions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    instrument VARCHAR(20),
    account VARCHAR(50),
    strategy_id UUID,
    side VARCHAR(10),
    quantity INTEGER,
    average_entry_price DECIMAL(18,8),
    exit_price DECIMAL(18,8),
    realized_pnl DECIMAL(18,2),
    unrealized_pnl DECIMAL(18,2),
    opened_at TIMESTAMP,
    closed_at TIMESTAMP,
    state VARCHAR(20)
);

-- AI decisions table
CREATE TABLE ai_decisions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    timestamp TIMESTAMP DEFAULT NOW(),
    instrument VARCHAR(20),
    strategy_id UUID,
    action VARCHAR(20),
    confidence DECIMAL(4,3),
    recommended_size DECIMAL(18,8),
    detected_regime VARCHAR(20),
    expected_return DECIMAL(18,8),
    risk_score DECIMAL(4,3),
    model_version VARCHAR(20),
    rationale TEXT
);

-- Trade history
CREATE TABLE trades (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    strategy_id UUID,
    instrument VARCHAR(20),
    entry_time TIMESTAMP,
    entry_price DECIMAL(18,8),
    exit_time TIMESTAMP,
    exit_price DECIMAL(18,8),
    quantity INTEGER,
    side VARCHAR(10),
    gross_pnl DECIMAL(18,2),
    commission DECIMAL(18,2),
    net_pnl DECIMAL(18,2),
    mae DECIMAL(18,8),
    mfe DECIMAL(18,8),
    exit_reason VARCHAR(50)
);
```
