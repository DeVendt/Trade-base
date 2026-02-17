# Windows Container Hosting for NinjaTrader

## Executive Summary

**The Solution:** Run NinjaTrader in a Windows Docker container with full DLL support.

**Why This Wins:**
- ✅ **Native Windows** - No Wine, no compatibility layers
- ✅ **Full NinjaTrader features** - All indicators, strategies, addons
- ✅ **NTDirect DLL** - Full API access for automation
- ✅ **Isolated** - Clean environment, easy to replicate
- ✅ **Scalable** - Run multiple NT instances per market
- ✅ **Cloud ready** - Azure, AWS, GCP all support Windows containers

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    WINDOWS HOST (Azure/AWS/GCP)                 │
│                                                                  │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │          Windows Container: NinjaTrader ES              │   │
│  │                                                          │   │
│  │  ┌─────────────────────────────────────────────────┐   │   │
│  │  │  NinjaTrader 8 Platform                         │   │   │
│  │  │  ├─ Charts, indicators, DOM                     │   │   │
│  │  │  ├─ Strategy analyzer                           │   │   │
│  │  │  └─ Market replay                               │   │   │
│  │  └─────────────────────────────────────────────────┘   │   │
│  │                          │                               │   │
│  │  ┌───────────────────────▼───────────────────────┐     │   │
│  │  │  NTDirect.dll (C# .NET)                       │     │   │
│  │  │  └─ Your automated strategy                   │     │   │
│  │  └───────────────────────┬───────────────────────┘     │   │
│  │                          │ gRPC/HTTP                    │   │
│  │  ┌───────────────────────▼───────────────────────┐     │   │
│  │  │  Bridge Service (.NET 8)                      │     │   │
│  │  │  └─ Exposes REST/gRPC API                     │     │   │
│  │  └───────────────────────┬───────────────────────┘     │   │
│  └──────────────────────────┼────────────────────────────┘   │
│                             │ Port 50051                      │
│  ┌──────────────────────────▼────────────────────────────┐   │
│  │           Linux Container (Optional)                   │   │
│  │  ┌───────────────────────────────────────────────┐   │   │
│  │  │  Trading Engine (.NET 8 on Linux)             │   │   │
│  │  │  ├─ Strategy logic                            │   │   │
│  │  │  ├─ Risk management                           │   │   │
│  │  │  └─ Discord notifications                     │   │   │
│  │  └───────────────────────────────────────────────┘   │   │
│  └────────────────────────────────────────────────────────┘   │
│                                                              │
│  ┌────────────────────────────────────────────────────────┐   │
│  │           Shared Services (Linux Containers)           │   │
│  │  ├─ PostgreSQL (analytics)                             │   │
│  │  ├─ Redis (state/cache)                                │   │
│  │  └─ Grafana (monitoring)                               │   │
│  └────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

**Alternative: All-in-Windows**
```
Windows Host
├── Container 1: NinjaTrader ES (with strategy)
├── Container 2: NinjaTrader NQ (with strategy)
├── Container 3: NinjaTrader YM (with strategy)
└── Shared: PostgreSQL + Redis (Windows containers)
```

---

## Windows Container Options

### Option 1: Azure Container Instances (ACI) - Recommended

```yaml
# Azure CLI deployment
az container create \
  --resource-group tradebase-rg \
  --name ninjatrader-es \
  --image mcr.microsoft.com/windows/servercore:ltsc2022 \
  --os-type Windows \
  --cpu 4 \
  --memory 8 \
  --ports 7497 50051 \
  --command-line "powershell -Command C:\\setup\\start-ninjatrader.ps1"
```

**Pros:**
- Fully managed, no VM maintenance
- Pay per second
- Auto-scaling
- Integrated with Azure services

**Cons:**
- More expensive than VMs for 24/7
- Limited to 4GB RAM per container (Windows)

**Cost:** ~$100-150/month per container

---

### Option 2: Azure VM with Docker

```yaml
# VM Specs
Size: Standard_D4s_v3 (4 vCPU, 16GB RAM)
OS: Windows Server 2022 Datacenter
Container Runtime: Docker Enterprise

# Run multiple containers on one VM
Container 1: NinjaTrader ES
Container 2: NinjaTrader NQ
Container 3: NinjaTrader YM
```

**Pros:**
- More cost effective for 24/7
- Can run multiple NT instances
- Full control over environment
- Can use RDP to access GUI if needed

**Cons:**
- Must manage VM (patches, updates)
- Slightly more complex setup

**Cost:** ~$150-200/month for VM (runs 3 markets)

---

### Option 3: AWS ECS with Windows

```yaml
# ECS Task Definition
family: ninjatrader-es
networkMode: awsvpc
requiresCompatibilities:
  - EC2  # Windows requires EC2, not Fargate
cpu: 2048
memory: 4096
containerDefinitions:
  - name: ninjatrader
    image: your-registry/ninjatrader-es:latest
    essential: true
    portMappings:
      - containerPort: 50051
        protocol: tcp
```

**Pros:**
- AWS ecosystem integration
- Can use spot instances (save 70%)
- Auto-scaling groups

**Cons:**
- Requires Windows EC2 instances
- More complex networking

**Cost:** ~$120-180/month with spot instances

---

### Option 4: Self-Hosted Windows Server

```yaml
Hardware:
  CPU: Intel i5/i7 or AMD Ryzen (4+ cores)
  RAM: 16-32GB
  Storage: 500GB SSD
  OS: Windows Server 2022 or Windows 11 Pro
  Network: Reliable internet (backup connection)

Location:
  - Home/office
  - Colocation datacenter
  - Trading-specific datacenter (Equinix, etc.)
```

**Pros:**
- Lowest cost long-term
- Full control
- No cloud dependencies
- Can access GUI anytime

**Cons:**
- You manage everything
- Power/internet outages
- Hardware failures

**Cost:** $0/month (existing hardware) or $50-100/month colocation

---

## Docker Implementation

### Windows Container Dockerfile

```dockerfile
# Dockerfile.ninjatrader
FROM mcr.microsoft.com/windows/servercore:ltsc2022

# Install .NET 8
ADD https://download.visualstudio.microsoft.com/download/pr/.../dotnet-runtime-8.0.x-win-x64.exe dotnet-installer.exe
RUN dotnet-installer.exe /quiet /install && del dotnet-installer.exe

# Install NinjaTrader dependencies
RUN powershell -Command \
    Install-WindowsFeature -Name Net-Framework-Core ; \
    Install-WindowsFeature -Name Net-Framework-45-Core

# Copy NinjaTrader installation
COPY NinjaTrader8/ C:/NinjaTrader8/

# Copy your strategy DLL
COPY TradeBase.Strategy.dll C:/NinjaTrader8/bin/Custom/
COPY TradeBase.Strategy.dll C:/NinjaTrader8/bin/Strategy/

# Copy bridge service
COPY TradeBase.Bridge/ C:/Bridge/

# Copy startup script
COPY start.ps1 C:/start.ps1

# Expose ports
EXPOSE 7497 50051

# Start script
CMD ["powershell", "-File", "C:/start.ps1"]
```

### Startup Script

```powershell
# start.ps1

Write-Host "🏴‍☠️ Starting TradeBase NinjaTrader Container..."

# 1. Start NinjaTrader in headless mode
$ntProcess = Start-Process -FilePath "C:\NinjaTrader8\bin\NinjaTrader.exe" `
    -ArgumentList @("/nologo", "/minimized") `
    -PassThru

# Wait for NT to initialize
Write-Host "Waiting for NinjaTrader to initialize..."
Start-Sleep -Seconds 30

# 2. Start Bridge Service (connects NT to external world)
Write-Host "Starting Bridge Service..."
$bridgeProcess = Start-Process -FilePath "dotnet" `
    -ArgumentPath "C:\Bridge\TradeBase.Bridge.dll" `
    -PassThru

# 3. Keep container running
while ($true) {
    Start-Sleep -Seconds 10
    
    # Check if processes are alive
    if ($ntProcess.HasExited) {
        Write-Error "NinjaTrader has exited! Restarting..."
        $ntProcess = Start-Process -FilePath "C:\NinjaTrader8\bin\NinjaTrader.exe" -PassThru
    }
    
    if ($bridgeProcess.HasExited) {
        Write-Error "Bridge has exited! Restarting..."
        $bridgeProcess = Start-Process -FilePath "dotnet" -ArgumentPath "C:\Bridge\TradeBase.Bridge.dll" -PassThru
    }
}
```

---

## NTDirect DLL Integration

### C# Strategy for NinjaTrader

```csharp
// TradeBaseStrategy.cs
// Compile as NinjaTrader strategy DLL

namespace NinjaTrader.NinjaScript.Strategies
{
    public class TradeBaseStrategy : Strategy
    {
        private TradeBaseBridge _bridge;
        private bool _isConnected;
        
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "TradeBase Automated Strategy";
                Name = "TradeBaseStrategy";
                Calculate = Calculate.OnBarClose;
                EntriesPerDirection = 1;
                EntryHandling = EntryHandling.AllEntries;
                IsExitOnSessionCloseStrategy = true;
                ExitOnSessionCloseSeconds = 30;
                IsFillLimitOnTouch = false;
                MaximumBarsLookBack = MaximumBarsLookBack.TwoHundredFiftySix;
                OrderFillResolution = OrderFillResolution.Standard;
                Slippage = 0;
                StartBehavior = StartBehavior.WaitUntilFlat;
                TimeInForce = TimeInForce.Gtc;
                TraceOrders = false;
                RealtimeErrorHandling = RealtimeErrorHandling.StopCancelClose;
                StopTargetHandling = StopTargetHandling.PerEntryExecution;
                BarsRequiredToTrade = 20;
            }
            else if (State == State.Configure)
            {
                // Add data series
                AddDataSeries(Data.BarsPeriodType.Minute, 1);
                AddDataSeries(Data.BarsPeriodType.Minute, 5);
                
                // Set properties from config
                DefaultQuantity = 1;
            }
            else if (State == State.DataLoaded)
            {
                // Initialize bridge connection
                _bridge = new TradeBaseBridge("localhost", 50051);
                _bridge.Connect();
                _isConnected = true;
                
                // Subscribe to external commands
                _bridge.OnEntrySignal += OnExternalEntry;
                _bridge.OnExitSignal += OnExternalExit;
            }
            else if (State == State.Terminated)
            {
                _bridge?.Disconnect();
            }
        }

        protected override void OnBarUpdate()
        {
            if (!_isConnected) return;
            
            // Send market data to bridge
            _bridge.SendBarData(new BarData
            {
                Timestamp = Time[0],
                Open = Open[0],
                High = High[0],
                Low = Low[0],
                Close = Close[0],
                Volume = Volume[0]
            });
            
            // Check for signals from external AI/strategy
            if (_bridge.HasEntrySignal)
            {
                var signal = _bridge.GetEntrySignal();
                if (signal.Direction == TradeDirection.Long)
                {
                    EnterLong(signal.Quantity, signal.SignalId);
                }
                else
                {
                    EnterShort(signal.Quantity, signal.SignalId);
                }
            }
            
            if (_bridge.HasExitSignal)
            {
                ExitLong();
                ExitShort();
            }
        }
        
        protected override void OnOrderUpdate(Order order, double limitPrice, double stopPrice, 
            int quantity, int filled, double averageFillPrice, OrderState orderState, DateTime time, ErrorCode error, string nativeError)
        {
            // Send order updates to bridge
            _bridge.SendOrderUpdate(new OrderUpdate
            {
                OrderId = order.OrderId,
                State = orderState.ToString(),
                FilledQuantity = filled,
                AverageFillPrice = averageFillPrice
            });
        }
        
        protected override void OnExecutionUpdate(Execution execution, string executionId, 
            double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
        {
            // Send execution to bridge
            _bridge.SendExecution(new ExecutionData
            {
                ExecutionId = executionId,
                Price = price,
                Quantity = quantity,
                MarketPosition = marketPosition.ToString(),
                OrderId = orderId
            });
        }
        
        protected override void OnPositionUpdate(Position position, double averagePrice, 
            int quantity, MarketPosition marketPosition)
        {
            // Send position update to bridge
            _bridge.SendPosition(new PositionData
            {
                AveragePrice = averagePrice,
                Quantity = quantity,
                MarketPosition = marketPosition.ToString()
            });
        }
        
        private void OnExternalEntry(object sender, EntrySignalEventArgs e)
        {
            // Called when external system sends entry signal
            if (e.Direction == TradeDirection.Long && Position.MarketPosition != MarketPosition.Long)
            {
                EnterLong(e.Quantity, e.SignalId);
            }
            else if (e.Direction == TradeDirection.Short && Position.MarketPosition != MarketPosition.Short)
            {
                EnterShort(e.Quantity, e.SignalId);
            }
        }
        
        private void OnExternalExit(object sender, ExitSignalEventArgs e)
        {
            // Called when external system sends exit signal
            ExitLong();
            ExitShort();
        }
    }
}
```

### Bridge Communication Layer

```csharp
// TradeBaseBridge.cs
// Runs inside NinjaTrader container

using Grpc.Core;
using System;
using System.Threading.Tasks;

public class TradeBaseBridge
{
    private readonly string _host;
    private readonly int _port;
    private Server _grpcServer;
    
    public bool HasEntrySignal { get; private set; }
    public bool HasExitSignal { get; private set; }
    
    public event EventHandler<EntrySignalEventArgs> OnEntrySignal;
    public event EventHandler<ExitSignalEventArgs> OnExitSignal;
    
    public TradeBaseBridge(string host, int port)
    {
        _host = host;
        _port = port;
    }
    
    public void Connect()
    {
        // Start gRPC server to receive commands from external system
        _grpcServer = new Server
        {
            Services = { TradeBaseGrpc.BindService(new TradeBaseServiceImpl(this)) },
            Ports = { new ServerPort(_host, _port, ServerCredentials.Insecure) }
        };
        _grpcServer.Start();
    }
    
    public void Disconnect()
    {
        _grpcServer?.ShutdownAsync().Wait();
    }
    
    // Called by NinjaTrader to send data out
    public void SendBarData(BarData bar) { /* ... */ }
    public void SendOrderUpdate(OrderUpdate update) { /* ... */ }
    public void SendExecution(ExecutionData execution) { /* ... */ }
    public void SendPosition(PositionData position) { /* ... */ }
    
    // Called by gRPC service when external system sends command
    public void TriggerEntry(EntrySignal signal)
    {
        OnEntrySignal?.Invoke(this, new EntrySignalEventArgs(signal));
    }
    
    public void TriggerExit()
    {
        OnExitSignal?.Invoke(this, new ExitSignalEventArgs());
    }
}
```

---

## Multi-Market Deployment

### Docker Compose (Windows Containers)

```yaml
# docker-compose.windows.yml
version: '2.4'  # Windows containers require 2.x

services:
  # ES Market Instance
  ninjatrader-es:
    build:
      context: ./NinjaTrader
      dockerfile: Dockerfile.ninjatrader
    environment:
      - SYMBOL=ES
      - ACCOUNT=Sim101
      - RISK_PER_TRADE=1.0
    ports:
      - "50051:50051"
    volumes:
      - nt-es-data:C:/NinjaTrader8/db
    networks:
      - trading-network
    restart: unless-stopped

  # NQ Market Instance
  ninjatrader-nq:
    build:
      context: ./NinjaTrader
      dockerfile: Dockerfile.ninjatrader
    environment:
      - SYMBOL=NQ
      - ACCOUNT=Sim101
      - RISK_PER_TRADE=1.0
    ports:
      - "50052:50051"
    volumes:
      - nt-nq-data:C:/NinjaTrader8/db
    networks:
      - trading-network
    restart: unless-stopped

  # YM Market Instance
  ninjatrader-ym:
    build:
      context: ./NinjaTrader
      dockerfile: Dockerfile.ninjatrader
    environment:
      - SYMBOL=YM
      - ACCOUNT=Sim101
      - RISK_PER_TRADE=1.0
    ports:
      - "50053:50051"
    volumes:
      - nt-ym-data:C:/NinjaTrader8/db
    networks:
      - trading-network
    restart: unless-stopped

  # Trading Engine (can be Windows or Linux)
  trading-engine:
    build:
      context: ./src/TradeBase
      dockerfile: Dockerfile
    environment:
      - ES_BRIDGE_HOST=ninjatrader-es
      - NQ_BRIDGE_HOST=ninjatrader-nq
      - YM_BRIDGE_HOST=ninjatrader-ym
      - DISCORD_WEBHOOK_URL=${DISCORD_WEBHOOK_URL}
    depends_on:
      - ninjatrader-es
      - ninjatrader-nq
      - ninjatrader-ym
      - postgres
      - redis
    networks:
      - trading-network

  # Shared PostgreSQL (Windows container)
  postgres:
    image: mcr.microsoft.com/windows/servercore:ltsc2022
    command: powershell -Command "C:/postgres/start.ps1"
    volumes:
      - postgres-data:C:/postgres/data
    networks:
      - trading-network

  # Shared Redis (Windows container)
  redis:
    image: mcr.microsoft.com/windows/servercore:ltsc2022
    command: powershell -Command "C:/redis/redis-server.exe"
    volumes:
      - redis-data:C:/redis/data
    networks:
      - trading-network

networks:
  trading-network:
    driver: nat

volumes:
  nt-es-data:
  nt-nq-data:
  nt-ym-data:
  postgres-data:
  redis-data:
```

---

## Cloud Deployment Guides

### Azure Deployment

```powershell
# Deploy to Azure Container Instances

# 1. Login
az login

# 2. Create resource group
az group create --name tradebase-rg --location eastus

# 3. Create container
az container create `
  --resource-group tradebase-rg `
  --name ninjatrader-es `
  --image yourregistry.azurecr.io/ninjatrader-es:latest `
  --os-type Windows `
  --cpu 4 `
  --memory 8 `
  --registry-login-server yourregistry.azurecr.io `
  --registry-username $env:REGISTRY_USERNAME `
  --registry-password $env:REGISTRY_PASSWORD `
  --environment-variables `
    SYMBOL=ES `
    ACCOUNT=Sim101 `
  --ports 50051

# 4. Get logs
az container logs --resource-group tradebase-rg --name ninjatrader-es

# 5. Delete when done
az container delete --resource-group tradebase-rg --name ninjatrader-es
```

### AWS Deployment

```powershell
# Deploy to AWS ECS with Windows

# 1. Create ECS cluster with Windows EC2 instances
aws ecs create-cluster --cluster-name tradebase-windows

# 2. Register Windows EC2 instances
# Use: Windows_Server-2022-English-Full-ECS_Optimized AMI

# 3. Create task definition
aws ecs register-task-definition --cli-input-json file://task-definition.json

# 4. Run task
aws ecs run-task `
  --cluster tradebase-windows `
  --task-definition ninjatrader-es `
  --count 1
```

---

## Cost Analysis

### Monthly Costs Comparison

| Solution | Setup | Monthly | Best For |
|----------|-------|---------|----------|
| **Azure VM (4core/16GB)** | Easy | $150-200 | Production, 24/7 |
| **Azure Container Instances** | Easy | $300-400 | Testing, short runs |
| **AWS EC2 Windows** | Medium | $140-180 | Production, spot savings |
| **Self-hosted (colo)** | Hard | $50-100 | Cost-conscious |
| **Home/Office PC** | Easy | $0 | Development |

### Recommended: Azure VM Approach

```yaml
VM: Standard_D4s_v3 (4 vCPU, 16GB RAM)
OS: Windows Server 2022
Cost: ~$150/month
Can run: 3-4 NinjaTrader instances
Best value for 24/7 trading
```

---

## Security Best Practices

### Windows Container Security

```powershell
# Run as non-admin (if possible)
USER ContainerUser

# Disable unnecessary Windows features
RUN dism /online /disable-feature /featurename:WindowsMediaPlayer

# Use read-only volumes where possible
volumes:
  - type: bind
    source: ./config
    target: C:/config
    read_only: true
```

### Network Security

```yaml
# Only expose necessary ports
ports:
  - "50051:50051"  # Bridge API
# Do NOT expose RDP (3389) to internet

# Use Azure/AWS security groups
Inbound:
  - Allow: 50051 from TradingEngine only
  - Deny: All other
Outbound:
  - Allow: 443 (HTTPS) - broker connection
  - Allow: 80 (HTTP) - updates
```

### Secrets Management

```powershell
# Azure Key Vault
$secret = Get-AzKeyVaultSecret -VaultName "tradebase-vault" -Name "NT-License"

# AWS Secrets Manager
$secret = Get-SECSecretValue -SecretId "tradebase/nt-license"

# Never hardcode credentials in Dockerfile!
```

---

## Monitoring & Alerting

### Windows Container Health Checks

```powershell
# healthcheck.ps1
param(
    [string]$BridgeUrl = "http://localhost:50051/health"
)

try {
    $response = Invoke-RestMethod -Uri $BridgeUrl -TimeoutSec 5
    if ($response.status -eq "healthy") {
        exit 0  # Healthy
    }
} catch {
    # Check if NinjaTrader is running
    $nt = Get-Process "NinjaTrader" -ErrorAction SilentlyContinue
    if (-not $nt) {
        # Restart NinjaTrader
        Start-Process "C:\NinjaTrader8\bin\NinjaTrader.exe"
    }
    exit 1  # Unhealthy
}
```

### Prometheus Metrics

```csharp
// Expose metrics from bridge
public class MetricsService
{
    private readonly Gauge _positionSize = Metrics.CreateGauge(
        "tradebase_position_size", "Current position size");
    
    private readonly Counter _ordersFilled = Metrics.CreateCounter(
        "tradebase_orders_filled_total", "Total orders filled");
    
    private readonly Histogram _latency = Metrics.CreateHistogram(
        "tradebase_order_latency_seconds", "Order fill latency");
}
```

---

## Troubleshooting

### Common Issues

```powershell
# Issue: Container exits immediately
# Fix: Check logs
docker logs ninjatrader-es

# Issue: NinjaTrader won't start
# Fix: Check .NET Framework is installed
docker exec -it ninjatrader-es powershell
Get-WindowsFeature -Name Net-Framework*

# Issue: Bridge can't connect
# Fix: Check port binding
docker port ninjatrader-es

# Issue: High memory usage
# Fix: Limit container memory
docker run -m 8g --memory-swap 8g ninjatrader-es

# Issue: Time sync problems
# Fix: Sync container time with host
docker run -v /etc/localtime:/etc/localtime:ro ninjatrader-es
```

---

## Summary

### Why Windows Containers for NinjaTrader:

✅ **Native performance** - No emulation  
✅ **Full feature set** - All NT indicators/strategies  
✅ **Easy deployment** - Docker standardization  
✅ **Scalable** - Run multiple markets per host  
✅ **Cloud ready** - Azure, AWS, GCP support  
✅ **Isolated** - Clean, reproducible environments  

### Recommended Setup:

```yaml
For Production:
  - Azure VM (Standard_D4s_v3)
  - Windows Server 2022
  - Docker Desktop Enterprise
  - 3-4 NinjaTrader containers
  - PostgreSQL + Redis containers
  - Cost: ~$150-200/month

For Development:
  - Local Windows PC
  - Docker Desktop
  - 1-2 NinjaTrader containers
  - Cost: $0
```

---

## Next Steps

1. **Choose cloud provider** (Azure recommended)
2. **Create Windows VM** or set up ACI
3. **Build NinjaTrader container**
4. **Deploy and test** with Sim101
5. **Scale** to multiple markets

Ready to build this out, Cap'n? 🏴‍☠️🪟
