# Deployment and Operations

## Overview

This document covers deployment architecture, configuration management, monitoring, and operational procedures for the headless trading platform.

## Deployment Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    DEPLOYMENT OPTIONS                                        │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  OPTION 1: ON-PREMISES (RECOMMENDED FOR LOW LATENCY)                        │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │  ┌─────────────┐    ┌─────────────┐    ┌─────────────────────┐     │    │
│  │  │   Trading   │◄──►│  NinjaTrader│◄──►│   Broker/Exchange   │     │    │
│  │  │   Bot       │    │   Platform  │    │                     │     │    │
│  │  │   (.NET)    │    │             │    │                     │     │    │
│  │  └─────────────┘    └─────────────┘    └─────────────────────┘     │    │
│  │         │                                                           │    │
│  │         ▼                                                           │    │
│  │  ┌─────────────┐                                                     │    │
│  │  │  PostgreSQL │                                                     │    │
│  │  │  (Local)    │                                                     │    │
│  │  └─────────────┘                                                     │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
│  OPTION 2: HYBRID (RECOMMENDED FOR RELIABILITY)                             │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │  ON-PREMISES                      │  CLOUD                          │    │
│  │  ┌─────────────┐                  │  ┌─────────────────────────┐    │    │
│  │  │   Trading   │◄──► NinjaTrader │  │   Monitoring & Logging  │    │    │
│  │  │   Bot       │                  │  │   - Prometheus/Grafana  │    │    │
│  │  └─────────────┘                  │  │   - Seq/ELK             │    │    │
│  │       │                           │  │   - Alerting            │    │    │
│  │       ▼                           │  │                         │    │    │
│  │  ┌─────────────┐                  │  │   Model Training        │    │    │
│  │  │  Local DB   │──────VPN/SSH────►│  │   - GPU instances       │    │    │
│  │  │  (Cache)    │                  │  │   - Data storage        │    │    │
│  │  └─────────────┘                  │  │                         │    │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
│  OPTION 3: FULL CLOUD (NOT RECOMMENDED FOR LIVE TRADING)                    │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │  ┌─────────────┐    ┌─────────────┐    ┌─────────────────────┐     │    │
│  │  │   Trading   │◄──►│  VPN/VPS    │◄──►│   NinjaTrader at    │     │    │
│  │  │   Bot       │    │  Connection │    │   Home/Office       │     │    │
│  │  │   (Cloud)   │    │             │    │   (Remote Desktop)  │     │    │
│  │  └─────────────┘    └─────────────┘    └─────────────────────┘     │    │
│  │         │                                                           │    │
│  │         ▼                                                           │    │
│  │  ┌─────────────────────────────────────────────────────────────┐    │    │
│  │  │              Cloud Managed Services                          │    │    │
│  │  │  ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────────────┐   │    │    │
│  │  │  │   RDS   │ │  ElastiCache│ │   S3    │ │  CloudWatch     │   │    │    │
│  │  │  │(PostgreSQL)│ │  (Redis)   │ │(Models) │ │  (Monitoring)   │   │    │    │
│  │  │  └─────────┘ └─────────┘ └─────────┘ └─────────────────┘   │    │    │
│  │  └─────────────────────────────────────────────────────────────┘    │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Configuration Management

```csharp
// Configuration structure
public class TradingBotConfiguration
{
    public ApplicationConfig Application { get; set; }
    public NinjaTraderConfig NinjaTrader { get; set; }
    public DatabaseConfig Database { get; set; }
    public RiskConfig Risk { get; set; }
    public AIConfig AI { get; set; }
    public MonitoringConfig Monitoring { get; set; }
    public List<StrategyConfig> Strategies { get; set; }
}

public class ApplicationConfig
{
    public string Environment { get; set; } = "Development";
    public string InstanceId { get; set; }
    public bool EnablePaperTrading { get; set; } = true;
    public TimeSpan StartupDelay { get; set; } = TimeSpan.FromSeconds(30);
    public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;
}

public class NinjaTraderConfig
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 3692;
    public string ApiKey { get; set; }
    public string AccountName { get; set; } = "Sim101";
    public bool AutoReconnect { get; set; } = true;
    public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(5);
    public int MaxReconnectAttempts { get; set; } = 10;
}

public class DatabaseConfig
{
    public string ConnectionString { get; set; }
    public int MaxPoolSize { get; set; } = 100;
    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public bool EnableRetries { get; set; } = true;
}

public class AIConfig
{
    public string ModelsPath { get; set; } = "./models";
    public string ActiveModelVersion { get; set; }
    public bool UseGPU { get; set; } = false;
    public int BatchSize { get; set; } = 1;
    public TimeSpan InferenceTimeout { get; set; } = TimeSpan.FromMilliseconds(100);
}
```

### Configuration Sources (Priority Order)

```csharp
// appsettings.json (base configuration)
// appsettings.{Environment}.json (environment-specific)
// Environment variables (production secrets)
// Key Vault / AWS Secrets Manager (sensitive data)
// Command line arguments (runtime overrides)

public static class ConfigurationBuilder
{
    public static IConfiguration Build(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
        
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .AddUserSecrets<Program>()
            .AddCommandLine(args)
            .Build();
    }
}
```

## Container Deployment

```dockerfile
# Dockerfile
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/TradingBot/TradingBot.csproj", "src/TradingBot/"]
RUN dotnet restore "src/TradingBot/TradingBot.csproj"
COPY . .
WORKDIR "/src/src/TradingBot"
RUN dotnet build "TradingBot.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TradingBot.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create directories for models and logs
RUN mkdir -p /app/models /app/logs

# Non-root user for security
RUN useradd -m -s /bin/bash trader && chown -R trader:trader /app
USER trader

ENTRYPOINT ["dotnet", "TradingBot.dll"]
```

```yaml
# docker-compose.yml
version: '3.8'

services:
  tradingbot:
    build:
      context: .
      dockerfile: Dockerfile
    container_name: tradingbot
    restart: unless-stopped
    environment:
      - DOTNET_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=${DB_CONNECTION_STRING}
      - NinjaTrader__ApiKey=${NT_API_KEY}
    volumes:
      - ./models:/app/models:ro
      - ./logs:/app/logs
      - ./config:/app/config:ro
    networks:
      - trading-network
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s
    deploy:
      resources:
        limits:
          cpus: '2'
          memory: 4G
        reservations:
          cpus: '1'
          memory: 2G

  postgres:
    image: timescale/timescaledb:latest-pg15
    container_name: tradingbot-db
    restart: unless-stopped
    environment:
      - POSTGRES_USER=${DB_USER}
      - POSTGRES_PASSWORD=${DB_PASSWORD}
      - POSTGRES_DB=tradingbot
    volumes:
      - postgres-data:/var/lib/postgresql/data
    ports:
      - "5432:5432"
    networks:
      - trading-network

  redis:
    image: redis:7-alpine
    container_name: tradingbot-cache
    restart: unless-stopped
    volumes:
      - redis-data:/data
    networks:
      - trading-network

  prometheus:
    image: prom/prometheus:latest
    container_name: tradingbot-prometheus
    restart: unless-stopped
    volumes:
      - ./monitoring/prometheus.yml:/etc/prometheus/prometheus.yml:ro
      - prometheus-data:/prometheus
    ports:
      - "9090:9090"
    networks:
      - trading-network

  grafana:
    image: grafana/grafana:latest
    container_name: tradingbot-grafana
    restart: unless-stopped
    environment:
      - GF_SECURITY_ADMIN_USER=${GRAFANA_USER}
      - GF_SECURITY_ADMIN_PASSWORD=${GRAFANA_PASSWORD}
    volumes:
      - ./monitoring/grafana/dashboards:/etc/grafana/provisioning/dashboards:ro
      - ./monitoring/grafana/datasources:/etc/grafana/provisioning/datasources:ro
      - grafana-data:/var/lib/grafana
    ports:
      - "3000:3000"
    networks:
      - trading-network

volumes:
  postgres-data:
  redis-data:
  prometheus-data:
  grafana-data:

networks:
  trading-network:
    driver: bridge
```

## Monitoring and Observability

### Metrics Collection

```csharp
public class TradingMetrics
{
    private readonly Meter _meter = new("TradingBot", "1.0.0");
    
    // System metrics
    private readonly Counter<long> _ordersSubmitted;
    private readonly Counter<long> _ordersFilled;
    private readonly Counter<long> _ordersCancelled;
    private readonly Counter<long> _ordersRejected;
    
    // Performance metrics
    private readonly Histogram<double> _orderLatency;
    private readonly Histogram<double> _inferenceLatency;
    private readonly Histogram<double> _pnlPerTrade;
    
    // Position metrics
    private readonly ObservableGauge<int> _openPositions;
    private readonly ObservableGauge<decimal> _totalExposure;
    private readonly ObservableGauge<decimal> _dailyPnL;
    
    public TradingMetrics()
    {
        _ordersSubmitted = _meter.CreateCounter<long>("orders.submitted");
        _ordersFilled = _meter.CreateCounter<long>("orders.filled");
        _orderLatency = _meter.CreateHistogram<double>("order.latency_ms");
        _inferenceLatency = _meter.CreateHistogram<double>("ai.inference_ms");
        
        _openPositions = _meter.CreateObservableGauge<int>("positions.open", 
            () => GetOpenPositionsCount());
    }
    
    public void RecordOrderSubmitted(string instrument, OrderType type)
    {
        _ordersSubmitted.Add(1, 
            new KeyValuePair<string, object?>("instrument", instrument),
            new KeyValuePair<string, object?>("type", type.ToString()));
    }
    
    public void RecordOrderFilled(string orderId, TimeSpan latency, double slippage)
    {
        _ordersFilled.Add(1);
        _orderLatency.Record(latency.TotalMilliseconds,
            new KeyValuePair<string, object?>("slippage", slippage));
    }
    
    public void RecordInference(TimeSpan latency, double confidence)
    {
        _inferenceLatency.Record(latency.TotalMilliseconds,
            new KeyValuePair<string, object?>("confidence_bucket", GetConfidenceBucket(confidence)));
    }
}
```

### Health Checks

```csharp
public class TradingBotHealthChecks : IHealthCheck
{
    private readonly INinjaTraderConnection _ntConnection;
    private readonly IStrategyOrchestrator _strategies;
    private readonly IDbConnection _dbConnection;
    
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        var checks = new Dictionary<string, object>();
        var status = HealthStatus.Healthy;
        
        // Check NinjaTrader connection
        if (_ntConnection.State != ConnectionState.Connected)
        {
            checks["ninjatrader"] = $"Disconnected: {_ntConnection.State}";
            status = HealthStatus.Unhealthy;
        }
        else
        {
            checks["ninjatrader"] = "Connected";
        }
        
        // Check strategies
        var runningStrategies = _strategies.RunningStrategies.Count;
        checks["strategies_running"] = runningStrategies;
        
        // Check database
        try
        {
            await _dbConnection.ExecuteAsync("SELECT 1");
            checks["database"] = "Connected";
        }
        catch (Exception ex)
        {
            checks["database"] = $"Error: {ex.Message}";
            status = HealthStatus.Unhealthy;
        }
        
        // Check daily loss limit
        var dailyPnL = await GetDailyPnLAsync();
        checks["daily_pnl"] = dailyPnL;
        if (dailyPnL < -AccountValue * 0.05m)  // 5% daily loss
        {
            checks["risk_status"] = "WARNING: Near daily loss limit";
            if (status == HealthStatus.Healthy)
                status = HealthStatus.Degraded;
        }
        
        return new HealthCheckResult(status, data: checks);
    }
}
```

### Logging

```csharp
public static class LoggingConfiguration
{
    public static IHostBuilder ConfigureTradingBotLogging(this IHostBuilder host)
    {
        return host.UseSerilog((context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("InstanceId", 
                    context.Configuration["Application:InstanceId"])
                .Enrich.WithProperty("Environment", 
                    context.HostingEnvironment.EnvironmentName)
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(
                    path: "logs/tradingbot-.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30)
                .WriteTo.Seq(
                    serverUrl: context.Configuration["Monitoring:SeqUrl"])
                .Filter.ByExcluding(Matching.FromSource("Microsoft.AspNetCore"));
        });
    }
}

// Structured logging example
public class TradeLogger
{
    private readonly ILogger<TradeLogger> _logger;
    
    public void LogTradeExecuted(Trade trade)
    {
        _logger.LogInformation(
            "Trade executed: {TradeId} | Instrument: {Instrument} | Direction: {Direction} | " +
            "Entry: {EntryPrice:F2} | Exit: {ExitPrice:F2} | PnL: {PnL:C} | Duration: {Duration}",
            trade.Id,
            trade.Instrument,
            trade.Direction,
            trade.EntryPrice,
            trade.ExitPrice,
            trade.NetPnL,
            trade.Duration);
    }
    
    public void LogAIDecision(AIDecision decision)
    {
        _logger.LogInformation(
            "AI Decision: {Instrument} | Action: {Action} | Confidence: {Confidence:P} | " +
            "Expected Return: {ExpectedReturn:F4} | Regime: {Regime} | Model: {ModelVersion}",
            decision.Instrument,
            decision.Action,
            decision.Confidence,
            decision.ExpectedReturn,
            decision.DetectedRegime,
            decision.ModelVersion);
    }
    
    public void LogRiskEvent(RiskEvent riskEvent)
    {
        _logger.LogWarning(
            "Risk Event: {EventType} | Instrument: {Instrument} | " +
            "Reason: {Reason} | Current Exposure: {Exposure:C}",
            riskEvent.Type,
            riskEvent.Instrument,
            riskEvent.Reason,
            riskEvent.CurrentExposure);
    }
}
```

## Alerting

```csharp
public interface IAlertingService
{
    Task SendAlertAsync(Alert alert);
    Task SendTradeNotificationAsync(Trade trade);
    Task SendRiskAlertAsync(RiskEvent riskEvent);
}

public class AlertingService : IAlertingService
{
    private readonly ILogger<AlertingService> _logger;
    private readonly IConfiguration _config;
    
    public async Task SendAlertAsync(Alert alert)
    {
        // Log alert
        _logger.Log(alert.Severity.ToLogLevel(), 
            "ALERT: {Title} - {Message}", alert.Title, alert.Message);
        
        // Send to notification channels based on severity
        switch (alert.Severity)
        {
            case AlertSeverity.Critical:
                await SendEmailAsync(alert);
                await SendSmsAsync(alert);
                await SendPushNotificationAsync(alert);
                await SendDiscordAlertAsync(alert);
                break;
                
            case AlertSeverity.Warning:
                await SendEmailAsync(alert);
                await SendDiscordAlertAsync(alert);
                break;
                
            case AlertSeverity.Info:
                await SendDiscordAlertAsync(alert);
                break;
        }
        
        // Store in database for audit
        await StoreAlertAsync(alert);
    }
    
    public async Task SendTradeNotificationAsync(Trade trade)
    {
        if (Math.Abs(trade.NetPnL) > SignificantTradeThreshold)
        {
            var emoji = trade.NetPnL > 0 ? "🟢" : "🔴";
            var alert = new Alert
            {
                Severity = trade.NetPnL > 0 ? AlertSeverity.Info : AlertSeverity.Warning,
                Title = $"{emoji} Trade Closed: {trade.Instrument}",
                Message = $"PnL: {trade.NetPnL:C} | Duration: {trade.Duration:hh\\:mm\\:ss}",
                Category = AlertCategory.Trading,
                Timestamp = DateTime.UtcNow
            };
            
            await SendAlertAsync(alert);
        }
    }
    
    private async Task SendDiscordAlertAsync(Alert alert)
    {
        var webhookUrl = _config["Alerting:DiscordWebhookUrl"];
        if (string.IsNullOrEmpty(webhookUrl))
            return;
            
        var color = alert.Severity switch
        {
            AlertSeverity.Critical => 0xFF0000,  // Red
            AlertSeverity.Warning => 0xFFA500,   // Orange
            AlertSeverity.Info => 0x00FF00,      // Green
            _ => 0x808080                         // Gray
        };
        
        var payload = new
        {
            embeds = new[]
            {
                new
                {
                    title = alert.Title,
                    description = alert.Message,
                    color = color,
                    timestamp = alert.Timestamp.ToString("O"),
                    footer = new { text = $"Instance: {_config["Application:InstanceId"]}" }
                }
            }
        };
        
        using var client = new HttpClient();
        await client.PostAsJsonAsync(webhookUrl, payload);
    }
}

public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}

public enum AlertCategory
{
    Trading,
    Risk,
    System,
    Connection,
    Performance
}
```

## Operational Procedures

### Startup Sequence

```csharp
public class TradingBotStartup : BackgroundService
{
    private readonly ILogger<TradingBotStartup> _logger;
    private readonly IServiceProvider _services;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Trading Bot...");
        
        try
        {
            // 1. Validate configuration
            await ValidateConfigurationAsync();
            
            // 2. Initialize database
            await InitializeDatabaseAsync();
            
            // 3. Load AI models
            await LoadAIModelsAsync();
            
            // 4. Connect to NinjaTrader
            await ConnectToNinjaTraderAsync(stoppingToken);
            
            // 5. Initialize risk engine
            await InitializeRiskEngineAsync();
            
            // 6. Start strategies
            await StartStrategiesAsync(stoppingToken);
            
            _logger.LogInformation("Trading Bot started successfully");
            
            // Keep running
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fatal error during startup");
            throw;
        }
        finally
        {
            await ShutdownAsync();
        }
    }
    
    private async Task ConnectToNinjaTraderAsync(CancellationToken ct)
    {
        var ntConnection = _services.GetRequiredService<INinjaTraderConnection>();
        var config = _services.GetRequiredService<IConfiguration>();
        
        _logger.LogInformation("Connecting to NinjaTrader...");
        
        var connectConfig = new ConnectionConfig
        {
            Host = config["NinjaTrader:Host"],
            Port = int.Parse(config["NinjaTrader:Port"]),
            ApiKey = config["NinjaTrader:ApiKey"],
            AccountName = config["NinjaTrader:AccountName"]
        };
        
        var result = await ntConnection.ConnectAsync(connectConfig);
        
        if (!result.Success)
        {
            throw new InvalidOperationException($"Failed to connect to NinjaTrader: {result.Error}");
        }
        
        _logger.LogInformation("Connected to NinjaTrader successfully");
    }
}
```

### Graceful Shutdown

```csharp
public async Task ShutdownAsync()
{
    _logger.LogInformation("Shutting down Trading Bot...");
    
    // 1. Stop accepting new signals
    await _strategyOrchestrator.StopAllAsync();
    
    // 2. Close open positions (optional, configurable)
    if (_config.GetValue<bool>("Trading:ClosePositionsOnShutdown"))
    {
        _logger.LogInformation("Closing all open positions...");
        await _positionManager.CloseAllPositionsAsync("System shutdown");
    }
    
    // 3. Cancel all working orders
    await _executionAdapter.CancelAllOrdersAsync();
    
    // 4. Wait for pending operations
    await Task.Delay(TimeSpan.FromSeconds(5));
    
    // 5. Disconnect from NinjaTrader
    await _ntConnection.DisconnectAsync();
    
    // 6. Flush logs
    await Log.CloseAndFlushAsync();
    
    _logger.LogInformation("Trading Bot shutdown complete");
}
```

### Backup and Recovery

```bash
#!/bin/bash
# backup.sh - Daily backup script

BACKUP_DIR="/backup/tradingbot/$(date +%Y%m%d)"
mkdir -p $BACKUP_DIR

# Backup database
pg_dump -h localhost -U tradingbot tradingbot > $BACKUP_DIR/database.sql

# Backup configuration
cp -r /opt/tradingbot/config $BACKUP_DIR/

# Backup models
cp -r /opt/tradingbot/models $BACKUP_DIR/

# Backup logs (last 7 days)
find /opt/tradingbot/logs -name "*.log" -mtime -7 -exec cp {} $BACKUP_DIR/logs/ \;

# Compress
 tar -czf $BACKUP_DIR.tar.gz $BACKUP_DIR
 
 # Upload to S3 (optional)
aws s3 cp $BACKUP_DIR.tar.gz s3://tradingbot-backups/

# Clean old backups
find /backup/tradingbot -name "*.tar.gz" -mtime +30 -delete
```

## Maintenance Windows

```csharp
public interface IMaintenanceService
{
    Task<bool> IsMaintenanceWindowAsync();
    Task ScheduleMaintenanceAsync(MaintenanceWindow window);
    Task EnterMaintenanceModeAsync();
    Task ExitMaintenanceModeAsync();
}

public class MaintenanceService : IMaintenanceService
{
    // Predefined maintenance windows (e.g., weekends, after market close)
    private readonly List<MaintenanceWindow> _scheduledWindows = new()
    {
        // Weekly maintenance: Saturday 00:00 - 06:00 UTC
        new MaintenanceWindow 
        { 
            DayOfWeek = DayOfWeek.Saturday, 
            StartTime = TimeSpan.Zero,
            Duration = TimeSpan.FromHours(6)
        }
    };
    
    public async Task EnterMaintenanceModeAsync()
    {
        _logger.LogWarning("Entering maintenance mode...");
        
        // Stop strategies
        await _strategyOrchestrator.StopAllAsync();
        
        // Cancel orders
        await _executionAdapter.CancelAllOrdersAsync();
        
        // Disconnect (optional)
        // await _ntConnection.DisconnectAsync();
        
        _maintenanceMode = true;
        
        await _alerting.SendAlertAsync(new Alert
        {
            Severity = AlertSeverity.Info,
            Title = "Maintenance Mode Active",
            Message = "Trading bot is in maintenance mode. No trading activity."
        });
    }
}
```
