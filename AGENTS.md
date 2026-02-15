---
# 🏢 Project Agents Configuration
# Trade Base - Headless Futures Trading System for NinjaTrader
---

# Project: Trade Base

## 📋 Project Context

### Overview
**Trade Base** is a fully automated, headless futures trading system that integrates directly with **NinjaTrader's .NET DLL**. The system executes AI-driven trading strategies on futures markets (ES, NQ, YM, etc.) completely autonomously - no manual intervention required.

### Key Characteristics
- **Headless**: No UI, runs as Windows Service or Docker container
- **Fully Automated**: Entry, exit, position sizing, and risk management handled automatically
- **Futures Focus**: Optimized for CME futures (ES, NQ, YM, CL, GC, etc.)
- **NinjaTrader Integration**: Direct DLL integration for order execution
- **AI-Driven**: ML models make all trading decisions
- **Hands-Free**: Set it and forget it - system manages everything

### Tech Stack
- **Runtime:** .NET 8+ (for NinjaTrader DLL integration)
- **Trading Platform:** NinjaTrader 8+ via .NET DLL
- **Markets:** CME Futures (ES, NQ, YM, CL, GC, ZB, etc.)
- **Data:** Real-time tick and bar data from NinjaTrader
- **AI/ML:** ML.NET / ONNX Runtime for inference
- **Database:** PostgreSQL + TimescaleDB for analytics
- **Cache:** Redis for state management
- **Message Queue:** RabbitMQ for event streaming
- **Monitoring:** Prometheus + Grafana
- **Notifications:** Discord webhooks

### Architecture Pattern
```
┌─────────────────────────────────────────────────────────────────┐
│                    HEADLESS FUTURES TRADER                       │
│                                                                  │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐       │
│  │   AI Brain   │───→│   Strategy   │───→│   Risk Mgr   │       │
│  │              │    │   Engine     │    │              │       │
│  │ • Predict    │    │              │    │ • Position   │       │
│  │ • Classify   │    │ • Entry/Exit │    │   sizing     │       │
│  │ • Regime     │    │ • Scale in   │    │ • Stops      │       │
│  │   detect     │    │ • Scale out  │    │ • Limits     │       │
│  └──────────────┘    └──────────────┘    └──────────────┘       │
│         │                   │                   │                │
│         └───────────────────┼───────────────────┘                │
│                             ▼                                    │
│                    ┌──────────────┐                              │
│                    │  NinjaTrader │                              │
│                    │  DLL Client  │                              │
│                    │              │                              │
│                    │ • Connect    │                              │
│                    │ • Subscribe  │                              │
│                    │ • Execute    │                              │
│                    └──────────────┘                              │
│                             │                                    │
│                             ▼                                    │
│                       ┌──────────┐                               │
│                       │  Broker  │                               │
│                       │ (CME)    │                               │
│                       └──────────┘                               │
└─────────────────────────────────────────────────────────────────┘
```

### Trading Specifications

#### Supported Futures
| Symbol | Name | Tick Size | Point Value | Session |
|--------|------|-----------|-------------|---------|
| ES | E-mini S&P 500 | $0.25 | $50 | 18:00-17:00 ET |
| NQ | E-mini NASDAQ-100 | $0.25 | $20 | 18:00-17:00 ET |
| YM | E-mini Dow | $1.00 | $5 | 18:00-17:00 ET |
| CL | Crude Oil | $0.01 | $1000 | 18:00-17:00 ET |
| GC | Gold | $0.10 | $100 | 18:00-17:00 ET |
| ZB | 30-Year T-Bond | $0.03125 | $1000 | 18:00-17:00 ET |

#### Order Types Supported
- Market orders (immediate execution)
- Limit orders (price improvement)
- Stop-market (loss protection)
- Stop-limit (precision exits)
- OCO (One-Cancels-Other brackets)
- Trailing stops (dynamic protection)

### Code Standards
- **Language:** C# 12 with .NET 8
- **Style Guide:** Microsoft C# Coding Conventions
- **Async:** All I/O operations must be async
- **Null Safety:** Enable nullable reference types
- **Testing:** xUnit with 80%+ coverage
- **Documentation:** XML docs for all public APIs
- **Language:** English (all code, docs, comments)

### Important Files/Directories
```
src/
  ├── Core/                    # Domain models, interfaces
  ├── NinjaTraderAdapter/      # NT DLL integration layer
  ├── Strategies/              # Trading strategies
  │   └── FullyAutomated/      # Main hands-free strategy
  ├── AI/                      # ML models and inference
  ├── RiskManagement/          # Position sizing, stops
  ├── Data/                    # Market data handling
  ├── ImprovementEngine/       # Self-optimization (C#)
  └── Notifications/           # Discord alerts
Tests/
  ├── Unit/                    # Unit tests
  ├── Integration/             # NT integration tests
  └── Backtests/               # Strategy backtests
docs/
  ├── architecture/            # System design
  ├── strategies/              # Strategy documentation
  └── deployment/              # Deployment guides
scripts/
  └── automation/              # CI/CD scripts
```

### Conventions
- **Branch naming:** feature/, bugfix/, hotfix/, strategy/
- **Commit messages:** Conventional Commits
- **Code review:** 2 approvals for strategy changes
- **Tests required:** Yes, including backtests
- **Documentation:** Update with every strategy change

### Environment Variables
```bash
# NinjaTrader Connection
NINJATRADER_HOST=localhost
NINJATRADER_PORT=3692
NINJATRADER_API_KEY=your_api_key
NINJATRADER_ACCOUNT=Sim101

# Trading Settings
TRADING_MODE=PAPER  # PAPER or LIVE
DEFAULT_SYMBOL=ES
MAX_POSITIONS=3
RISK_PER_TRADE_PCT=1.0

# Database
DATABASE_URL=postgresql://user:pass@localhost/tradebase
REDIS_URL=redis://localhost:6379/0

# Notifications
DISCORD_WEBHOOK_URL=https://discord.com/api/webhooks/...

# AI Models
MODEL_PATH=/models/
PREDICTION_THRESHOLD=0.65
```

### GitHub Configuration
- **Repository:** DeVendt/Trade-base
- **Default Branch:** main
- **Branch Protection:** Enabled (2 reviews for strategies)
- **CI/CD:** GitHub Actions (.NET build + test)

---

## 🤖 Swarm Configuration

### Team Assignments

| Team | Status | Specialization |
|------|--------|----------------|
| **Research** | Always active | Futures market analysis, regime detection |
| **Development** | Always active | C# strategies, NT integration |
| **Testing** | On demand | Backtests, paper trading validation |
| **Deployment** | On releases | Windows Service deployment |
| **Documentation** | Always active | Strategy docs, API docs |
| **GitHub** | On demand | Repo management |

### Custom Specialists

```yaml
specialists:
  futures-expert:
    description: "CME futures market microstructure and contract specs"
    trigger: "For futures-specific logic and rollover handling"
  
  ninjabuilder-expert:
    description: "NinjaTrader DLL integration and API usage"
    trigger: "For NT connection, order execution, data subscription"
  
  strategy-automation:
    description: "Fully automated strategy design and implementation"
    trigger: "For hands-free strategy development"
  
  market-regime-detector:
    description: "Trending vs ranging vs volatile detection"
    trigger: "For regime-specific strategy adjustments"
  
  position-manager:
    description: "Scale-in, scale-out, pyramiding logic"
    trigger: "For advanced position management"
```

### Automation Agents

```yaml
automation:
  trade-monitor:
    description: "Monitors open positions and P&L"
    interval: "every_5_seconds"
    
  strategy-optimizer:
    description: "Optimizes strategy parameters based on performance"
    schedule: "daily_at_midnight"
    
  rollover-detector:
    description: "Detects futures contract rollover dates"
    schedule: "daily"
    alert_channel: "discord"
```

---

## 🔄 Fully Automated Strategy Workflow

### Strategy Logic Flow
```
Market Data (Tick/Bar)
        │
        ▼
┌───────────────┐
│ Data Handler  │───→ Feature Engineering
└───────────────┘
        │
        ▼
┌───────────────┐
│  AI Models    │───→ Prediction + Confidence
│               │     (Direction, Volatility, Regime)
└───────────────┘
        │
        ▼
┌───────────────┐
│  Strategy     │───→ Entry/Exit/Scale Signals
│  Engine       │
└───────────────┘
        │
        ▼
┌───────────────┐
│  Risk Manager │───→ Position Size, Stops, Limits
│               │     (Rejects if risk exceeded)
└───────────────┘
        │
        ▼
┌───────────────┐
│  Execution    │───→ Submit to NinjaTrader
│  Engine       │     (OCO brackets, trailing stops)
└───────────────┘
        │
        ▼
┌───────────────┐
│  Monitoring   │───→ Discord alerts, logging
│  & Alerts     │     Performance tracking
└───────────────┘
```

### Decision Making
The strategy makes ALL decisions automatically:
- **When to enter**: AI prediction confidence > threshold
- **When to exit**: Target hit, stop hit, or AI exit signal
- **Position size**: Based on risk %, volatility, account size
- **Scale in/out**: Pyramid on winners, reduce on losers
- **Risk management**: Hard stops, daily loss limits, max positions

### Configuration Example
```csharp
public class FullyAutomatedStrategyConfig
{
    // Symbol settings
    public string Symbol { get; set; } = "ES";
    public string Account { get; set; } = "Sim101";
    
    // AI thresholds
    public double EntryConfidenceThreshold { get; set; } = 0.65;
    public double ExitConfidenceThreshold { get; set; } = 0.55;
    
    // Risk management
    public double RiskPerTradePercent { get; set; } = 1.0;  // 1% of account
    public double MaxDailyLossPercent { get; set; } = 3.0;  // 3% daily limit
    public int MaxConcurrentPositions { get; set; } = 3;
    
    // Position sizing
    public bool EnablePyramiding { get; set; } = true;
    public int MaxPyramidLevels { get; set; } = 3;
    public double PyramidThreshold { get; set; } = 1.0;  // ATR multiple
    
    // Exits
    public double StopLossATR { get; set; } = 1.5;
    public double TakeProfitATR { get; set; } = 3.0;
    public bool UseTrailingStop { get; set; } = true;
    public double TrailingStopATR { get; set; } = 1.0;
}
```

---

## 📊 Performance Targets (Futures)

| Metric | Target | Alert Threshold |
|--------|--------|-----------------|
| Win Rate | > 50% | < 45% |
| Profit Factor | > 1.5 | < 1.2 |
| Sharpe Ratio | > 1.2 | < 0.8 |
| Max Drawdown | < 10% | > 15% |
| Avg Win/Loss | > 1.5:1 | < 1.2:1 |
| Expectancy | > $100/trade | < $0 |
| Latency | < 50ms | > 100ms |

---

## 🚀 Deployment Modes

### 1. Paper Trading (Recommended First)
```bash
TRADING_MODE=PAPER
NINJATRADER_ACCOUNT=Sim101
```
- Uses NinjaTrader simulation account
- Real market data, fake money
- Validate strategy for 2-4 weeks minimum

### 2. Live Trading
```bash
TRADING_MODE=LIVE
NINJATRADER_ACCOUNT=LiveAccount
```
- Real money, real execution
- All risk limits strictly enforced
- Start with 1 contract only

### 3. Hybrid (Strategy-Specific)
- Some strategies in paper
- Others in live
- Useful for testing new strategies
