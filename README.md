# Trade Base - Headless Futures Trading System

[![.NET](https://img.shields.io/badge/.NET-8.0-blue)](https://dotnet.microsoft.com/)
[![NinjaTrader](https://img.shields.io/badge/NinjaTrader-8+-green)](https://ninjatrader.com/)
[![License](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

> **Fully automated, headless futures trading on CME markets via NinjaTrader**

Trade Base is a production-grade, AI-driven trading system designed for **hands-free operation** on futures markets. It integrates directly with NinjaTrader's .NET DLL to execute trades autonomously - no UI, no manual intervention, just pure automation.

## 🎯 Key Features

- **🤖 Fully Automated**: Entry, exit, position sizing, and risk management handled by AI
- **📈 Futures Focused**: Optimized for CME futures (ES, NQ, YM, CL, GC, etc.)
- **⚡ Headless Operation**: Runs as Windows Service or console app - no UI needed
- **🧠 AI-Driven Decisions**: ML models predict direction, volatility, and market regime
- **🛡️ Built-in Risk Management**: Automatic stops, position sizing, and circuit breakers
- **📊 Self-Improving**: Continuous optimization engine improves performance over time
- **🔔 Discord Integration**: Real-time alerts for all trading activity

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    TRADE BASE - HEADLESS TRADER                  │
│                                                                  │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐       │
│  │  AI Models   │───→│   Strategy   │───→│   Risk Mgr   │       │
│  │              │    │   Engine     │    │              │       │
│  │ • Direction  │    │              │    │ • Position   │       │
│  │ • Volatility │    │ • Entries    │    │   sizing     │       │
│  │ • Regime     │    │ • Exits      │    │ • Stops      │       │
│  └──────────────┘    │ • Scaling    │    │ • Limits     │       │
│         │            └──────────────┘    └──────────────┘       │
│         │                   │                   │                │
│         │                   └───────────────────┘                │
│         │                           │                            │
│         ▼                           ▼                            │
│  ┌──────────────────────────────────────────────────┐           │
│  │         CONTINUOUS IMPROVEMENT ENGINE            │           │
│  │  ANALYZE → IDENTIFY → OPTIMIZE → DEPLOY          │           │
│  │  Self-optimizes strategy parameters hourly       │           │
│  └──────────────────────────────────────────────────┘           │
│                             │                                    │
│                             ▼                                    │
│                    ┌──────────────┐                              │
│                    │  NinjaTrader │                              │
│                    │  DLL Client  │                              │
│                    └──────────────┘                              │
│                             │                                    │
│                             ▼                                    │
│                       ┌──────────┐                               │
│                       │  Broker  │                               │
│                       │  (CME)   │                               │
│                       └──────────┘                               │
└─────────────────────────────────────────────────────────────────┘
```

## 🚀 Quick Start

### Prerequisites
- Windows 10/11 or Windows Server
- NinjaTrader 8+ installed
- .NET 8.0 SDK
- PostgreSQL 15+ (optional, for analytics)

### 1. Clone and Build

```bash
git clone https://github.com/DeVendt/Trade-base.git
cd Trade-base

dotnet build -c Release
```

### 2. Configure

Copy `appsettings.json` and configure:

```json
{
  "NinjaTrader": {
    "Host": "localhost",
    "Port": 3692,
    "ApiKey": "your_nt_api_key",
    "Account": "Sim101"
  },
  "Trading": {
    "Mode": "PAPER",
    "Symbol": "ES",
    "RiskPerTrade": 1.0
  },
  "Discord": {
    "WebhookUrl": "https://discord.com/api/webhooks/..."
  }
}
```

### 3. Run in Paper Trading

```bash
# Run as console
dotnet run --project src/TradeBase -- --mode headless --symbol ES --paper

# Or install as Windows Service
sc create TradeBase binPath= "C:\TradeBase\TradeBase.exe --mode service"
sc start TradeBase
```

### 4. Monitor

Check Discord for real-time updates:
- Entry/exit notifications
- P&L updates
- Risk alerts
- Daily summaries

## 📊 Supported Futures

| Symbol | Market | Tick | Point Value | Session |
|--------|--------|------|-------------|---------|
| ES | E-mini S&P 500 | $0.25 | $50 | 18:00-17:00 ET |
| NQ | E-mini NASDAQ-100 | $0.25 | $20 | 18:00-17:00 ET |
| YM | E-mini Dow | $1.00 | $5 | 18:00-17:00 ET |
| CL | Crude Oil | $0.01 | $1000 | 18:00-17:00 ET |
| GC | Gold | $0.10 | $100 | 18:00-17:00 ET |
| ZB | 30-Year T-Bond | $0.03125 | $1000 | 18:00-17:00 ET |

## 🧠 AI Decision Making

The system uses three ML models:

1. **Direction Predictor**: Predicts UP/DOWN/NEUTRAL with confidence score
2. **Volatility Predictor**: Estimates expected volatility (ATR)
3. **Regime Detector**: Identifies trending, ranging, or choppy markets

### Entry Criteria (Long Example)
- Direction prediction: UP with ≥ 65% confidence
- Volatility: Not extreme (< 4x normal)
- Regime: Trending up OR ranging (avoid choppy)
- Risk check: Position size ≤ 1% account

### Exit Strategies
- **Target**: 3x ATR profit target
- **Stop**: 1.5x ATR stop loss
- **AI Exit**: Confidence drops below 55%
- **Trailing Stop**: Activated after 1R profit
- **Scale Out**: 1/3 at each profit level

## 🛡️ Risk Management

### Per-Trade Limits
- Maximum 1% risk per trade
- Position sized by volatility
- Automatic OCO (bracket) orders

### Daily Circuit Breakers
- Max 3% daily loss → Trading halted
- 5 consecutive losses → Pause
- Win rate < 30% (last 20 trades) → Review
- Max drawdown > 10% → Alert

### Position Management
- Max 3 concurrent positions
- Automatic scale-in on confirmation
- Automatic scale-out at targets
- Move to breakeven after 1R

## 🔄 Continuous Improvement

The system automatically improves itself:

1. **ANALYZE**: Review trade outcomes every hour
2. **IDENTIFY**: Find underperforming parameters
3. **OPTIMIZE**: Run Bayesian optimization
4. **DEPLOY**: A/B test new parameters

All improvements are tracked and notified via Discord.

```bash
# Run improvement cycle manually
python scripts/automation/run_improvement_cycle.py

# Or let it run automatically via GitHub Actions
```

## 📁 Project Structure

```
Trade-base/
├── src/
│   ├── Core/                    # Domain models
│   ├── NinjaTraderAdapter/      # NT DLL integration
│   ├── Strategies/
│   │   └── FullyAutomated/      # Main hands-free strategy
│   ├── AI/                      # ML models
│   ├── RiskManagement/          # Position sizing, stops
│   ├── ImprovementEngine/       # Self-optimization
│   └── Notifications/           # Discord alerts
├── Tests/
│   ├── Unit/
│   ├── Integration/
│   └── Backtests/
├── docs/
│   ├── architecture/
│   └── strategies/
└── scripts/
    └── automation/
```

## 📖 Documentation

- [Fully Automated Strategy](docs/strategies/fully-automated-futures.md) - Complete strategy documentation
- [NinjaTrader Integration](docs/architecture/03-ninjatrader-integration.md) - DLL integration details
- [Continuous Improvement](docs/improvement-system/02-continuous-improvement-engine.md) - Self-optimization
- [Project Overview](docs/planning/01-project-overview.md) - Architecture and goals

## 🔧 Configuration Examples

### Conservative Settings (Low Risk)
```json
{
  "RiskPerTrade": 0.5,
  "MaxPositions": 2,
  "EntryConfidence": 0.70,
  "StopLossATR": 1.5,
  "TakeProfitATR": 2.5
}
```

### Moderate Settings (Balanced)
```json
{
  "RiskPerTrade": 1.0,
  "MaxPositions": 3,
  "EntryConfidence": 0.65,
  "StopLossATR": 1.5,
  "TakeProfitATR": 3.0
}
```

### Aggressive Settings (Higher Risk)
```json
{
  "RiskPerTrade": 2.0,
  "MaxPositions": 5,
  "EntryConfidence": 0.60,
  "StopLossATR": 1.0,
  "TakeProfitATR": 4.0
}
```

## 📈 Performance Targets

| Metric | Target |
|--------|--------|
| Win Rate | 50-55% |
| Profit Factor | > 1.5 |
| Sharpe Ratio | > 1.2 |
| Max Drawdown | < 10% |
| Expectancy | > $100/trade (ES) |

## 🧪 Testing

```bash
# Unit tests
dotnet test Tests/Unit

# Integration tests (requires NT connection)
dotnet test Tests/Integration

# Backtest
dotnet run -- --mode backtest --symbol ES --start 2024-01-01 --end 2024-12-31
```

## 🚀 Deployment

### Option 1: Windows Service (Recommended)
```powershell
# Install service
sc create TradeBase binPath= "C:\TradeBase\TradeBase.exe --mode service"
sc config TradeBase start= auto
sc start TradeBase

# View logs
Get-EventLog -LogName Application -Source TradeBase
```

### Option 2: Docker (Experimental)
```bash
docker build -t tradebase .
docker run -d --name tradebase tradebase
```

### Option 3: Console (Development)
```bash
dotnet run --project src/TradeBase -- --mode headless
```

## 🔔 Discord Notifications

The system sends alerts for:
- ✅ Trade entries and exits
- 📊 P&L updates
- ⚠️ Risk warnings
- 🛑 Circuit breaker triggers
- 📈 Daily performance summaries
- 🔄 Optimization updates

Setup:
1. Create Discord webhook
2. Add URL to `appsettings.json`
3. Restart service

## 🛠️ Troubleshooting

### Strategy not trading?
- Check AI model is loaded
- Verify confidence threshold
- Check risk limits not exceeded
- Review Discord for rejection reasons

### High latency?
- Run on same machine as NinjaTrader
- Check network connection
- Reduce logging verbosity

### Unexpected losses?
- Check recent market regime
- Review AI model accuracy
- Verify stop distances
- Check position sizing

## ⚠️ Risk Disclaimer

**Trading futures involves substantial risk of loss and is not suitable for all investors.** Past performance is not indicative of future results. The system can and will lose money. Always start with paper trading and never risk more than you can afford to lose.

## 🤝 Contributing

Contributions welcome! Please read [CONTRIBUTING.md](CONTRIBUTING.md) first.

## 📄 License

MIT License - see [LICENSE](LICENSE) file.

## 🙏 Acknowledgments

- NinjaTrader for the excellent .NET API
- CME Group for market data
- Contributors and testers

---

**Ready to trade?** Start with paper trading, validate thoroughly, then deploy with caution. The market is always right.
