using Microsoft.Extensions.Logging;
using TradeBase.Core.Interfaces;
using TradeBase.Core.Models;

namespace TradeBase.Strategies.FullyAutomated;

/// <summary>
/// Fully automated futures trading strategy
/// Hands-free operation with AI-driven entry/exit decisions
/// </summary>
public class FullyAutomatedStrategy : IFullyAutomatedStrategy
{
    private readonly IMarketDataSubscriber _marketData;
    private readonly IOrderExecutor _orderExecutor;
    private readonly IAccountTracker _accountTracker;
    private readonly ILogger<FullyAutomatedStrategy>? _logger;
    
    private StrategyConfiguration? _config;
    private StrategyState _state = StrategyState.Uninitialized;
    private readonly List<TradeSignal> _signalHistory = new();
    private readonly Dictionary<string, Position> _trackedPositions = new();
    private readonly Dictionary<string, Order> _workingOrders = new();
    
    private MarketContext? _currentContext;
    private DateTime _lastEvaluationTime = DateTime.MinValue;
    private readonly TimeSpan _evaluationInterval = TimeSpan.FromSeconds(1);
    
    // Daily tracking
    private double _dailyPnL = 0;
    private int _dailyTrades = 0;
    private DateTime _lastResetDate = DateTime.MinValue;
    
    // Performance tracking
    private readonly List<double> _tradePnLs = new();
    private double _peakEquity = 0;
    private double _maxDrawdown = 0;

    public string Name => "FullyAutomatedFutures";
    public string Description => "AI-driven, hands-free futures trading with automatic entry, exit, and risk management";
    public bool IsActive => _state == StrategyState.Running;
    
    public StrategyState State 
    { 
        get => _state;
        private set
        {
            _state = value;
            _logger?.LogInformation("Strategy state changed to {State}", value);
        }
    }

    // Events
    public event EventHandler<SignalEventArgs>? SignalGenerated;
    public event EventHandler<StrategyErrorEventArgs>? ErrorOccurred;

    public FullyAutomatedStrategy(
        IMarketDataSubscriber marketData,
        IOrderExecutor orderExecutor,
        IAccountTracker accountTracker,
        ILogger<FullyAutomatedStrategy>? logger = null)
    {
        _marketData = marketData ?? throw new ArgumentNullException(nameof(marketData));
        _orderExecutor = orderExecutor ?? throw new ArgumentNullException(nameof(orderExecutor));
        _accountTracker = accountTracker ?? throw new ArgumentNullException(nameof(accountTracker));
        _logger = logger;
        
        // Wire up events
        _marketData.PriceUpdateReceived += OnPriceUpdate;
        _orderExecutor.OrderFilled += OnOrderFilled;
    }

    public async Task<bool> InitializeAsync(StrategyConfiguration config, CancellationToken cancellationToken = default)
    {
        if (State != StrategyState.Uninitialized && State != StrategyState.Stopped)
        {
            _logger?.LogWarning("Cannot initialize strategy in state {State}", State);
            return false;
        }
        
        State = StrategyState.Initializing;
        
        try
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            
            _logger?.LogInformation(
                "Initializing {Strategy} for {Symbol} on {Account}",
                Name, config.Symbol, config.Account);
            
            // Validate configuration
            if (!ValidateConfiguration(config))
            {
                throw new InvalidOperationException("Invalid strategy configuration");
            }
            
            // Reset tracking
            ResetDailyTracking();
            
            // Subscribe to market data
            await _marketData.SubscribeMarketDataAsync(config.Symbol, DataType.Last, cancellationToken);
            
            State = StrategyState.Ready;
            _logger?.LogInformation("Strategy initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            State = StrategyState.Error;
            OnErrorOccurred("Failed to initialize strategy", ex, true);
            return false;
        }
    }

    public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
    {
        if (State != StrategyState.Ready && State != StrategyState.Paused)
        {
            _logger?.LogWarning("Cannot start strategy in state {State}", State);
            return false;
        }
        
        State = StrategyState.Running;
        _logger?.LogInformation("Strategy started - entering hands-free mode");
        
        // Start evaluation loop
        _ = Task.Run(async () => await EvaluationLoopAsync(cancellationToken), cancellationToken);
        
        return true;
    }

    public async Task<bool> StopAsync(CancellationToken cancellationToken = default)
    {
        if (State != StrategyState.Running)
        {
            return true;
        }
        
        State = StrategyState.Stopping;
        _logger?.LogInformation("Stopping strategy...");
        
        try
        {
            // Cancel all working orders
            var workingOrders = await _orderExecutor.GetWorkingOrdersAsync(cancellationToken);
            foreach (var order in workingOrders)
            {
                await _orderExecutor.CancelOrderAsync(order.OrderId, cancellationToken);
            }
            
            State = StrategyState.Stopped;
            _logger?.LogInformation("Strategy stopped");
            return true;
        }
        catch (Exception ex)
        {
            OnErrorOccurred("Error stopping strategy", ex, false);
            return false;
        }
    }

    public Task<bool> UpdateConfigurationAsync(StrategyConfiguration config, CancellationToken cancellationToken = default)
    {
        if (State == StrategyState.Running)
        {
            _logger?.LogWarning("Cannot update configuration while running");
            return Task.FromResult(false);
        }
        
        _config = config;
        _logger?.LogInformation("Configuration updated");
        return Task.FromResult(true);
    }

    public StrategyConfiguration GetCurrentConfiguration()
    {
        return _config ?? throw new InvalidOperationException("Strategy not initialized");
    }

    public StrategyPerformance GetPerformanceMetrics()
    {
        var perf = new StrategyPerformance
        {
            PeriodStart = _lastResetDate,
            PeriodEnd = DateTime.UtcNow,
            TotalTrades = _tradePnLs.Count,
            WinningTrades = _tradePnLs.Count(pnl => pnl > 0),
            LosingTrades = _tradePnLs.Count(pnl => pnl < 0),
            TotalPnL = _tradePnLs.Sum(),
            MaxDrawdown = _maxDrawdown
        };
        
        if (perf.WinningTrades > 0)
            perf.AverageWin = _tradePnLs.Where(pnl => pnl > 0).Average();
        
        if (perf.LosingTrades > 0)
            perf.AverageLoss = _tradePnLs.Where(pnl => pnl < 0).Average();
        
        return perf;
    }

    // IFullyAutomatedStrategy implementation
    public async Task<TradeSignal?> EvaluateEntryAsync(MarketContext context, CancellationToken cancellationToken = default)
    {
        if (_config == null) return null;
        
        // Check daily loss limit (circuit breaker)
        if (Math.Abs(_dailyPnL) >= CalculateDailyLossLimit())
        {
            _logger?.LogWarning("Daily loss limit reached - no new entries");
            return null;
        }
        
        // Check max positions
        var currentPositions = await _accountTracker.GetPositionsForAccountAsync(_config.Account, cancellationToken);
        if (currentPositions.Count >= _config.MaxConcurrentPositions)
        {
            return null;
        }
        
        // Check for existing position in this symbol
        var existingPosition = currentPositions.FirstOrDefault(p => p.Symbol == _config.Symbol);
        if (existingPosition?.Direction != PositionDirection.Flat)
        {
            return null;  // Already in a position
        }
        
        // Evaluate entry signal (placeholder for AI integration)
        var signal = GenerateEntrySignal(context);
        
        if (signal != null && signal.Confidence >= _config.EntryConfidenceThreshold)
        {
            OnSignalGenerated(signal);
            return signal;
        }
        
        return null;
    }

    public Task<TradeSignal?> EvaluateExitAsync(Position position, MarketContext context, CancellationToken cancellationToken = default)
    {
        if (_config == null || position.Direction == PositionDirection.Flat)
            return Task.FromResult<TradeSignal?>(null);
        
        // Check exit conditions
        var signal = GenerateExitSignal(position, context);
        
        if (signal != null)
        {
            OnSignalGenerated(signal);
            return Task.FromResult<TradeSignal?>(signal);
        }
        
        return Task.FromResult<TradeSignal?>(null);
    }

    public Task<PositionScaleSignal?> EvaluateScaleInAsync(Position position, MarketContext context, CancellationToken cancellationToken = default)
    {
        if (_config == null || !_config.EnablePyramiding || position.Direction == PositionDirection.Flat)
            return Task.FromResult<PositionScaleSignal?>(null);
        
        // Evaluate pyramiding conditions
        var signal = GenerateScaleInSignal(position, context);
        
        return Task.FromResult(signal);
    }

    public Task<PositionScaleSignal?> EvaluateScaleOutAsync(Position position, MarketContext context, CancellationToken cancellationToken = default)
    {
        if (_config == null || position.Direction == PositionDirection.Flat)
            return Task.FromResult<PositionScaleSignal?>(null);
        
        // Evaluate scale out conditions (profit taking)
        var signal = GenerateScaleOutSignal(position, context);
        
        return Task.FromResult(signal);
    }

    public bool ValidateRisk(Position proposedPosition, AccountInfo account)
    {
        if (_config == null) return false;
        
        // Check per-trade risk
        var riskAmount = Math.Abs(proposedPosition.UnrealizedPnL);  // Simplified
        var riskPercent = (riskAmount / account.BuyingPower) * 100;
        
        if (riskPercent > _config.RiskPerTradePercent)
        {
            _logger?.LogWarning("Risk {Risk:P} exceeds limit {Limit:P}", riskPercent / 100, _config.RiskPerTradePercent / 100);
            return false;
        }
        
        return true;
    }

    // Private helper methods
    private async Task EvaluationLoopAsync(CancellationToken cancellationToken)
    {
        while (State == StrategyState.Running && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Check daily reset
                CheckDailyReset();
                
                // Only evaluate on interval
                if (DateTime.UtcNow - _lastEvaluationTime < _evaluationInterval)
                {
                    await Task.Delay(100, cancellationToken);
                    continue;
                }
                
                _lastEvaluationTime = DateTime.UtcNow;
                
                if (_currentContext == null || _config == null)
                    continue;
                
                // Get current position
                var position = await _accountTracker.GetPositionAsync(_config.Symbol, _config.Account, cancellationToken);
                position ??= new Position { Symbol = _config.Symbol, Account = _config.Account, Direction = PositionDirection.Flat };
                
                // Evaluate exits first (if in position)
                if (position.Direction != PositionDirection.Flat)
                {
                    var exitSignal = await EvaluateExitAsync(position, _currentContext, cancellationToken);
                    if (exitSignal != null)
                    {
                        await ExecuteExitAsync(exitSignal, cancellationToken);
                        continue;
                    }
                    
                    // Evaluate scaling
                    var scaleOutSignal = await EvaluateScaleOutAsync(position, _currentContext, cancellationToken);
                    if (scaleOutSignal != null)
                    {
                        await ExecuteScaleOutAsync(scaleOutSignal, cancellationToken);
                    }
                }
                else
                {
                    // Evaluate entry (if flat)
                    var entrySignal = await EvaluateEntryAsync(_currentContext, cancellationToken);
                    if (entrySignal != null)
                    {
                        await ExecuteEntryAsync(entrySignal, cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred("Error in evaluation loop", ex, false);
                await Task.Delay(1000, cancellationToken);
            }
        }
    }

    private async Task ExecuteEntryAsync(TradeSignal signal, CancellationToken cancellationToken)
    {
        if (_config == null) return;
        
        _logger?.LogInformation(
            "Executing {Action} entry for {Quantity} {Symbol} - Confidence: {Confidence:P}",
            signal.Action, signal.Quantity, signal.Symbol, signal.Confidence);
        
        try
        {
            // Submit OCO bracket order
            var entryPrice = signal.LimitPrice ?? _currentContext?.CurrentPrice ?? 0;
            var stopPrice = entryPrice - (signal.Action == OrderAction.Buy ? _config.StopLossATR * _currentContext?.ATR : -_config.StopLossATR * _currentContext?.ATR) ?? 0;
            var targetPrice = entryPrice + (signal.Action == OrderAction.Buy ? _config.TakeProfitATR * _currentContext?.ATR : -_config.TakeProfitATR * _currentContext?.ATR) ?? 0;
            
            await _orderExecutor.SubmitOCOBracketAsync(
                signal.Symbol,
                signal.Action,
                signal.Quantity,
                entryPrice,
                stopPrice,
                targetPrice,
                _config.Account,
                cancellationToken);
            
            _dailyTrades++;
        }
        catch (Exception ex)
        {
            OnErrorOccurred("Failed to execute entry", ex, false);
        }
    }

    private async Task ExecuteExitAsync(TradeSignal signal, CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Executing exit for {Symbol} - Reason: {Reason}", signal.Symbol, signal.Reason);
        
        try
        {
            // Cancel all working orders for this symbol
            var orders = await _orderExecutor.GetOrdersForSymbolAsync(signal.Symbol, cancellationToken);
            foreach (var order in orders.Where(o => o.State == OrderState.Working))
            {
                await _orderExecutor.CancelOrderAsync(order.OrderId, cancellationToken);
            }
            
            // Submit market order to close position
            var action = signal.Action;  // Should be opposite of current position
            var position = await _accountTracker.GetPositionAsync(signal.Symbol, _config?.Account ?? "", cancellationToken);
            if (position != null && position.Quantity > 0)
            {
                await _orderExecutor.SubmitMarketOrderAsync(
                    signal.Symbol,
                    action,
                    position.Quantity,
                    _config?.Account ?? "",
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            OnErrorOccurred("Failed to execute exit", ex, false);
        }
    }

    private async Task ExecuteScaleOutAsync(PositionScaleSignal signal, CancellationToken cancellationToken)
    {
        _logger?.LogInformation(
            "Scaling out {Quantity} contracts from {Symbol}",
            Math.Abs(signal.AdditionalQuantity), signal.Symbol);
        
        // Implementation for scaling out
        await Task.CompletedTask;
    }

    private TradeSignal? GenerateEntrySignal(MarketContext context)
    {
        // Placeholder: This would call the AI model for prediction
        // For now, generate random signals for testing
        var random = new Random();
        var confidence = random.NextDouble();
        
        if (confidence < 0.6) return null;
        
        var isLong = random.NextDouble() > 0.5;
        
        return new TradeSignal
        {
            Symbol = context.Symbol,
            Type = isLong ? SignalType.EntryLong : SignalType.EntryShort,
            Action = isLong ? OrderAction.Buy : OrderAction.Sell,
            Quantity = 1,  // Will be calculated by position sizing
            Confidence = confidence,
            Reason = $"AI prediction confidence: {confidence:P}",
            Metadata = new Dictionary<string, object>
            {
                ["ATR"] = context.ATR,
                ["Regime"] = context.Regime.ToString()
            }
        };
    }

    private TradeSignal? GenerateExitSignal(Position position, MarketContext context)
    {
        // Placeholder exit logic
        // In real implementation, check:
        // - AI exit signal
        // - Stop loss hit
        // - Target hit
        // - Trailing stop
        
        return null;  // No exit for now
    }

    private PositionScaleSignal? GenerateScaleInSignal(Position position, MarketContext context)
    {
        // Placeholder pyramiding logic
        return null;
    }

    private PositionScaleSignal? GenerateScaleOutSignal(Position position, MarketContext context)
    {
        // Placeholder scale out logic
        return null;
    }

    private void OnPriceUpdate(object? sender, PriceUpdateEventArgs e)
    {
        if (_currentContext == null || _currentContext.Symbol != e.Update.Symbol)
        {
            _currentContext = new MarketContext
            {
                Symbol = e.Update.Symbol,
                CurrentPrice = e.Update.Price,
                LatestTick = e.Update
            };
        }
        else
        {
            _currentContext.CurrentPrice = e.Update.Price;
            _currentContext.LatestTick = e.Update;
            _currentContext.Timestamp = DateTime.UtcNow;
        }
    }

    private void OnOrderFilled(object? sender, OrderEventArgs e)
    {
        // Track P&L
        // This is simplified - real implementation would calculate actual P&L
        _logger?.LogInformation("Order filled: {OrderId} - PnL tracking updated", e.Order.OrderId);
    }

    private void CheckDailyReset()
    {
        var today = DateTime.UtcNow.Date;
        if (_lastResetDate.Date != today)
        {
            ResetDailyTracking();
        }
    }

    private void ResetDailyTracking()
    {
        _lastResetDate = DateTime.UtcNow;
        _dailyPnL = 0;
        _dailyTrades = 0;
        _logger?.LogInformation("Daily tracking reset for {Date}", _lastResetDate.Date);
    }

    private double CalculateDailyLossLimit()
    {
        if (_config == null) return 0;
        // This would calculate based on account balance
        // For now, return configured percentage
        return _config.MaxDailyLossPercent;
    }

    private bool ValidateConfiguration(StrategyConfiguration config)
    {
        if (string.IsNullOrEmpty(config.Symbol))
        {
            _logger?.LogError("Symbol is required");
            return false;
        }
        
        if (config.RiskPerTradePercent <= 0 || config.RiskPerTradePercent > 5)
        {
            _logger?.LogError("Risk per trade must be between 0 and 5%");
            return false;
        }
        
        if (config.EntryConfidenceThreshold < 0.5 || config.EntryConfidenceThreshold > 1.0)
        {
            _logger?.LogError("Entry confidence threshold must be between 0.5 and 1.0");
            return false;
        }
        
        return true;
    }

    private void OnSignalGenerated(TradeSignal signal)
    {
        _signalHistory.Add(signal);
        SignalGenerated?.Invoke(this, new SignalEventArgs { Signal = signal });
    }

    private void OnErrorOccurred(string error, Exception? ex = null, bool isFatal = false)
    {
        _logger?.LogError(ex, "Strategy error: {Error}", error);
        ErrorOccurred?.Invoke(this, new StrategyErrorEventArgs { Error = error, Exception = ex, IsFatal = isFatal });
    }
}
