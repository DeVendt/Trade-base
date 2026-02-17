# Linux Futures Trading with Rithmic + Apex Trader Funding

## Executive Summary

**Good News:** Rithmic is more Linux-friendly than NinjaTrader, but still requires Windows for the core connection.

**The Path Forward:**
1. Use **Docker with Windows containers** OR **Wine** for Rithmic connection
2. Run the trading logic on native Linux
3. Use **Apex Trader Funding** (they use Rithmic as the backend)

---

## Architecture Options

### Option 1: Docker Windows Container (Recommended)

```
┌─────────────────────────────────────────────────────────────┐
│                    LINUX HOST (Ubuntu)                       │
│  ┌─────────────────────────────────────────────────────┐   │
│  │           TRADING LOGIC CONTAINER (Linux)           │   │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  │   │
│  │  │  Strategy   │  │  Risk Mgr   │  │  AI Models  │  │   │
│  │  │   Engine    │  │             │  │             │  │   │
│  │  └──────┬──────┘  └─────────────┘  └─────────────┘  │   │
│  │         │                                           │   │
│  │         ▼ gRPC/WebSocket                            │   │
│  │  ┌─────────────────────────────────────────────┐   │   │
│  │  │      Rithmic Bridge (cross-platform)        │   │   │
│  │  └─────────────────────────────────────────────┘   │   │
│  └──────────────────────┬──────────────────────────────┘   │
│                         │ TCP/IPC                          │
│  ┌──────────────────────▼──────────────────────────────┐   │
│  │         RITHMIC CONNECTOR (Windows Container)        │   │
│  │  ┌─────────────────────────────────────────────┐    │   │
│  │  │  Rithmic .NET API (R|API+)                  │    │   │
│  │  │  └─> Rithmic servers (Chicago)              │    │   │
│  │  └─────────────────────────────────────────────┘    │   │
│  └─────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

**Pros:**
- Clean separation of concerns
- Linux trading logic stays native
- Rithmic runs in isolated Windows environment
- Can run on cloud VPS (AWS, DigitalOcean, etc.)

**Cons:**
- Requires Docker Enterprise or Windows host for Windows containers
- More complex setup

---

### Option 2: Wine Compatibility Layer

```
┌─────────────────────────────────────────────────────────────┐
│                    LINUX HOST (Ubuntu)                       │
│  ┌─────────────────────────────────────────────────────┐   │
│  │              WINE COMPATIBILITY LAYER                │   │
│  │  ┌─────────────────────────────────────────────┐    │   │
│  │  │  Rithmic Trader Pro / R|API+ .NET           │    │   │
│  │  │  (running via Wine)                         │    │   │
│  │  └─────────────────────────────────────────────┘    │   │
│  └────────────────────────┬────────────────────────────┘   │
│                           │                                  │
│  ┌────────────────────────▼────────────────────────────┐   │
│  │              NATIVE LINUX TRADING APP               │   │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  │   │
│  │  │  Strategy   │  │  Risk Mgr   │  │  Discord    │  │   │
│  │  │   Engine    │  │             │  │  Notifier   │  │   │
│  │  └─────────────┘  └─────────────┘  └─────────────┘  │   │
│  └─────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

**Pros:**
- Runs entirely on Linux
- No Windows license needed
- Simpler deployment

**Cons:**
- Wine can be unstable with .NET apps
- Rithmic updates may break compatibility
- Limited support from Rithmic

---

### Option 3: Hybrid Cloud (Best for Production)

```
┌─────────────────────────────────────────────────────────────┐
│                   CLOUD SETUP                                │
│                                                              │
│  ┌────────────────────────┐  ┌──────────────────────────┐   │
│  │   LINUX VPS (Ubuntu)   │  │   WINDOWS VPS (Azure)    │   │
│  │  ┌──────────────────┐  │  │  ┌────────────────────┐  │   │
│  │  │  Trading Engine  │  │  │  │  Rithmic Gateway   │  │   │
│  │  │  - Strategies    │──┼──┼─>│  - R|API+          │  │   │
│  │  │  - AI Models     │  │  │  │  - Order routing   │  │   │
│  │  │  - Risk Mgmt     │  │  │  └────────────────────┘  │   │
│  │  └──────────────────┘  │  │           │              │   │
│  │           │            │  │           ▼              │   │
│  │           ▼            │  │    ┌──────────────┐      │   │
│  │    ┌────────────┐      │  │    │  Apex/Rithmic│      │   │
│  │    │  Database  │      │  │    │  Servers     │      │   │
│  │    │  (Redis/   │      │  │    └──────────────┘      │   │
│  │    │   Postgres)│      │  │                          │   │
│  │    └────────────┘      │  └──────────────────────────┘   │
│  └────────────────────────┘                                 │
└─────────────────────────────────────────────────────────────┘
```

**Pros:**
- Most reliable option
- Can use cheapest Linux VPS for logic
- Only pay for small Windows VM for Rithmic
- Easy to scale

**Cons:**
- Requires two servers
- Network latency between them (minimal if same datacenter)

---

## Rithmic + Apex Specifics

### What is Rithmic?

Rithmic provides:
- **Market Data:** Real-time Level 1 & 2 for CME, CBOT, NYMEX, COMEX
- **Order Execution:** Direct market access (DMA)
- **APIs:** R|API+ (.NET), R|API (C++), R|Trader Pro (GUI)

### What is Apex Trader Funding?

Apex is a **prop firm** that:
- Gives you a funded account after passing an evaluation
- Uses **Rithmic** as their broker/data provider
- Offers accounts from $25K to $300K
- Keeps 90% of profits (you get 10%... wait, no - they keep 10%, you get 90%!)

### Connection Flow

```
Your App ──> Rithmic R|API+ ──> Rithmic Servers ──> CME/Apex
                │
                └─> Requires Windows (officially)
```

---

## Implementation Roadmap

### Phase 1: Development Environment (This Week)

**Option A: Wine Setup**
```bash
# Install Wine
sudo dpkg --add-architecture i386
wget -nc https://dl.winehq.org/wine-builds/winehq.key
sudo apt-key add winehq.key
sudo apt-add-repository 'deb https://dl.winehq.org/wine-builds/ubuntu/ jammy main'
sudo apt update
sudo apt install --install-recommends winehq-stable

# Install .NET 8 for Wine
wget https://download.visualstudio.microsoft.com/download/pr/.../dotnet-runtime-8.0.x-win-x64.exe
wine dotnet-runtime-8.0.x-win-x64.exe
```

**Option B: Docker Windows Container**
```bash
# Requires Windows host or Docker Desktop with Windows containers
# On Linux host, use this workaround:
docker run -it --rm mcr.microsoft.com/dotnet/framework/runtime:4.8-windowsservercore-ltsc2019
```

### Phase 2: Rithmic Bridge

Create a bridge that runs in Windows but exposes a cross-platform API:

```csharp
// RithmicBridge/Program.cs (runs on Windows/Wine)
// Exposes gRPC or WebSocket for Linux clients

public class RithmicBridgeService
{
    private IRithmicConnection _rithmic;
    private IGrpcServer _grpcServer;
    
    public async Task StartAsync()
    { 
        // Connect to Rithmic
        _rithmic = await RithmicConnection.ConnectAsync(
            username: Environment.GetEnvironmentVariable("RITHMIC_USERNAME"),
            password: Environment.GetEnvironmentVariable("RITHMIC_PASSWORD"),
            server: "Rithmic-Chicago" // or "Rithmic-Apex"
        );
        
        // Start gRPC server for Linux clients
        _grpcServer = new GrpcServer(50051);
        _grpcServer.RegisterService(new MarketDataService(_rithmic));
        _grpcServer.RegisterService(new OrderService(_rithmic));
        await _grpcServer.StartAsync();
    }
}
```

### Phase 3: Linux Trading Engine

```csharp
// TradeBase/Connection/RithmicGrpcClient.cs (runs on Linux)

public class RithmicGrpcClient : IMarketDataSubscriber, IOrderExecutor
{
    private readonly MarketData.MarketDataClient _marketDataClient;
    private readonly Orders.OrdersClient _ordersClient;
    
    public RithmicGrpcClient(string bridgeHost = "localhost", int port = 50051)
    {
        var channel = GrpcChannel.ForAddress($"http://{bridgeHost}:{port}");
        _marketDataClient = new MarketData.MarketDataClient(channel);
        _ordersClient = new Orders.OrdersClient(channel);
    }
    
    // Implement IMarketDataSubscriber and IOrderExecutor
    // All calls go to the Windows bridge via gRPC
}
```

---

## Project Structure for Rithmic/Apex

```
TradeBase/
├── src/
│   ├── Core/                    # Domain models (same as before)
│   ├── RithmicAdapter/          # Rithmic-specific adapter
│   │   ├── Bridge/              # gRPC bridge (Windows/Wine)
│   │   │   ├── RithmicBridge.csproj
│   │   │   └── Program.cs       # Bridge entry point
│   │   └── Client/              # Linux gRPC client
│   │       ├── RithmicGrpcClient.cs
│   │       └── RithmicClient.csproj
│   ├── Strategies/              # Same strategy engine
│   ├── RiskManagement/          # Position sizing, stops
│   └── TradeBase/               # Main app
├── scripts/
│   ├── setup_wine.sh            # Wine installation
│   ├── setup_bridge.sh          # Bridge setup
│   └── deploy.sh                # Deployment script
├── docker/
│   ├── Dockerfile.bridge        # Windows bridge container
│   ├── Dockerfile.trading       # Linux trading container
│   └── docker-compose.yml       # Full stack
├── docs/
│   └── RITHMIC_SETUP.md         # This document
└── config/
    ├── rithmic.dev.json         # Dev credentials (gitignored)
    └── rithmic.prod.json        # Prod credentials (gitignored)
```

---

## Wine Setup Script

```bash
#!/bin/bash
# scripts/setup_wine.sh

set -e

echo "🍷 Setting up Wine for Rithmic on Ubuntu..."

# Update and install dependencies
sudo apt update
sudo apt install -y wget gnupg2 software-properties-common

# Add WineHQ repository
sudo dpkg --add-architecture i386
wget -nc https://dl.winehq.org/wine-builds/winehq.key
sudo apt-key add winehq.key
sudo apt-add-repository "deb https://dl.winehq.org/wine-builds/ubuntu/ $(lsb_release -cs) main"

# Install Wine
sudo apt update
sudo apt install -y --install-recommends winehq-stable

# Install Winetricks
wget https://raw.githubusercontent.com/Winetricks/winetricks/master/src/winetricks
chmod +x winetricks
sudo mv winetricks /usr/local/bin/

# Setup Wine prefix for Rithmic
export WINEPREFIX="$HOME/.wine-rithmic"
export WINEARCH=win64
winecfg

# Install .NET 8 runtime via Wine
winetricks -q dotnet80

# Install core fonts
winetricks -q corefonts

echo "✅ Wine setup complete!"
echo ""
echo "Next steps:"
echo "1. Download Rithmic R|Trader Pro or R|API+"
echo "2. Install: wine RithmicInstaller.exe"
echo "3. Configure credentials in config/rithmic.dev.json"
```

---

## Docker Compose Setup

```yaml
# docker/docker-compose.yml
version: '3.8'

services:
  rithmic-bridge:
    build:
      context: ../src/RithmicAdapter/Bridge
      dockerfile: ../../../docker/Dockerfile.bridge
    environment:
      - RITHMIC_USERNAME=${RITHMIC_USERNAME}
      - RITHMIC_PASSWORD=${RITHMIC_PASSWORD}
      - RITHMIC_SERVER=${RITHMIC_SERVER:-Rithmic-Apex}
    ports:
      - "50051:50051"
    networks:
      - trading-network
    restart: unless-stopped

  trading-engine:
    build:
      context: ../src/TradeBase
      dockerfile: ../../docker/Dockerfile.trading
    environment:
      - RITHMIC_BRIDGE_HOST=rithmic-bridge
      - RITHMIC_BRIDGE_PORT=50051
      - TRADING_MODE=${TRADING_MODE:-PAPER}
      - DISCORD_WEBHOOK_URL=${DISCORD_WEBHOOK_URL}
    depends_on:
      - rithmic-bridge
      - redis
      - postgres
    networks:
      - trading-network
    restart: unless-stopped

  redis:
    image: redis:7-alpine
    networks:
      - trading-network

  postgres:
    image: postgres:15-alpine
    environment:
      - POSTGRES_USER=tradebase
      - POSTGRES_PASSWORD=${DB_PASSWORD}
      - POSTGRES_DB=tradebase
    volumes:
      - postgres-data:/var/lib/postgresql/data
    networks:
      - trading-network

networks:
  trading-network:
    driver: bridge

volumes:
  postgres-data:
```

---

## Apex Trader Funding Account Setup

### Step 1: Get Eval Account
1. Go to https://apextraderfunding.com/
2. Choose evaluation size ($25K to $300K)
3. Pay evaluation fee ($50-$300 depending on size)
4. Pass the eval (trade for 10 days, hit profit target, don't hit max loss)

### Step 2: Get Rithmic Credentials
After passing, Apex emails you:
- Username: `APEX_YourName_12345`
- Password: (temporary, change on first login)
- Server: `Rithmic-Apex` (or similar)

### Step 3: Configure App
```json
// config/rithmic.json
{
  "Rithmic": {
    "Username": "APEX_YourName_12345",
    "Password": "${RITHMIC_PASSWORD}",
    "Server": "Rithmic-Apex",
    "Environment": "Live"
  },
  "Trading": {
    "Account": "APEX_YourName_12345",
    "Mode": "LIVE",
    "MaxDailyLoss": 500.00,
    "MaxPositionSize": 5
  }
}
```

---

## Comparison: Rithmic vs NinjaTrader

| Feature | Rithmic + Apex | NinjaTrader |
|---------|----------------|-------------|
| **Linux Support** | Via Wine/Docker ❓ | No ❌ |
| **Prop Firm** | Apex, Bulenox, etc. | Some (less common) |
| **API** | R\|API+ (.NET), C++ | NTDirect (.NET) |
| **Data Feed** | Direct from CME | Via Rithmic/others |
| **Fees** | Exchange fees only | Platform + exchange |
| **Latency** | Very low | Higher (extra layer) |
| **Cost** | Free API | $99+/month platform |

**Winner for Linux:** Rithmic (can hack with Wine)
**Winner for Features:** NinjaTrader (more built-in tools)

---

## Recommended Architecture for Your Setup

Since you're on Ubuntu and want to use Apex (Rithmic):

### Development (Your Current Machine)
```
┌─────────────────────────────────────────────────┐
│              Your Ubuntu Laptop                 │
│  ┌─────────────────────────────────────────┐   │
│  │  Wine + Rithmic Bridge (R|API+)        │   │
│  │  └─> Exposes gRPC on localhost:50051   │   │
│  └─────────────────────────────────────────┘   │
│                    │                            │
│  ┌─────────────────▼───────────────────────┐   │
│  │  Native Trading Engine (.NET on Linux)  │   │
│  │  ├─ Connects via gRPC to bridge         │   │
│  │  ├─ Strategies, Risk Mgmt, AI           │   │
│  │  └─ Discord notifications               │   │
│  └─────────────────────────────────────────┘   │
└─────────────────────────────────────────────────┘
```

### Production (Cloud VPS)
```
┌──────────────────────────┐  ┌──────────────────────────┐
│   Cheap Linux VPS        │  │   Small Windows VPS      │
│   ($5-10/month)          │  │   ($20-30/month)         │
│  ┌──────────────────┐    │  │  ┌──────────────────┐    │
│  │ Trading Engine   │────┼──┼─>│ Rithmic Bridge   │    │
│  │ - Full logic     │    │  │  └──────────────────┘    │
│  │ - AI models      │    │  │           │              │
│  └──────────────────┘    │  │           ▼              │
│           │              │  │    ┌──────────────┐      │
│           ▼              │  │    │ Apex/Rithmic │      │
│    ┌────────────┐        │  │    └──────────────┘      │
│    │ Database   │        │  │                          │
│    └────────────┘        │  └──────────────────────────┘
└──────────────────────────┘
```

---

## Next Steps

1. **Choose your path:**
   - Option A: Wine setup (cheaper, less reliable)
   - Option B: Two VPS setup (more expensive, very reliable)

2. **Get Apex eval account** ($50-300)

3. **I can implement:**
   - Rithmic bridge with gRPC
   - Linux-native Rithmic client
   - Docker compose setup
   - Wine automation scripts

4. **Timeline:**
   - Wine setup: 1-2 days
   - Bridge implementation: 2-3 days
   - Testing with Apex eval: 1 week

---

## Questions for the Captain

1. **Budget preference?**
   - Wine (free but less stable)
   - Two VPS (~$35/month but rock solid)

2. **Apex account size?**
   - $25K eval ($50 fee)
   - $50K eval ($100 fee)
   - $100K+ eval ($200-300 fee)

3. **Symbols to trade?**
   - ES only?
   - ES + NQ + YM?
   - Other futures?

4. **Priority?**
   - Get trading ASAP (Wine)
   - Build proper infra (Docker/VPS)

Ready to set sail with Rithmic, Cap'n? 🏴‍☠️
