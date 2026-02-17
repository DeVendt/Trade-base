using Microsoft.Extensions.Logging;
using TradeBase.Core.Interfaces;
using TradeBase.Core.Models;
using TradeBase.NinjaTraderAdapter.Connection;

namespace TradeBase;

/// <summary>
/// Main application entry point
/// </summary>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("🏴‍☠️ Trade Base - Headless Futures Trading System");
        Console.WriteLine("================================================");
        
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<Program>();
        
        // Parse command line args
        var mode = args.FirstOrDefault(a => a.StartsWith("--mode="))?.Split('=')[1] ?? "console";
        var symbol = args.FirstOrDefault(a => a.StartsWith("--symbol="))?.Split('=')[1] ?? "ES";
        var isPaper = args.Contains("--paper");
        
        logger.LogInformation("Starting in {Mode} mode with symbol {Symbol}", mode, symbol);
        
        // Setup connection config
        var config = new NinjaTraderConnectionConfig
        {
            Account = isPaper ? "Sim101" : "Live",
            AutoReconnect = true,
            MaxRetries = 3,
            RetryDelayMs = 5000
        };
        
        // Create connection
        using var connection = new NinjaTraderConnection(config, loggerFactory.CreateLogger<NinjaTraderConnection>());
        
        // Wire up events
        connection.ConnectionStateChanged += (s, e) =>
        {
            logger.LogInformation("Connection state changed: {OldState} -> {NewState} ({Reason})", 
                e.OldState, e.NewState, e.Reason);
        };
        
        connection.PriceUpdateReceived += (s, e) =>
        {
            logger.LogDebug("Price update: {Symbol} @ {Price}", e.Update.Symbol, e.Update.Price);
        };
        
        connection.OrderFilled += (s, e) =>
        {
            logger.LogInformation("Order filled: {OrderId} - {Symbol} {Action} {Quantity} @ {Price}",
                e.Order.OrderId, e.Order.Symbol, e.Order.Action, e.Order.Quantity, e.Order.AvgFillPrice);
        };
        
        // Connect
        logger.LogInformation("Connecting to NinjaTrader...");
        if (!await connection.ConnectAsync())
        {
            logger.LogError("Failed to connect to NinjaTrader");
            return 1;
        }
        
        // Subscribe to market data
        logger.LogInformation("Subscribing to market data for {Symbol}...", symbol);
        await connection.SubscribeMarketDataAsync(symbol, DataType.Last);
        
        // Test order submission (in paper mode)
        if (isPaper)
        {
            logger.LogInformation("Running paper trading test...");
            
            // Wait a bit for market data
            await Task.Delay(2000);
            
            // Submit a test market order
            var order = await connection.SubmitMarketOrderAsync(symbol, OrderAction.Buy, 1, config.Account);
            logger.LogInformation("Submitted test order: {OrderId}", order.OrderId);
            
            // Wait for fill
            await Task.Delay(2000);
            
            // Check position
            var position = await connection.GetPositionAsync(symbol, config.Account);
            if (position != null)
            {
                logger.LogInformation("Position: {Symbol} {Direction} {Quantity} @ {AvgPrice}",
                    position.Symbol, position.Direction, position.Quantity, position.AveragePrice);
            }
        }
        
        // Keep running until cancelled
        logger.LogInformation("System running. Press Ctrl+C to exit...");
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
        
        try
        {
            await Task.Delay(-1, cts.Token);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Shutting down...");
        }
        
        // Cleanup
        await connection.DisconnectAsync();
        logger.LogInformation("Trade Base stopped. Fair winds!");
        
        return 0;
    }
}
