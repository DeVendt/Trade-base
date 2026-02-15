# Analytics Database Schema

## Overview

This schema supports the Continuous Improvement Engine by storing all trade outcomes, model predictions, market conditions, and performance metrics for automated analysis and retraining.

---

## Schema Design

### 1. Trade Outcomes Table

```sql
-- Core trade execution data with outcomes
CREATE TABLE trade_outcomes (
    id BIGSERIAL PRIMARY KEY,
    trade_id UUID NOT NULL UNIQUE,
    
    -- Execution Details
    strategy_id VARCHAR(50) NOT NULL,
    model_version VARCHAR(20) NOT NULL,
    symbol VARCHAR(10) NOT NULL,
    direction VARCHAR(4) NOT NULL CHECK (direction IN ('LONG', 'SHORT')),
    entry_time TIMESTAMPTZ NOT NULL,
    exit_time TIMESTAMPTZ,
    
    -- Price Data
    entry_price DECIMAL(18, 8) NOT NULL,
    exit_price DECIMAL(18, 8),
    quantity INTEGER NOT NULL,
    
    -- Performance
    gross_pnl DECIMAL(18, 4),
    net_pnl DECIMAL(18, 4),  -- After commissions/slippage
    commission DECIMAL(18, 4) DEFAULT 0,
    slippage DECIMAL(18, 8) DEFAULT 0,
    
    -- Trade Characteristics
    duration_seconds INTEGER,
    max_favorable_excursion DECIMAL(18, 4),  -- Max profit during trade
    max_adverse_excursion DECIMAL(18, 4),     -- Max loss during trade
    
    -- Classification
    outcome_type VARCHAR(20) CHECK (outcome_type IN ('WIN', 'LOSS', 'BREAKEVEN', 'OPEN')),
    
    -- Metadata
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_trade_outcomes_strategy ON trade_outcomes(strategy_id, entry_time DESC);
CREATE INDEX idx_trade_outcomes_model ON trade_outcomes(model_version, entry_time DESC);
CREATE INDEX idx_trade_outcomes_symbol ON trade_outcomes(symbol, entry_time DESC);
CREATE INDEX idx_trade_outcomes_time ON trade_outcomes(entry_time DESC);
```

### 2. Model Predictions Table

```sql
-- Every prediction made by models for audit and improvement
CREATE TABLE model_predictions (
    id BIGSERIAL PRIMARY KEY,
    prediction_id UUID NOT NULL UNIQUE,
    
    -- Model Context
    model_version VARCHAR(20) NOT NULL,
    model_type VARCHAR(30) NOT NULL,  -- 'direction', 'volatility', 'regime'
    
    -- Prediction Details
    symbol VARCHAR(10) NOT NULL,
    predicted_at TIMESTAMPTZ NOT NULL,
    prediction_horizon_minutes INTEGER NOT NULL,  -- 5, 15, 60, etc.
    
    -- Prediction Values
    predicted_direction VARCHAR(4),  -- LONG/SHORT/NEUTRAL
    confidence_score DECIMAL(5, 4),  -- 0.0000 to 1.0000
    predicted_volatility DECIMAL(10, 6),
    
    -- Feature Snapshot (stored as JSONB for flexibility)
    feature_vector JSONB NOT NULL,
    feature_hash VARCHAR(64),  -- For deduplication
    
    -- Actual Outcome (filled later)
    actual_direction VARCHAR(4),
    actual_return DECIMAL(18, 8),
    prediction_accuracy BOOLEAN,  -- Did prediction match reality?
    
    -- Validation
    validated_at TIMESTAMPTZ,
    
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_model_predictions_model ON model_predictions(model_version, predicted_at DESC);
CREATE INDEX idx_model_predictions_symbol ON model_predictions(symbol, predicted_at DESC);
CREATE INDEX idx_model_predictions_unvalidated ON model_predictions(validated_at) WHERE validated_at IS NULL;
```

### 3. Market Conditions Table

```sql
-- Market regime and conditions at trade time
CREATE TABLE market_conditions (
    id BIGSERIAL PRIMARY KEY,
    
    symbol VARCHAR(10) NOT NULL,
    timestamp TIMESTAMPTZ NOT NULL,
    
    -- Price Action
    open DECIMAL(18, 8) NOT NULL,
    high DECIMAL(18, 8) NOT NULL,
    low DECIMAL(18, 8) NOT NULL,
    close DECIMAL(18, 8) NOT NULL,
    volume BIGINT NOT NULL,
    
    -- Technical Indicators
    atr_14 DECIMAL(18, 8),
    rsi_14 DECIMAL(5, 2),
    ema_20 DECIMAL(18, 8),
    ema_50 DECIMAL(18, 8),
    bb_upper DECIMAL(18, 8),
    bb_lower DECIMAL(18, 8),
    macd_line DECIMAL(18, 8),
    macd_signal DECIMAL(18, 8),
    
    -- Volatility
    volatility_20d DECIMAL(10, 6),
    vix DECIMAL(8, 4),
    
    -- Market Regime (detected by model)
    detected_regime VARCHAR(20),  -- 'trending_up', 'trending_down', 'ranging', 'volatile'
    regime_confidence DECIMAL(5, 4),
    
    -- Correlation Context
    spy_correlation DECIMAL(4, 3),
    sector_correlation DECIMAL(4, 3),
    
    created_at TIMESTAMPTZ DEFAULT NOW(),
    
    UNIQUE(symbol, timestamp)
);

-- Convert to TimescaleDB hypertable
SELECT create_hypertable('market_conditions', 'timestamp', chunk_time_interval => INTERVAL '1 day');

CREATE INDEX idx_market_conditions_regime ON market_conditions(detected_regime, timestamp DESC);
```

### 4. Strategy Performance Metrics Table

```sql
-- Aggregated performance metrics by time period
CREATE TABLE strategy_performance (
    id BIGSERIAL PRIMARY KEY,
    
    strategy_id VARCHAR(50) NOT NULL,
    model_version VARCHAR(20) NOT NULL,
    period_type VARCHAR(10) NOT NULL CHECK (period_type IN ('DAY', 'WEEK', 'MONTH')),
    period_start DATE NOT NULL,
    
    -- Trade Counts
    total_trades INTEGER DEFAULT 0,
    winning_trades INTEGER DEFAULT 0,
    losing_trades INTEGER DEFAULT 0,
    breakeven_trades INTEGER DEFAULT 0,
    
    -- Financial Metrics
    gross_profit DECIMAL(18, 4) DEFAULT 0,
    gross_loss DECIMAL(18, 4) DEFAULT 0,
    net_pnl DECIMAL(18, 4) DEFAULT 0,
    total_commission DECIMAL(18, 4) DEFAULT 0,
    
    -- Performance Ratios
    win_rate DECIMAL(5, 4),
    profit_factor DECIMAL(8, 4),
    avg_win DECIMAL(18, 4),
    avg_loss DECIMAL(18, 4),
    avg_trade DECIMAL(18, 4),
    largest_win DECIMAL(18, 4),
    largest_loss DECIMAL(18, 4),
    
    -- Risk Metrics
    max_drawdown DECIMAL(8, 4),
    max_drawdown_duration_days INTEGER,
    sharpe_ratio DECIMAL(8, 4),
    sortino_ratio DECIMAL(8, 4),
    calmar_ratio DECIMAL(8, 4),
    
    -- Execution Quality
    avg_slippage DECIMAL(18, 8),
    avg_fill_time_ms INTEGER,
    
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    
    UNIQUE(strategy_id, model_version, period_type, period_start)
);

CREATE INDEX idx_strategy_performance_strategy ON strategy_performance(strategy_id, period_start DESC);
CREATE INDEX idx_strategy_performance_model ON strategy_performance(model_version, period_start DESC);
```

### 5. Model Performance Metrics Table

```sql
-- ML model performance tracking
CREATE TABLE model_performance (
    id BIGSERIAL PRIMARY KEY,
    
    model_version VARCHAR(20) NOT NULL,
    model_type VARCHAR(30) NOT NULL,
    evaluation_date DATE NOT NULL,
    
    -- Dataset Info
    training_samples INTEGER,
    testing_samples INTEGER,
    training_period_start DATE,
    training_period_end DATE,
    
    -- Classification Metrics
    accuracy DECIMAL(5, 4),
    precision DECIMAL(5, 4),
    recall DECIMAL(5, 4),
    f1_score DECIMAL(5, 4),
    auc_roc DECIMAL(5, 4),
    
    -- Calibration
    brier_score DECIMAL(6, 5),
    expected_calibration_error DECIMAL(6, 5),
    
    -- By Regime Performance
    trending_up_accuracy DECIMAL(5, 4),
    trending_down_accuracy DECIMAL(5, 4),
    ranging_accuracy DECIMAL(5, 4),
    volatile_accuracy DECIMAL(5, 4),
    
    -- Feature Importance (stored as JSONB)
    feature_importance JSONB,
    
    -- Production Performance (backfilled)
    live_accuracy DECIMAL(5, 4),
    live_pnl_contribution DECIMAL(18, 4),
    
    -- Status
    status VARCHAR(20) DEFAULT 'evaluating' CHECK (status IN ('evaluating', 'staging', 'production', 'retired')),
    
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_model_performance_version ON model_performance(model_version, evaluation_date DESC);
CREATE INDEX idx_model_performance_status ON model_performance(status);
```

### 6. Improvement Events Table

```sql
-- Audit log of all system improvements
CREATE TABLE improvement_events (
    id BIGSERIAL PRIMARY KEY,
    
    event_type VARCHAR(30) NOT NULL,  -- 'model_deployed', 'params_optimized', 'strategy_disabled'
    event_timestamp TIMESTAMPTZ DEFAULT NOW(),
    
    -- What changed
    component_type VARCHAR(30) NOT NULL,  -- 'model', 'strategy', 'risk_params', 'feature'
    component_id VARCHAR(50) NOT NULL,
    old_value JSONB,
    new_value JSONB,
    
    -- Why it changed
    trigger_reason VARCHAR(100),  -- 'performance_degradation', 'scheduled_retrain', 'manual_override'
    performance_before JSONB,
    performance_after JSONB,
    improvement_metrics JSONB,  -- Specific metrics that improved
    
    -- Approval
    automated BOOLEAN DEFAULT TRUE,
    approved_by VARCHAR(50),  -- NULL if automated
    approval_timestamp TIMESTAMPTZ,
    
    -- Rollback info
    can_rollback BOOLEAN DEFAULT TRUE,
    rollback_timestamp TIMESTAMPTZ,
    
    -- Notifications
    discord_notified BOOLEAN DEFAULT FALSE,
    notification_message_id VARCHAR(100),
    
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_improvement_events_type ON improvement_events(event_type, event_timestamp DESC);
CREATE INDEX idx_improvement_events_component ON improvement_events(component_type, component_id);
```

### 7. A/B Tests Table

```sql
-- Track active and historical A/B tests
CREATE TABLE ab_tests (
    id BIGSERIAL PRIMARY KEY,
    
    test_id UUID NOT NULL UNIQUE,
    test_name VARCHAR(100) NOT NULL,
    
    -- Test Configuration
    control_version VARCHAR(20) NOT NULL,
    treatment_version VARCHAR(20) NOT NULL,
    test_type VARCHAR(20) NOT NULL CHECK (test_type IN ('model', 'strategy', 'params')),
    
    -- Traffic Split
    control_traffic_percent INTEGER NOT NULL CHECK (control_traffic_percent BETWEEN 0 AND 100),
    treatment_traffic_percent INTEGER NOT NULL CHECK (treatment_traffic_percent BETWEEN 0 AND 100),
    
    -- Duration
    started_at TIMESTAMPTZ NOT NULL,
    ended_at TIMESTAMPTZ,
    scheduled_duration_hours INTEGER DEFAULT 48,
    
    -- Success Criteria
    success_metric VARCHAR(50) NOT NULL,  -- 'sharpe_ratio', 'win_rate', 'profit_factor'
    minimum_improvement_percent DECIMAL(5, 2) DEFAULT 5.0,
    
    -- Results
    control_metrics JSONB,
    treatment_metrics JSONB,
    improvement_percent DECIMAL(6, 2),
    
    -- Outcome
    status VARCHAR(20) DEFAULT 'running' CHECK (status IN ('running', 'promoted', 'rejected', 'rollback')),
    conclusion VARCHAR(200),
    
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_ab_tests_status ON ab_tests(status);
CREATE INDEX idx_ab_tests_running ON ab_tests(status, started_at) WHERE status = 'running';
```

### 8. Continuous Optimization Queue

```sql
-- Queue for recurring optimization tasks
CREATE TABLE optimization_queue (
    id BIGSERIAL PRIMARY KEY,
    
    task_type VARCHAR(30) NOT NULL,  -- 'hyperparameter', 'feature_selection', 'strategy_weights'
    component_id VARCHAR(50) NOT NULL,
    
    -- Schedule
    frequency VARCHAR(20) NOT NULL,  -- 'hourly', 'daily', 'weekly'
    last_run_at TIMESTAMPTZ,
    next_run_at TIMESTAMPTZ NOT NULL,
    
    -- Execution
    status VARCHAR(20) DEFAULT 'pending' CHECK (status IN ('pending', 'running', 'completed', 'failed')),
    priority INTEGER DEFAULT 5,  -- 1 = highest
    
    -- Results
    last_result JSONB,
    last_error TEXT,
    consecutive_failures INTEGER DEFAULT 0,
    
    -- Configuration
    config JSONB NOT NULL,  -- Task-specific parameters
    
    enabled BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_optimization_queue_next_run ON optimization_queue(next_run_at) WHERE enabled = TRUE AND status = 'pending';
CREATE INDEX idx_optimization_queue_status ON optimization_queue(status);
```

---

## Entity Relationship Diagram

```
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────┐
│  trade_outcomes │────→│ market_conditions │←────│ model_predictions│
└────────┬────────┘     └──────────────────┘     └────────┬────────┘
         │                                               │
         ↓                                               ↓
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────┐
│strategy_performance│   │ improvement_events │   │ model_performance│
└─────────────────┘     └──────────────────┘     └─────────────────┘
         ↑                                               ↑
         │                                               │
         └─────────────────┐     ┌───────────────────────┘
                           ↓     ↓
                    ┌─────────────────┐
                    │    ab_tests     │
                    └─────────────────┘
                           ↑
                           │
                    ┌─────────────────┐
                    │optimization_queue│
                    └─────────────────┘
```

---

## Partitioning Strategy

### TimescaleDB Hypertables

| Table | Chunk Interval | Retention |
|-------|---------------|-----------|
| `market_conditions` | 1 day | 2 years |
| `trade_outcomes` | 1 week | 5 years |
| `model_predictions` | 1 week | 3 years |

### Archival Strategy

```sql
-- Move old data to cold storage after retention period
-- Trade outcomes older than 5 years → Parquet files in S3
-- Market conditions older than 2 years → Aggregated daily summaries only
```

---

## Migration Script

```sql
-- Run this to create all tables
\i 01-create-tables.sql

-- Setup TimescaleDB
\i 02-setup-timescale.sql

-- Create indexes
\i 03-create-indexes.sql

-- Setup retention policies
\i 04-retention-policies.sql
```

---

## Next Steps

1. Execute migration scripts in PostgreSQL
2. Configure TimescaleDB compression
3. Set up automated backup to S3
4. Configure read replicas for analytics queries
