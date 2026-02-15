# Improvement System

This directory contains documentation for the Continuous Improvement Engine - an automated system that continuously analyzes trading performance and optimizes strategies.

## Documentation Index

| Document | Description |
|----------|-------------|
| [01-analytics-schema.md](./01-analytics-schema.md) | Database schema for tracking trades, models, and improvements |
| [02-continuous-improvement-engine.md](./02-continuous-improvement-engine.md) | Full documentation of the 4-step improvement process |

## Quick Start

### 1. Setup Environment

```bash
# Set required environment variables
export DATABASE_URL="postgresql://user:pass@localhost/tradebase"
export DISCORD_WEBHOOK_URL="https://discord.com/api/webhooks/..."

# Install dependencies
pip install -r requirements.txt
```

### 2. Run Single Improvement Cycle

```bash
python scripts/automation/run_improvement_cycle.py
```

### 3. Setup Recurring Schedule

```bash
python scripts/automation/schedule_optimizations.py
```

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Continuous Improvement Engine              │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│   ┌──────────┐    ┌──────────┐    ┌──────────┐    ┌────────┐│
│   │ ANALYZE  │───→│ IDENTIFY │───→│ OPTIMIZE │───→│ DEPLOY ││
│   └──────────┘    └──────────┘    └──────────┘    └────────┘│
│        │               │               │              │      │
│        ▼               ▼               ▼              ▼      │
│   Trade Stats    Recommendations   Backtests      A/B Test   │
│   Performance    Opportunities     Optimized      Production │
│   Model Acc      Priorities        Parameters     Rollback   │
│                                                              │
├─────────────────────────────────────────────────────────────┤
│                      Discord Notifications                    │
│              Real-time alerts for all events                  │
└─────────────────────────────────────────────────────────────┘
```

## The 4 Steps

### Step 1: ANALYZE
- Gather trade outcomes from the last 7-30 days
- Calculate performance metrics (win rate, Sharpe, drawdown)
- Evaluate model prediction accuracy
- Analyze performance by market regime

### Step 2: IDENTIFY
- Compare metrics against thresholds
- Detect declining trends
- Identify underperforming periods
- Generate prioritized recommendations

### Step 3: OPTIMIZE
- Hyperparameter tuning using Bayesian optimization
- Model retraining with recent data
- Feature selection based on importance
- Risk parameter adjustment

### Step 4: DEPLOY
- Deploy to staging environment
- Run smoke tests
- A/B test with 10% → 50% → 100% traffic
- Automatic rollback on degradation

## Key Features

- **Automated Scheduling:** Hourly, daily, and weekly optimization tasks
- **A/B Testing:** Safe deployment with gradual rollout
- **Automatic Rollback:** Reverts changes if performance degrades
- **Discord Integration:** Real-time notifications for all events
- **Performance Tracking:** Complete audit trail of all improvements
- **Circuit Breakers:** Disables failing tasks automatically

## Monitoring

### View Recent Improvements

```sql
SELECT 
    event_timestamp,
    event_type,
    component_id,
    trigger_reason,
    improvement_metrics
FROM improvement_events 
WHERE event_timestamp > NOW() - INTERVAL '7 days'
ORDER BY event_timestamp DESC;
```

### View Pending Optimizations

```sql
SELECT 
    task_type,
    component_id,
    frequency,
    next_run_at,
    status
FROM optimization_queue 
WHERE enabled = TRUE
ORDER BY next_run_at;
```

### View A/B Test Status

```sql
SELECT 
    test_name,
    control_version,
    treatment_version,
    status,
    improvement_percent
FROM ab_tests 
WHERE status = 'running';
```

## Configuration

### Task Scheduling

Tasks are configured in `scripts/automation/schedule_optimizations.py`:

```python
OptimizationTask(
    task_id="hyperparam_001",
    task_type=OptimizationType.HYPERPARAMETER,
    component_id="main_strategy",
    frequency="daily",  # hourly, daily, weekly
    priority=3,  # 1 = highest
    config={"params": ["stop_loss", "take_profit"]}
)
```

### Alert Thresholds

| Metric | Target | Warning | Critical |
|--------|--------|---------|----------|
| Win Rate | > 55% | < 50% | < 45% |
| Sharpe Ratio | > 1.5 | < 1.0 | < 0.5 |
| Max Drawdown | < 10% | > 10% | > 15% |
| Model Accuracy | > 60% | < 60% | < 55% |

## GitHub Actions Integration

The system runs automatically via GitHub Actions:

- **Schedule:** Every hour (`0 * * * *`)
- **Manual Trigger:** Via workflow_dispatch
- **Artifacts:** All results uploaded for review
- **Notifications:** Discord alerts for each step

See `.github/workflows/continuous-improvement.yml`

## Safety Measures

1. **Staging First:** All changes deploy to staging first
2. **Smoke Tests:** Automated tests before A/B test
3. **Gradual Rollout:** 10% → 50% → 100%
4. **Auto-Rollback:** On metric degradation
5. **Circuit Breakers:** 5 failures = auto-disable

## Troubleshooting

### Task Failures

Check logs:
```bash
tail -f improvement_cycle_*.log
```

Common issues:
- Database connection errors
- Insufficient training data
- Optimization timeout

### Manual Rollback

```python
from improvement_engine import ContinuousImprovementEngine

engine = ContinuousImprovementEngine(db_connection)
await engine.rollback(deployment_id="deploy_123")
```

## Support

For issues or questions:
- Check logs: `improvement_cycle_*.log`
- Review Discord notifications
- Query `improvement_events` table
- Open issue in GitHub repository
