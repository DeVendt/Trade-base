# AI Decision Framework

## Overview

The AI Decision Framework is the core intelligence of the trading system. It uses machine learning models to make trading decisions with confidence scoring, allowing the system to abstain from trading when uncertainty is high.

## Decision Types

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        AI DECISION OUTPUTS                                   │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌─────────────┐   ┌─────────────┐   ┌─────────────┐   ┌─────────────┐     │
│  │    BUY      │   │    SELL     │   │    HOLD     │   │   SCALE     │     │
│  │   (Long)    │   │   (Short)   │   │  (No Action)│   │  Position   │     │
│  │             │   │             │   │             │   │             │     │
│  │ + Confidence│   │ + Confidence│   │ + Reason    │   │ + Direction │     │
│  │ + Size      │   │ + Size      │   │             │   │ + Amount    │     │
│  └─────────────┘   └─────────────┘   └─────────────┘   └─────────────┘     │
│                                                                              │
│  Confidence Thresholds:                                                      │
│  ├── HIGH (≥0.8): Execute immediately                                       │
│  ├── MEDIUM (0.6-0.8): Execute with confirmation                            │
│  └── LOW (<0.6): Do not trade / Reduce position                             │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Model Architecture

### Multi-Model Ensemble Approach

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         ENSEMBLE MODEL                                       │
│                                                                              │
│   ┌───────────────┐                                                         │
│   │  Market Data  │                                                         │
│   │   Features    │                                                         │
│   └───────┬───────┘                                                         │
│           │                                                                 │
│           ▼                                                                 │
│   ┌─────────────────────────────────────────────────────────────────┐      │
│   │                     FEATURE ENGINEERING                          │      │
│   │  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐ ┌───────────┐  │      │
│   │  │   Price     │ │  Technical  │ │   Volume    │ │  Time     │  │      │
│   │  │  Features   │ │  Indicators │ │  Features   │ │ Features  │  │      │
│   │  └─────────────┘ └─────────────┘ └─────────────┘ └───────────┘  │      │
│   └─────────────────────────────────────────────────────────────────┘      │
│           │                                                                 │
│           ▼                                                                 │
│   ┌─────────────────────────────────────────────────────────────────┐      │
│   │                      MODEL ENSEMBLE                              │      │
│   │                                                                  │      │
│   │   ┌─────────────┐  ┌─────────────┐  ┌─────────────┐             │      │
│   │   │   Trend     │  │   Pattern   │  │  Volatility │             │      │
│   │   │   Model     │  │   Model     │  │    Model    │             │      │
│   │   │  (LSTM)     │  │  (CNN)      │  │   (XGBoost) │             │      │
│   │   └──────┬──────┘  └──────┬──────┘  └──────┬──────┘             │      │
│   │          │                │                │                     │      │
│   │          └────────────────┼────────────────┘                     │      │
│   │                           ▼                                      │      │
│   │                   ┌───────────────┐                              │      │
│   │                   │ Meta-Learner  │                              │      │
│   │                   │ (Neural Net)  │                              │      │
│   │                   └───────┬───────┘                              │      │
│   │                           │                                      │      │
│   │                           ▼                                      │      │
│   │   ┌─────────────────────────────────────────┐                    │      │
│   │   │         DECISION & CONFIDENCE          │                    │      │
│   │   │  Action: BUY/SELL/HOLD/SCALE           │                    │      │
│   │   │  Confidence: 0.0 - 1.0                 │                    │      │
│   │   │  Expected Return: $$$                  │                    │      │
│   │   │  Risk Score: 0.0 - 1.0                 │                    │      │
│   │   └─────────────────────────────────────────┘                    │      │
│   └─────────────────────────────────────────────────────────────────┘      │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Specialized Models

#### 1. Trend Prediction Model (LSTM)
- **Purpose**: Predict price direction over next N bars
- **Input**: OHLCV sequences
- **Output**: Probability of up/down/sideways movement
- **Architecture**: Stacked LSTM with attention

#### 2. Pattern Recognition Model (CNN)
- **Purpose**: Identify chart patterns
- **Input**: Normalized price images/chart data
- **Output**: Pattern classifications (head & shoulders, triangles, etc.)
- **Architecture**: CNN with residual connections

#### 3. Volatility Prediction Model (XGBoost/LightGBM)
- **Purpose**: Predict market volatility
- **Input**: Technical indicators, historical volatility
- **Output**: Expected volatility range
- **Architecture**: Gradient boosting ensemble

#### 4. Market Regime Detector (HMM/Clustering)
- **Purpose**: Classify current market condition
- **Output**: Trending up, trending down, ranging, volatile, choppy

### Meta-Learner

Combines outputs from specialized models:
```
Inputs:
- Trend model output (probabilities)
- Pattern model output (pattern presence scores)
- Volatility model output (expected range)
- Market regime (current condition)
- Recent performance (model accuracy tracking)

Output:
- Final decision (BUY/SELL/HOLD/SCALE)
- Confidence score (0.0 - 1.0)
- Recommended position size (% of account)
- Expected holding time
```

## Feature Engineering

### Price Features
```csharp
public class PriceFeatures
{
    // Returns
    public double Returns1 { get; set; }      // 1-bar return
    public double Returns5 { get; set; }      // 5-bar return
    public double Returns10 { get; set; }     // 10-bar return
    public double LogReturns { get; set; }    // Log returns
    
    // Price position
    public double PositionInRange { get; set; }  // Position within day's range
    public double DistanceFromVWAP { get; set; } // Distance from VWAP
    public double DistanceFromOpen { get; set; } // Distance from open
}
```

### Technical Indicators
```csharp
public class TechnicalFeatures
{
    // Trend indicators
    public double SMA20 { get; set; }
    public double SMA50 { get; set; }
    public double EMA12 { get; set; }
    public double EMA26 { get; set; }
    public double MACD { get; set; }
    public double MACDSignal { get; set; }
    
    // Momentum
    public double RSI14 { get; set; }
    public double StochasticK { get; set; }
    public double StochasticD { get; set; }
    
    // Volatility
    public double ATR14 { get; set; }
    public double BollingerUpper { get; set; }
    public double BollingerLower { get; set; }
    public double BollingerWidth { get; set; }
}
```

### Volume Features
```csharp
public class VolumeFeatures
{
    public double RelativeVolume { get; set; }    // Volume vs average
    public double VolumeTrend { get; set; }       // Increasing/decreasing
    public double VWAP { get; set; }              // Volume-weighted price
    public double OBV { get; set; }               // On-balance volume
    public double MoneyFlow { get; set; }         // Money flow index
}
```

## Confidence Scoring

### Confidence Calculation

```
Confidence = BaseConfidence × ModelAgreement × MarketConditionFactor × RecentPerformance

Where:
- BaseConfidence: Meta-learner raw output
- ModelAgreement: How much models agree (std dev of predictions)
- MarketConditionFactor: Lower confidence in choppy/volatile markets
- RecentPerformance: Model accuracy on recent similar trades
```

### Decision Matrix

| Confidence | Action | Position Size | Notes |
|------------|--------|---------------|-------|
| ≥ 0.90 | Execute | 100% | High conviction trade |
| 0.80 - 0.89 | Execute | 75% | Good opportunity |
| 0.70 - 0.79 | Execute | 50% | Moderate confidence |
| 0.60 - 0.69 | Execute | 25% | Low confidence, reduce size |
| < 0.60 | HOLD | 0% | Do not trade |

## Model Training Pipeline

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                      MODEL TRAINING PIPELINE                                 │
│                                                                              │
│  1. DATA COLLECTION                                                          │
│     └── Historical data from NinjaTrader                                     │
│         ├── OHLCV bars (1-min, 5-min, 15-min, 1-hour)                       │
│         ├── Tick data                                                        │
│         └── Order book (if available)                                        │
│                                                                              │
│  2. FEATURE ENGINEERING                                                      │
│     └── Calculate all features for each timestamp                            │
│         ├── Technical indicators                                             │
│         ├── Price patterns                                                   │
│         └── Volume analysis                                                  │
│                                                                              │
│  3. LABEL GENERATION                                                         │
│     └── Define profitable trade opportunities                                │
│         ├── Future returns (1, 5, 10 bars ahead)                            │
│         ├── Optimal entry/exit points                                        │
│         └── Risk-adjusted returns                                            │
│                                                                              │
│  4. MODEL TRAINING                                                           │
│     └── Train specialized models                                             │
│         ├── Hyperparameter tuning                                            │
│         ├── Cross-validation (walk-forward)                                  │
│         └── Ensemble training                                                │
│                                                                              │
│  5. VALIDATION                                                               │
│     └── Out-of-sample testing                                                │
│         ├── Paper trading                                                    │
│         ├── Different market conditions                                      │
│         └── Performance metrics                                              │
│                                                                              │
│  6. DEPLOYMENT                                                               │
│     └── Export to ONNX format                                                │
│         └── Version control for models                                       │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Model Monitoring

### Performance Metrics
- Prediction accuracy
- Precision/Recall per class
- Sharpe ratio of model decisions
- Maximum drawdown
- Calibration (confidence vs actual accuracy)

### Drift Detection
- Input feature drift
- Concept drift (market regime changes)
- Performance degradation alerts

### Model Versioning
```
/models
  /v1.0.0
    - trend_model.onnx
    - pattern_model.onnx
    - volatility_model.onnx
    - meta_learner.onnx
    - config.json
  /v1.1.0
    ...
```

## Decision Logging

Every AI decision is logged with:
```csharp
public class AIDecisionLog
{
    public DateTime Timestamp { get; set; }
    public string Instrument { get; set; }
    public string Decision { get; set; }  // BUY/SELL/HOLD/SCALE
    public double Confidence { get; set; }
    public double[] ModelOutputs { get; set; }
    public FeatureVector Features { get; set; }
    public MarketRegime Regime { get; set; }
    public double RecommendedSize { get; set; }
    public string Reason { get; set; }
}
```
