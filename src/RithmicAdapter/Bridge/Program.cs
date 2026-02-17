using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TradeBase.RithmicAdapter.Bridge.Services;

namespace TradeBase.RithmicAdapter.Bridge;

public class Program
{
    public static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.ConfigureKestrel(options =>
                {
                    // gRPC requires HTTP/2
                    options.ListenAnyIP(50051, listenOptions =>
                    {
                        listenOptions.Protocols = HttpProtocols.Http2;
                    });
                });
                
                webBuilder.UseStartup<Startup>();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            });
}

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddGrpc();
        services.AddSingleton<IRithmicConnection, MockRithmicConnection>();
        services.AddHostedService<RithmicConnectionService>();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseRouting();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGrpcService<MarketDataGrpcService>();
            endpoints.MapGrpcService<OrderExecutionGrpcService>();
            endpoints.MapGrpcService<AccountGrpcService>();
            endpoints.MapGrpcService<ConnectionGrpcService>();
            
            endpoints.MapGet("/", async context =>
            {
                await context.Response.WriteAsync(
                    "Rithmic Bridge is running. Use gRPC client to connect.");
            });
            
            endpoints.MapGet("/health", async context =>
            {
                await context.Response.WriteAsync("OK");
            });
        });
    }
}

/// <summary>
/// Background service that manages Rithmic connection lifecycle
/// </summary>
public class RithmicConnectionService : BackgroundService
{
    private readonly IRithmicConnection _rithmic;
    private readonly ILogger<RithmicConnectionService> _logger;

    public RithmicConnectionService(IRithmicConnection rithmic, ILogger<RithmicConnectionService> logger)
    {
        _rithmic = rithmic;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Rithmic Connection Service starting...");
        
        // Connection will be established when Connect gRPC call is received
        // This service just keeps the connection alive
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_rithmic.IsConnected)
                {
                    // Send heartbeat or check connection health
                    await Task.Delay(30000, stoppingToken); // 30s heartbeat
                }
                else
                {
                    await Task.Delay(1000, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in connection service");
                await Task.Delay(5000, stoppingToken);
            }
        }
        
        _logger.LogInformation("Rithmic Connection Service stopping...");
        await _rithmic.DisconnectAsync();
    }
}

// Placeholder implementations for other services
public class OrderExecutionGrpcService : OrderExecutionService.OrderExecutionServiceBase { }
public class AccountGrpcService : AccountService.AccountServiceBase { }
public class ConnectionGrpcService : ConnectionService.ConnectionServiceBase { }

// Mock implementation for development
public class MockRithmicConnection : IRithmicConnection
{
    public bool IsConnected { get; private set; }
    
    public event EventHandler<RithmicPriceEventArgs>? PriceUpdate;
    public event EventHandler<RithmicBarEventArgs>? BarUpdate;
    public event EventHandler<RithmicOrderEventArgs>? OrderUpdate;
    public event EventHandler<RithmicPositionEventArgs>? PositionUpdate;

    public async Task ConnectAsync(string username, string password, string server)
    {
        await Task.Delay(500); // Simulate connection
        IsConnected = true;
        
        // Start mock data generation
        _ = Task.Run(GenerateMockDataAsync);
    }

    public async Task DisconnectAsync()
    {
        IsConnected = false;
        await Task.CompletedTask;
    }

    public Task SubscribeMarketDataAsync(string symbol, string exchange) => Task.CompletedTask;
    public Task UnsubscribeMarketDataAsync(string symbol, string exchange) => Task.CompletedTask;
    public Task SubscribeBarDataAsync(string symbol, int intervalMinutes) => Task.CompletedTask;
    public Task UnsubscribeBarDataAsync(string symbol, int intervalMinutes) => Task.CompletedTask;
    
    public (double Bid, double Ask, double Last, long Volume) GetCurrentPrice(string symbol)
    {
        return (4500.25, 4500.50, 4500.50, 100);
    }

    public Task<string> SubmitOrderAsync(RithmicOrder order) => Task.FromResult(Guid.NewGuid().ToString());
    public Task<bool> CancelOrderAsync(string orderId) => Task.FromResult(true);
    public Task<bool> ModifyOrderAsync(string orderId, RithmicOrderChanges changes) => Task.FromResult(true);

    private async Task GenerateMockDataAsync()
    {
        var random = new Random();
        var basePrice = 4500.0;
        
        while (IsConnected)
        {
            basePrice += (random.NextDouble() - 0.5) * 0.5;
            
            PriceUpdate?.Invoke(this, new RithmicPriceEventArgs
            {
                Symbol = "ES",
                Exchange = "CME",
                Bid = basePrice - 0.25,
                Ask = basePrice + 0.25,
                Last = basePrice,
                Volume = random.Next(1, 100)
            });
            
            await Task.Delay(1000);
        }
    }
}
