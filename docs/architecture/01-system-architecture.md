# System Architecture

## High-Level Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           MARKET DATA SOURCES                                │
│  (NinjaTrader Data Feed / Broker API / External Providers)                  │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                           DATA INGESTION LAYER                               │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐ │
│  │ Market Data │  │  Order Book │  │  Historical │  │   News/Sentiment    │ │
│  │   Stream    │  │   Processor │  │    Cache    │  │     Processor       │ │
│  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                           CORE TRADING ENGINE                                │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                     STRATEGY ORCHESTRATOR                            │   │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  │   │
│  │  │ Strategy A  │  │ Strategy B  │  │ Strategy C  │  │  Strategy N │  │   │
│  │  │ (Scalping)  │  │  (Trend)    │  │(Mean Revert)│  │  (Custom)   │  │   │
│  │  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘  │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                      │                                      │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                     AI DECISION ENGINE                               │   │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  │   │
│  │  │   Model     │  │   Feature   │  │ Confidence  │  │   Market    │  │   │
│  │  │  Inference  │  │  Extractor  │  │   Scorer    │  │   Regime    │  │   │
│  │  │   Engine    │  │             │  │             │  │  Detector   │  │   │
│  │  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘  │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                      │                                      │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                     RISK MANAGEMENT SYSTEM                           │   │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  │   │
│  │  │ Position    │  │   Daily     │  │  Portfolio  │  │  Circuit    │  │   │
│  │  │   Limits    │  │    P&L      │  │    Heat     │  │  Breaker    │  │   │
│  │  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘  │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                      │                                      │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                     EXECUTION ENGINE                                 │   │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  │   │
│  │  │   Order     │  │   Smart     │  │   Slippage  │  │   Order     │  │   │
│  │  │   Manager   │  │   Routing   │  │  Monitor    │  │   Queue     │  │   │
│  │  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘  │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                     NINJATRADER DLL INTEGRATION LAYER                        │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐ │
│  │  Account    │  │   Order     │  │  Position   │  │  Connection         │ │
│  │  Adapter    │  │   Adapter   │  │   Adapter   │  │  Manager            │ │
│  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                           BROKER / EXCHANGE                                  │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Component Details

### 1. Data Ingestion Layer

#### Market Data Stream
- Real-time tick data from NinjaTrader
- OHLCV bars (multiple timeframes)
- Level 2 order book data
- Trade volume and flow

#### Historical Cache
- Pre-loaded historical data for ML features
- Redis/Memcached for hot data
- PostgreSQL TimescaleDB for cold storage

#### News/Sentiment Processor (Optional)
- RSS feed integration
- Social media sentiment (Twitter/X, Reddit)
- Economic calendar events

### 2. Core Trading Engine

#### Strategy Orchestrator
- Manages multiple concurrent strategies
- Strategy lifecycle (start/stop/pause)
- Resource allocation per strategy
- Strategy performance tracking

#### AI Decision Engine
- **Model Inference Engine**: Loads and executes ML models
- **Feature Extractor**: Real-time feature calculation
- **Confidence Scorer**: Probability assessment for trades
- **Market Regime Detector**: Identifies trending/ranging/volatile markets

#### Risk Management System
- **Position Limits**: Max contracts per instrument
- **Daily P&L Limits**: Stop trading after daily loss
- **Portfolio Heat**: Total exposure monitoring
- **Circuit Breaker**: Emergency stop mechanisms

#### Execution Engine
- **Order Manager**: Order creation and modification
- **Smart Routing**: Best price/execution selection
- **Slippage Monitor**: Tracks execution quality
- **Order Queue**: Manages pending orders

### 3. NinjaTrader DLL Integration Layer

Abstracts NinjaTrader API to provide:
- Connection management
- Account information
- Order operations
- Position tracking
- Real-time callbacks

## Data Flow

```
1. Market Data Flow:
   NinjaTrader → Data Ingestion → Feature Engineering → AI Engine

2. Decision Flow:
   AI Engine → Risk Check → Strategy Validation → Order Generation

3. Execution Flow:
   Order Manager → Risk Confirmation → NinjaTrader → Broker

4. Position Flow:
   Broker → NinjaTrader → Position Adapter → Portfolio Manager
```

## Communication Patterns

### Internal Communication
- **In-Memory**: Within-process (fastest)
- **Message Bus**: Inter-service (RabbitMQ/ASB)
- **Event Sourcing**: For audit trail

### External Communication
- **NinjaTrader**: DLL interop calls
- **Database**: Async I/O with connection pooling
- **Monitoring**: Push metrics to Prometheus

## Threading Model

```
Main Thread: Coordination and configuration
├── Market Data Thread: Processes incoming ticks/bars
├── Strategy Threads: One per active strategy
├── AI Inference Thread: ML model execution
├── Risk Check Thread: Continuous risk monitoring
├── Order Thread: Order submission and tracking
└── Monitoring Thread: Metrics and health checks
```

## Technology Decisions

| Decision | Rationale |
|----------|-----------|
| .NET 8 | Native NinjaTrader DLL interop, high performance |
| ML.NET | Native .NET ML, ONNX model support |
| TimescaleDB | Time-series optimized PostgreSQL |
| RabbitMQ | Reliable async messaging |
| Prometheus | Industry standard metrics |
