# Testing and Backtesting Infrastructure

## Overview

A robust testing and backtesting infrastructure is essential for validating strategies, testing AI models, and ensuring system reliability before deploying to live trading.

## Testing Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    TESTING LAYERS                                            │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                    UNIT TESTS                                        │    │
│  │  - Individual component testing                                      │    │
│  │  - Mocked dependencies                                               │    │
│  │  - Fast execution                                                    │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                    │                                         │
│                                    ▼                                         │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                 INTEGRATION TESTS                                    │    │
│  │  - Component interactions                                            │    │
│  │  - Database operations                                               │    │
│  │  - NinjaTrader API (mocked)                                          │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                    │                                         │
│                                    ▼                                         │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                  BACKTESTS                                           │    │
│  │  - Historical data simulation                                        │    │
│  │  - Strategy performance validation                                   │    │
│  │  - AI model validation                                               │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                    │                                         │
│                                    ▼                                         │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                  PAPER TRADING                                       │    │
│  │  - Live market data, simulated execution                             │    │
│  │  - Real-time strategy validation                                     │    │
│  │  - NinjaTrader simulation mode                                       │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                    │                                         │
│                                    ▼                                         │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                  LIVE TRADING                                        │    │
│  │  - Production deployment                                             │    │
│  │  - Real money (small size initially)                                 │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Unit Testing Framework

```csharp
// Test base class
public abstract class TestBase
{
    protected IServiceProvider Services { get; private set; }
    protected ILoggerFactory LoggerFactory { get; private set; }
    
    [SetUp]
    public virtual void Setup()
    {
        var services = new ServiceCollection();
        
        // Add logging
        services.AddLogging(builder => builder.AddConsole());
        
        // Add common mocks
        services.AddSingleton(Mock.Of<IMarketDataAdapter>());
        services.AddSingleton(Mock.Of<IExecutionAdapter>());
        services.AddSingleton(Mock.Of<IRiskEngine>());
        
        Services = services.BuildServiceProvider();
        LoggerFactory = Services.GetRequiredService<ILoggerFactory>();
    }
}

// Example: Feature extraction tests
[TestFixture]
public class FeatureExtractorTests : TestBase
{
    private FeatureExtractor _extractor;
    
    [SetUp]
    public void Setup()
    {
        base.Setup();
        _extractor = new FeatureExtractor(LoggerFactory.CreateLogger<FeatureExtractor>());
    }
    
    [Test]
    public void CalculateSMA_WithValidData_ReturnsCorrectValue()
    {
        var bars = new List<Bar>
        {
            new() { Close = 100 },
            new() { Close = 102 },
            new() { Close = 101 },
            new() { Close = 103 },
            new() { Close = 104 }
        };
        
        var sma = _extractor.CalculateSMA(bars, 5);
        
        sma.Should().BeApproximately(102.0, 0.001);
    }
    
    [Test]
    public void CalculateRSI_WithOverboughtMarket_ReturnsHighValue()
    {
        // Generate consistently up bars
        var bars = Enumerable.Range(0, 20)
            .Select(i => new Bar { Close = 100 + i * 2.0 })
            .ToList();
            
        var rsi = _extractor.CalculateRSI(bars, 14);
        
        rsi.Should().BeGreaterThan(70);  // Overbought
    }
    
    [Test]
    public void ExtractFeatures_WithValidInput_ReturnsCompleteFeatureVector()
    {
        var bars = GenerateSampleBars(50);
        var currentBar = bars.Last();
        
        var features = _extractor.Extract(bars, currentBar);
        
        features.Should().NotBeNull();
        features.RSI14.Should().BeInRange(0, 100);
        features.SMA20.Should().BeGreaterThan(0);
        features.ATR14.Should().BeGreaterThan(0);
    }
}

// Example: Risk engine tests
[TestFixture]
public class RiskEngineTests : TestBase
{
    private RiskEngine _riskEngine;
    private Mock<IPositionRepository> _positionRepoMock;
    private RiskConfiguration _config;
    
    [SetUp]
    public void Setup()
    {
        base.Setup();
        
        _positionRepoMock = new Mock<IPositionRepository>();
        _config = new RiskConfiguration
        {
            MaxDailyLossPercent = 0.03m,
            MaxRiskPerTradePercent = 0.01m,
            MaxPositionSize = 10,
            MinAIConfidence = 0.6m
        };
        
        _riskEngine = new RiskEngine(
            _config,
            _positionRepoMock.Object,
            LoggerFactory.CreateLogger<RiskEngine>()
        );
    }
    
    [Test]
    public async Task ValidateSignal_DailyLossLimitReached_ReturnsDenied()
    {
        var portfolio = new PortfolioSnapshot
        {
            NetLiquidation = 100000,
            DailyPnL = -4000  // Exceeds 3% limit
        };
        
        var signal = new TradingSignal { AIConfidence = 0.8 };
        
        var result = await _riskEngine.ValidateSignalAsync(signal, portfolio);
        
        result.IsAllowed.Should().BeFalse();
        result.Reason.Should().Contain("loss limit");
    }
    
    [Test]
    public async Task ValidateSignal_LowAIConfidence_ReturnsDenied()
    {
        var portfolio = new PortfolioSnapshot
        {
            NetLiquidation = 100000,
            DailyPnL = 0
        };
        
        var signal = new TradingSignal { AIConfidence = 0.4 };  // Below 0.6 threshold
        
        var result = await _riskEngine.ValidateSignalAsync(signal, portfolio);
        
        result.IsAllowed.Should().BeFalse();
        result.Reason.Should().Contain("confidence");
    }
    
    [Test]
    public async Task ValidateSignal_ValidSignal_ReturnsAllowedWithSize()
    {
        var portfolio = new PortfolioSnapshot
        {
            NetLiquidation = 100000,
            DailyPnL = 500,
            BuyingPower = 50000
        };
        
        var signal = new TradingSignal 
        { 
            AIConfidence = 0.8,
            EntryPrice = 100,
            StopLossPercent = 0.01
        };
        
        var result = await _riskEngine.ValidateSignalAsync(signal, portfolio);
        
        result.IsAllowed.Should().BeTrue();
        result.AllowedSize.Should().BeGreaterThan(0);
        result.AllowedSize.Should().BeLessOrEqualTo(_config.MaxPositionSize);
    }
}
```

## Backtesting Framework

```csharp
public interface IBacktestEngine
{
    Task<BacktestResult> RunAsync(BacktestConfiguration config);
    Task<BacktestResult> RunStrategyAsync(IStrategy strategy, BacktestConfiguration config);
}

public class BacktestConfiguration
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<string> Instruments { get; set; } = new();
    public decimal InitialCapital { get; set; } = 100000;
    public List<TimeSpan> Timeframes { get; set; } = new() { TimeSpan.FromMinutes(5) };
    
    // Simulation settings
    public decimal CommissionPerContract { get; set; } = 2.50m;
    public decimal SlippageTicks { get; set; } = 1;
    public bool FillOnTouch { get; set; } = false;
    
    // Data source
    public string HistoricalDataPath { get; set; }
    public DataSourceType DataSource { get; set; } = DataSourceType.CSV;
}

public class BacktestEngine : IBacktestEngine
{
    private readonly IHistoricalDataProvider _dataProvider;
    private readonly MockExecutionAdapter _execution;
    private readonly ILogger<BacktestEngine> _logger;
    
    public async Task<BacktestResult> RunAsync(BacktestConfiguration config)
    {
        var result = new BacktestResult
        {
            Configuration = config,
            StartTime = DateTime.UtcNow
        };
        
        // Load historical data
        var historicalData = await _dataProvider.LoadAsync(
            config.Instruments,
            config.StartDate,
            config.EndDate,
            config.Timeframes
        );
        
        // Initialize simulation
        var portfolio = new BacktestPortfolio(config.InitialCapital);
        _execution.Initialize(portfolio, config);
        
        // Simulate each bar
        foreach (var timestamp in historicalData.GetTimestamps())
        {
            var bars = historicalData.GetBarsAt(timestamp);
            
            // Process each bar
            foreach (var bar in bars)
            {
                // Update positions with current price
                portfolio.UpdatePrices(bar.Instrument, bar.Close);
                
                // Process any pending orders
                await _execution.ProcessOrdersAsync(bar);
                
                // Run strategy
                // This would be replaced with actual strategy logic
                await OnBarAsync(bar, portfolio);
            }
            
            // Record equity at end of bar
            result.EquityCurve.Add(new EquityPoint
            {
                Timestamp = timestamp,
                Equity = portfolio.TotalEquity,
                Cash = portfolio.Cash,
                OpenPositionsValue = portfolio.PositionsValue
            });
        }
        
        // Calculate statistics
        result.Statistics = CalculateStatistics(result);
        result.EndTime = DateTime.UtcNow;
        
        return result;
    }
    
    private BacktestStatistics CalculateStatistics(BacktestResult result)
    {
        var equity = result.EquityCurve.Select(e => e.Equity).ToList();
        var returns = equity.Zip(equity.Skip(1), (prev, curr) => (curr - prev) / prev).ToList();
        
        var trades = result.Trades;
        var winningTrades = trades.Where(t => t.NetPnL > 0).ToList();
        var losingTrades = trades.Where(t => t.NetPnL < 0).ToList();
        
        return new BacktestStatistics
        {
            TotalReturn = (equity.Last() - equity.First()) / equity.First(),
            AnnualizedReturn = CalculateAnnualizedReturn(equity, result.Configuration),
            SharpeRatio = CalculateSharpeRatio(returns),
            SortinoRatio = CalculateSortinoRatio(returns),
            MaxDrawdown = CalculateMaxDrawdown(equity),
            MaxDrawdownDuration = CalculateMaxDrawdownDuration(equity),
            
            TotalTrades = trades.Count,
            WinningTrades = winningTrades.Count,
            LosingTrades = losingTrades.Count,
            WinRate = trades.Count > 0 ? (double)winningTrades.Count / trades.Count : 0,
            
            AverageWin = winningTrades.Any() ? winningTrades.Average(t => t.NetPnL) : 0,
            AverageLoss = losingTrades.Any() ? losingTrades.Average(t => t.NetPnL) : 0,
            LargestWin = winningTrades.Any() ? winningTrades.Max(t => t.NetPnL) : 0,
            LargestLoss = losingTrades.Any() ? losingTrades.Min(t => t.NetPnL) : 0,
            
            ProfitFactor = CalculateProfitFactor(trades),
            Expectancy = CalculateExpectancy(trades),
            
            AverageTradeDuration = trades.Any() ? 
                TimeSpan.FromTicks((long)trades.Average(t => t.Duration.Ticks)) : TimeSpan.Zero,
                
            CalmarRatio = CalculateCalmarRatio(equity, result.Configuration)
        };
    }
}

public class BacktestResult
{
    public BacktestConfiguration Configuration { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration => EndTime - StartTime;
    
    public List<TradeRecord> Trades { get; set; } = new();
    public List<EquityPoint> EquityCurve { get; set; } = new();
    public List<AIDecision> Decisions { get; set; } = new();
    
    public BacktestStatistics Statistics { get; set; }
}

public class BacktestStatistics
{
    // Returns
    public decimal TotalReturn { get; set; }
    public decimal AnnualizedReturn { get; set; }
    
    // Risk metrics
    public double SharpeRatio { get; set; }
    public double SortinoRatio { get; set; }
    public decimal MaxDrawdown { get; set; }
    public TimeSpan MaxDrawdownDuration { get; set; }
    public double CalmarRatio { get; set; }
    
    // Trade metrics
    public int TotalTrades { get; set; }
    public int WinningTrades { get; set; }
    public int LosingTrades { get; set; }
    public double WinRate { get; set; }
    
    public decimal AverageWin { get; set; }
    public decimal AverageLoss { get; set; }
    public decimal LargestWin { get; set; }
    public decimal LargestLoss { get; set; }
    
    public double ProfitFactor { get; set; }
    public decimal Expectancy { get; set; }
    
    public TimeSpan AverageTradeDuration { get; set; }
    public int ConsecutiveWins { get; set; }
    public int ConsecutiveLosses { get; set; }
}
```

## Paper Trading

```csharp
public interface IPaperTradingEngine
{
    Task StartAsync(PaperTradingConfiguration config);
    Task StopAsync();
    PaperTradingStatus GetStatus();
}

public class PaperTradingEngine : IPaperTradingEngine
{
    private readonly IMarketDataAdapter _marketData;
    private readonly IStrategyOrchestrator _strategies;
    private readonly IExecutionAdapter _execution;
    private readonly IRiskEngine _risk;
    
    public async Task StartAsync(PaperTradingConfiguration config)
    {
        _logger.LogInformation("Starting paper trading with {Capital:C} virtual capital", config.InitialCapital);
        
        // Initialize paper trading account
        var account = new PaperAccount
        {
            Name = config.AccountName,
            InitialCapital = config.InitialCapital,
            CurrentEquity = config.InitialCapital,
            BuyingPower = config.InitialCapital * 4  // Simulate margin
        };
        
        // Connect to live market data (no real orders)
        await _marketData.ConnectAsync();
        
        // Subscribe to instruments
        foreach (var instrument in config.Instruments)
        {
            await _marketData.SubscribeAsync(instrument, DataType.Bar);
        }
        
        // Start strategies
        foreach (var strategy in _strategies.RunningStrategies)
        {
            await strategy.StartAsync();
        }
        
        _status = PaperTradingStatus.Running;
    }
    
    // Execution uses NinjaTrader SIM mode
    // All orders go through but no real money at risk
}
```

## AI Model Validation

```csharp
public interface IModelValidator
{
    Task<ModelValidationResult> ValidateAsync(ModelValidationConfig config);
    Task<WalkForwardResult> WalkForwardAnalysisAsync(WalkForwardConfig config);
}

public class ModelValidator : IModelValidator
{
    public async Task<ModelValidationResult> ValidateAsync(ModelValidationConfig config)
    {
        var result = new ModelValidationResult();
        
        // Load validation data
        var validationData = await LoadValidationDataAsync(config);
        
        // Run predictions
        var predictions = new List<PredictionResult>();
        foreach (var sample in validationData)
        {
            var prediction = await _model.PredictAsync(sample.Features);
            predictions.Add(new PredictionResult
            {
                Actual = sample.Label,
                Predicted = prediction.Action,
                Confidence = prediction.Confidence,
                Timestamp = sample.Timestamp
            });
        }
        
        // Calculate metrics
        result.Accuracy = CalculateAccuracy(predictions);
        result.Precision = CalculatePrecision(predictions);
        result.Recall = CalculateRecall(predictions);
        result.F1Score = 2 * (result.Precision * result.Recall) / (result.Precision + result.Recall);
        
        // Confidence calibration
        result.CalibrationCurve = CalculateCalibration(predictions);
        result.AverageConfidence = predictions.Average(p => p.Confidence);
        result.ConfidenceAccuracyCorrelation = CalculateCorrelation(predictions);
        
        // Trading performance
        result.TradingMetrics = CalculateTradingMetrics(predictions, config);
        
        // Detect overfitting
        result.IsOverfit = DetectOverfitting(predictions, config);
        
        return result;
    }
    
    public async Task<WalkForwardResult> WalkForwardAnalysisAsync(WalkForwardConfig config)
    {
        var result = new WalkForwardResult();
        
        // Divide data into windows
        var windows = CreateWindows(config.StartDate, config.EndDate, config.WindowSize, config.StepSize);
        
        foreach (var window in windows)
        {
            // Train on in-sample data
            var trainData = await LoadDataAsync(window.TrainStart, window.TrainEnd);
            var model = await _modelTrainer.TrainAsync(trainData);
            
            // Test on out-of-sample data
            var testData = await LoadDataAsync(window.TestStart, window.TestEnd);
            var performance = await TestModelAsync(model, testData);
            
            result.WindowResults.Add(new WindowResult
            {
                Period = window,
                SharpeRatio = performance.SharpeRatio,
                TotalReturn = performance.TotalReturn,
                MaxDrawdown = performance.MaxDrawdown,
                WinRate = performance.WinRate
            });
        }
        
        // Calculate consistency metrics
        result.IsConsistent = result.WindowResults.All(w => w.SharpeRatio > 0);
        result.AverageSharpe = result.WindowResults.Average(w => w.SharpeRatio);
        result.SharpeStdDev = CalculateStdDev(result.WindowResults.Select(w => w.SharpeRatio));
        
        return result;
    }
}

public class ModelValidationResult
{
    // Classification metrics
    public double Accuracy { get; set; }
    public double Precision { get; set; }
    public double Recall { get; set; }
    public double F1Score { get; set; }
    public Dictionary<string, double> ClassMetrics { get; set; } = new();
    
    // Confidence metrics
    public double AverageConfidence { get; set; }
    public double ConfidenceAccuracyCorrelation { get; set; }
    public List<CalibrationPoint> CalibrationCurve { get; set; } = new();
    
    // Trading metrics
    public TradingMetrics TradingMetrics { get; set; }
    
    // Overfitting detection
    public bool IsOverfit { get; set; }
    public double TrainTestGap { get; set; }
}
```

## Test Data Generation

```csharp
public class TestDataGenerator
{
    public List<Bar> GenerateTrendingMarket(int bars, double startPrice, double trend, double volatility)
    {
        var random = new Random(42);  // Seed for reproducibility
        var result = new List<Bar>();
        var price = startPrice;
        
        for (int i = 0; i < bars; i++)
        {
            var change = trend + (random.NextDouble() - 0.5) * volatility;
            var open = price;
            var close = price * (1 + change);
            var high = Math.Max(open, close) * (1 + random.NextDouble() * volatility * 0.5);
            var low = Math.Min(open, close) * (1 - random.NextDouble() * volatility * 0.5);
            
            result.Add(new Bar
            {
                Timestamp = DateTime.UtcNow.AddMinutes(i * 5),
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = (long)(random.Next(1000, 10000))
            });
            
            price = close;
        }
        
        return result;
    }
    
    public List<Bar> GenerateRangingMarket(int bars, double centerPrice, double range, double volatility)
    {
        var random = new Random(42);
        var result = new List<Bar>();
        
        for (int i = 0; i < bars; i++)
        {
            var position = Math.Sin(i * 0.1) * range + (random.NextDouble() - 0.5) * volatility;
            var close = centerPrice + position;
            var open = close + (random.NextDouble() - 0.5) * volatility * 0.5;
            var high = Math.Max(open, close) + random.NextDouble() * volatility * 0.3;
            var low = Math.Min(open, close) - random.NextDouble() * volatility * 0.3;
            
            result.Add(new Bar
            {
                Timestamp = DateTime.UtcNow.AddMinutes(i * 5),
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = (long)(random.Next(1000, 10000))
            });
        }
        
        return result;
    }
    
    public List<Bar> GenerateVolatileMarket(int bars, double startPrice, double volatility)
    {
        var random = new Random(42);
        var result = new List<Bar>();
        var price = startPrice;
        
        for (int i = 0; i < bars; i++)
        {
            var change = (random.NextDouble() - 0.5) * volatility * 3;  // Higher volatility
            var open = price;
            var close = price * (1 + change);
            var high = Math.Max(open, close) * (1 + random.NextDouble() * volatility);
            var low = Math.Min(open, close) * (1 - random.NextDouble() * volatility);
            
            // Add occasional gaps
            if (random.NextDouble() < 0.05)
            {
                close = close * (1 + (random.NextDouble() - 0.5) * 0.02);
            }
            
            result.Add(new Bar
            {
                Timestamp = DateTime.UtcNow.AddMinutes(i * 5),
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = (long)(random.Next(5000, 50000))  // Higher volume
            });
            
            price = close;
        }
        
        return result;
    }
}
```

## Performance Testing

```csharp
[TestFixture]
public class PerformanceTests
{
    [Test]
    public async Task Strategy_OnBar_ProcessingTime_UnderThreshold()
    {
        var strategy = CreateTestStrategy();
        var bars = GenerateBars(1000);
        
        var stopwatch = Stopwatch.StartNew();
        
        foreach (var bar in bars)
        {
            await strategy.OnBarAsync(bar);
        }
        
        stopwatch.Stop();
        
        var averageMs = stopwatch.ElapsedMilliseconds / (double)bars.Count;
        averageMs.Should().BeLessThan(10);  // Less than 10ms per bar
    }
    
    [Test]
    public async Task AI_Inference_Latency_UnderThreshold()
    {
        var model = await LoadModelAsync("test-model.onnx");
        var features = GenerateFeatures(1000);
        
        var stopwatch = Stopwatch.StartNew();
        
        foreach (var feature in features)
        {
            await model.PredictAsync(feature);
        }
        
        stopwatch.Stop();
        
        var averageMs = stopwatch.ElapsedMilliseconds / (double)features.Count;
        averageMs.Should().BeLessThan(5);  // Less than 5ms inference
    }
    
    [Test]
    public async Task OrderSubmission_Latency_UnderThreshold()
    {
        var execution = CreateMockExecution();
        var orders = GenerateOrders(100);
        
        var latencies = new List<long>();
        
        foreach (var order in orders)
        {
            var sw = Stopwatch.StartNew();
            await execution.SubmitOrderAsync(order);
            sw.Stop();
            latencies.Add(sw.ElapsedMilliseconds);
        }
        
        latencies.Average().Should().BeLessThan(50);  // Average < 50ms
        latencies.Max().Should().BeLessThan(100);     // Max < 100ms
    }
}
```
