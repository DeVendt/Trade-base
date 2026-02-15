# Continuous Improvement Engine

## Overview

The Continuous Improvement Engine is an automated system that continuously analyzes **futures trading performance** on CME markets, identifies optimization opportunities, and deploys improvements with safety measures - all while you sleep.

## The 4-Step Process

```
┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐
│  STEP 1  │───→│  STEP 2  │───→│  STEP 3  │───→│  STEP 4  │
│ ANALYZE  │    │ IDENTIFY │    │ OPTIMIZE │    │  DEPLOY  │
└──────────┘    └──────────┘    └──────────┘    └──────────┘
```

### Step 1: ANALYZE

Gathers comprehensive futures trading data:
- **Trade Outcomes**: Entry/exit prices, P&L, duration, slippage
- **Strategy Performance**: Win rate, Sharpe ratio, max drawdown per symbol
- **Market Regime Performance**: How ES, NQ, YM perform in trending vs ranging
- **Session Analysis**: RTH vs overnight performance
- **Model Accuracy**: Prediction accuracy per futures symbol

**Futures-Specific Metrics:**
- Tick efficiency (entries near intended price)
- Fill quality (slippage in ticks)
- Session-specific performance
- Contract rollover impact

**Output:** Performance snapshot with baseline metrics per symbol

### Step 2: IDENTIFY

Analyzes futures-specific metrics:
- Win rate below thresholds per symbol
- Drawdown exceeding limits (ES vs NQ vs YM)
- Poor performance in specific market regimes
- Session degradation (RTH performance dropping)
- Slippage increase (execution quality issues)

**Output:** Prioritized list of recommendations per futures symbol

### Step 3: OPTIMIZE

Executes optimizations for futures trading:
- **Hyperparameter Tuning**: Stop distances, profit targets, entry thresholds
- **Model Retraining**: Per-symbol models (ES model vs NQ model)
- **Session Optimization**: Different parameters for RTH vs overnight
- **Regime-Specific Params**: Different settings for trending vs ranging
- **Execution Optimization**: Order type selection (market vs limit)

**Output:** Optimized parameters with backtest validation on historical futures data

### Step 4: DEPLOY

Deploys improvements with futures-specific safety:
1. Deploy to staging (paper trading)
2. Run smoke tests on all symbols
3. A/B test on single symbol first (e.g., ES only)
4. Monitor for 1 hour during active session
5. Gradually roll out to NQ, YM, etc.
6. Full production deployment
7. Automatic rollback on any symbol degradation

**Output:** Deployed improvement with per-symbol monitoring

## Futures Market Considerations

### Symbol-Specific Optimization

Each futures symbol has unique characteristics:

```python
# Per-symbol optimization config
symbol_configs = {
    "ES": {
        "tick_size": 0.25,
        "tick_value": 12.50,
        "volatility_regime": "medium",
        "optimal_sessions": ["RTH", "overnight"],
        "confidence_threshold": 0.65
    },
    "NQ": {
        "tick_size": 0.25,
        "tick_value": 5.00,
        "volatility_regime": "high",
        "optimal_sessions": ["RTH"],  # Avoid overnight gaps
        "confidence_threshold": 0.70
    },
    "CL": {
        "tick_size": 0.01,
        "tick_value": 10.00,
        "volatility_regime": "high",
        "optimal_sessions": ["inventory_report_times"],
        "confidence_threshold": 0.75  # Higher bar for oil
    }
}
```

### Session-Specific Analysis

Futures trade nearly 24 hours. The engine analyzes:

| Session | ES Hours | Characteristics |
|---------|----------|-----------------|
| Asian | 18:00-20:00 ET | Lower volume, wider spreads |
| European | 03:00-09:30 ET | Moderate volume |
| RTH (US) | 09:30-16:00 ET | Highest volume, best liquidity |
| Close/Overnight | 16:00-18:00 ET | Position squaring |

### Contract Rollover Handling

The engine automatically detects and adapts to contract rollovers:

```python
async def check_contract_rollover(symbol: str):
    """Detect upcoming contract expiration and adjust."""
    current_contract = get_current_contract(symbol)
    days_to_expiry = (current_contract.expiry - datetime.now()).days
    
    if days_to_expiry <= 5:
        # Reduce position size
        await adjust_risk_parameters(symbol, size_multiplier=0.5)
        
        # Notify via Discord
        await discord.notify_rollover_warning(symbol, days_to_expiry)
    
    if days_to_expiry <= 2:
        # Pause new entries, only exits
        await pause_new_entries(symbol)
```

## Components

### 1. ContinuousImprovementEngine

Main orchestrator for futures optimization.

```python
from improvement_engine import ContinuousImprovementEngine, OptimizationTask, OptimizationType

engine = ContinuousImprovementEngine(db_connection, discord_notifier)

# Add per-symbol recurring tasks
for symbol in ["ES", "NQ", "YM"]:
    # Daily model retraining per symbol
    task = OptimizationTask(
        task_id=f"model_retrain_{symbol}",
        task_type=OptimizationType.MODEL_RETRAIN,
        component_id=f"{symbol}_predictor",
        frequency="daily",
        config={
            "symbol": symbol,
            "min_samples": 5000,
            "lookback_days": 60
        }
    )
    await engine.add_task(task)
    
    # Hourly hyperparameter optimization
    task = OptimizationTask(
        task_id=f"hyperparam_{symbol}",
        task_type=OptimizationType.HYPERPARAMETER,
        component_id=f"{symbol}_strategy",
        frequency="hourly",
        config={
            "symbol": symbol,
            "params": ["stop_loss_atr", "take_profit_atr", "confidence_threshold"]
        }
    )
    await engine.add_task(task)

# Start the engine
await engine.start()
```

### 2. OptimizationRunner

Executes the full 4-step cycle for futures.

```python
from improvement_engine import OptimizationRunner

runner = OptimizationRunner(db_connection, discord_notifier)

# Run full cycle for specific symbol
results = await runner.run_full_cycle(strategy_id="ES_fully_automated")

for result in results:
    print(f"{result.step}: {'Success' if result.success else 'Failed'}")
    if result.step == OptimizationStep.ANALYZE:
        print(f"  Win rate: {result.metrics_before.get('win_rate_7d'):.1%}")
        print(f"  Profit factor: {result.metrics_before.get('profit_factor_7d'):.2f}")
```

### 3. FuturesPerformanceAnalyzer

Analyzes futures-specific performance.

```python
from improvement_engine import PerformanceAnalyzer

analyzer = PerformanceAnalyzer(db_connection)

# Analyze ES performance
analysis = await analyzer.analyze_recent_performance(
    strategy_id="ES_fully_automated",
    days=30
)

# Check session-specific performance
for session in ["RTH", "overnight"]:
    session_perf = analysis['summary']['by_session'][session]
    print(f"{session}: {session_perf['win_rate']:.1%} win rate, "
          f"${session_perf['avg_pnl']:.2f} avg P&L")

# Check slippage
slippage = analysis['summary']['avg_slippage_ticks']
if slippage > 1:
    print(f"WARNING: High slippage ({slippage:.1f} ticks) - consider limit orders")
```

### 4. DiscordNotifier

Futures-specific notifications.

```python
from notifications import DiscordNotifier

discord = DiscordNotifier("https://discord.com/api/webhooks/...")

# Symbol-specific trade notification
await discord.notify_trade_executed({
    'symbol': 'ES',
    'direction': 'LONG',
    'entry_price': 4500.00,
    'exit_price': 4512.50,
    'contracts': 2,
    'net_pnl': 1250.00,
    'duration_minutes': 15,
    'slippage_ticks': 0.5,
    'session': 'RTH'
})

# Contract rollover warning
await discord.notify_system_alert(
    "📅 Contract Rollover Approaching",
    "ES March contract expires in 3 days. New entries paused.",
    severity="warning"
)
```

## Task Scheduling

### Default Futures Schedule

| Task | Frequency | Symbol | Priority | Description |
|------|-----------|--------|----------|-------------|
| Model retrain | Daily | ES, NQ, YM | 2 | Retrain per-symbol models |
| Hyperparameter opt | Hourly | ES, NQ, YM | 3 | Optimize stops/targets |
| Session analysis | Daily | All | 4 | RTH vs overnight performance |
| Slippage check | Every 4 hours | All | 5 | Execution quality |
| Rollover check | Daily | All | 1 | Contract expiration |
| Risk params | Weekly | All | 1 | Portfolio heat |

### Symbol-Specific Tasks

```python
# Different schedules for different symbols
symbols_config = {
    "ES": {
        "frequency": "hourly",  # Most liquid, optimize often
        "priority": 2
    },
    "NQ": {
        "frequency": "hourly",  # High volatility, monitor closely
        "priority": 2
    },
    "YM": {
        "frequency": "daily",   # Lower priority
        "priority": 4
    },
    "CL": {
        "frequency": "daily",   # Event-driven, less frequent
        "priority": 3
    }
}
```

## Automation Scripts

### Run Single Improvement Cycle

```bash
# Run for all symbols
python scripts/automation/run_improvement_cycle.py

# Run for specific symbol
python scripts/automation/run_improvement_cycle.py --symbol ES

# Run continuously (every hour)
python scripts/automation/run_improvement_cycle.py --continuous --interval 3600

# With Discord notifications
python scripts/automation/run_improvement_cycle.py --discord-webhook $DISCORD_WEBHOOK_URL
```

### Setup Recurring Schedule

```bash
# Setup default futures schedule
python scripts/automation/schedule_optimizations.py --config futures

# With notifications
python scripts/automation/schedule_optimizations.py --discord-webhook $DISCORD_WEBHOOK_URL
```

## Discord Notifications

### Futures-Specific Events

| Event | Severity | Details |
|-------|----------|---------|
| Trade executed | Info | Symbol, direction, P&L, session |
| Slippage alert | Warning | > 1 tick slippage |
| Rollover warning | Warning | 5 days before expiry |
| Session degradation | Warning | RTH performance drop |
| Contract switch | Info | Rolled to new contract |
| Daily summary | Info | Per-symbol P&L |

### Example Discord Message

```
🟢 TRADE WIN - ES
Direction: LONG 2 contracts @ 4500.00 → 4512.50
Session: RTH
P&L: +$1,250.00
Duration: 15m
Slippage: 0.5 ticks

Strategy: FullyAutomated
Confidence: 72%
Regime: Trending Up
```

## Configuration

### Environment Variables

```bash
# Database
export DATABASE_URL="postgresql://user:pass@localhost/tradebase"

# Discord
export DISCORD_WEBHOOK_URL="https://discord.com/api/webhooks/..."

# Trading
export TRADING_MODE="PAPER"  # or LIVE
export SYMBOLS="ES,NQ,YM"    # Comma-separated list

# NinjaTrader
export NINJATRADER_ACCOUNT="Sim101"
```

### Per-Symbol Configuration

```json
{
  "ImprovementEngine": {
    "Symbols": {
      "ES": {
        "enabled": true,
        "optimization_frequency": "hourly",
        "win_rate_threshold": 0.50,
        "max_drawdown_threshold": 0.10
      },
      "NQ": {
        "enabled": true,
        "optimization_frequency": "hourly",
        "win_rate_threshold": 0.48,
        "max_drawdown_threshold": 0.12
      },
      "CL": {
        "enabled": true,
        "optimization_frequency": "daily",
        "win_rate_threshold": 0.52,
        "max_drawdown_threshold": 0.08
      }
    }
  }
}
```

## Safety Measures

### Automatic Rollback

Monitors these futures-specific metrics:
- Win rate drop > 5% per symbol
- Slippage increase > 1 tick
- Drawdown increase > 3%
- Fill rate degradation

### A/B Testing for Futures

```
ES Symbol Test:
  Control: Current parameters
  Treatment: New optimized parameters
  Traffic: 50% / 50% (split by session)
  Duration: 2 hours minimum
  Success: Win rate >= control, Slippage <= control + 0.5 tick
```

### Circuit Breakers

- Task disabled after 5 consecutive failures
- Symbol paused if daily loss > 3%
- All trading halted if total portfolio drawdown > 10%

## Monitoring

### Key Futures Metrics

```sql
-- Win rate by symbol (last 7 days)
SELECT 
    symbol,
    COUNT(*) as total_trades,
    SUM(CASE WHEN net_pnl > 0 THEN 1 ELSE 0 END)::FLOAT / COUNT(*) as win_rate,
    AVG(net_pnl) as avg_pnl,
    AVG(slippage_ticks) as avg_slippage
FROM trade_outcomes 
WHERE entry_time > NOW() - INTERVAL '7 days'
GROUP BY symbol
ORDER BY win_rate DESC;

-- Performance by session
SELECT 
    symbol,
    CASE 
        WHEN EXTRACT(HOUR FROM entry_time) BETWEEN 9 AND 16 THEN 'RTH'
        ELSE 'Extended'
    END as session,
    AVG(net_pnl) as avg_pnl
FROM trade_outcomes
GROUP BY symbol, session;

-- Rollover impact
SELECT 
    contract_month,
    AVG(net_pnl) as avg_pnl,
    AVG(slippage_ticks) as avg_slippage
FROM trade_outcomes
WHERE entry_time > contract_rollover_date - INTERVAL '5 days'
GROUP BY contract_month;
```

## Best Practices

1. **Start Conservative**: Use paper trading for 2+ weeks per symbol
2. **Monitor Slippage**: Watch execution quality, adjust order types
3. **Respect Rollover**: Reduce size or pause near contract expiry
4. **Session Awareness**: Different strategies for RTH vs overnight
5. **Symbol Diversification**: Don't put all risk in one symbol

## Troubleshooting

### High Slippage on ES?
- Switch to limit orders for entries
- Avoid market open/close times
- Check NinjaTrader connection latency

### Poor Overnight Performance?
- Disable overnight trading for that symbol
- Increase confidence threshold after hours
- Reduce position size for extended sessions

### Rollover Issues?
- System auto-detects 5 days before expiry
- New entries paused 2 days before
- Manually roll positions if needed

## Future Enhancements

- [ ] Inter-market analysis (ES/NQ correlation)
- [ ] Options on futures integration
- [ ] Micro-contract support (MES, MNQ)
- [ ] News event detection and avoidance
- [ ] Order flow imbalance detection
