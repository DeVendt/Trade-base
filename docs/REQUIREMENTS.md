# System Requirements

## Overview

TradeBase is designed to run on **Linux-native** infrastructure with zero Windows dependencies. Each trading instance runs a single market strategy in its own container.

---

## Hardware Requirements

### Minimum System

```yaml
CPU: 2 cores (x86_64 or ARM64)
RAM: 4 GB
Storage: 20 GB SSD
Network: 10 Mbps (stable connection)
OS: Ubuntu 20.04+, Debian 11+, CentOS 8+, or any Linux with Docker
```

### Per Trading Instance

```yaml
CPU: 1 core
RAM: 2 GB
Storage: 10 GB
Network: 5 Mbps
```

### Recommended for Production

```yaml
CPU: 4+ cores
RAM: 8+ GB
Storage: 50+ GB SSD
Network: 100 Mbps (low latency to broker)
OS: Ubuntu 22.04 LTS (recommended)
```

---

## Software Requirements

### Core Runtime

```yaml
.NET: 8.0 SDK or Runtime
Docker: 20.10+
Docker Compose: 2.0+
Git: 2.30+
```

### For Interactive Brokers (IBKR)

```yaml
Java: OpenJDK 17+ (for IB Gateway)
Xvfb: For headless GUI support
```

### For Tradovate

```yaml
# No additional requirements - pure HTTP/REST API
```

### Database (Optional but Recommended)

```yaml
PostgreSQL: 15+ (for trade analytics)
Redis: 7+ (for state/cache)
```

---

## Network Requirements

### Outbound Ports

```yaml
# HTTPS API calls
443/tcp: All brokers (REST/WebSocket APIs)

# Interactive Brokers (local only)
7497/tcp: IB Gateway API
7496/tcp: IB TWS API (alternative)

# WebSocket Market Data
80/tcp: Some brokers (fallback)
```

### No Inbound Ports Required

All connections are outbound-only. No port forwarding needed.

### Latency Requirements

```yaml
Development: < 200ms to broker
Production: < 50ms to broker
High Frequency: < 10ms to broker
```

---

## Broker Support Matrix

| Broker | Linux Support | API Type | Cost | Best For |
|--------|--------------|----------|------|----------|
| **Interactive Brokers** | ✅ Native | .NET/Java | $0.25-0.85/contract | Reliability |
| **Tradovate** | ✅ Native | REST/WebSocket | $0.25-0.79/contract | Simplicity |
| **CQG** | ✅ Native | .NET/C++ | $$$ | Professional |
| **Trading Technologies** | ✅ Native | REST/.NET | $$$ | Enterprise |
| Rithmic | ⚠️ Wine only | .NET | Variable | Prop firms |
| NinjaTrader | ❌ No | .NET | $99+/month | Windows only |

---

## Deployment Options

### Option 1: Single VPS (Development)

```yaml
Provider: Any (DigitalOcean, Linode, Vultr)
Specs: 2 vCPU, 4GB RAM, 50GB SSD
Cost: ~$20-30/month
Markets: 1-2 instances max
```

### Option 2: Multi-VPS (Production)

```yaml
Trading VPS (per market):
  - 1 vCPU, 2GB RAM, 25GB SSD
  - Cost: ~$10/month per market
  - Deploy one per symbol (ES, NQ, YM)

Shared Services VPS:
  - 2 vCPU, 4GB RAM, 50GB SSD
  - PostgreSQL + Redis
  - Cost: ~$20/month
```

### Option 3: On-Premise

```yaml
Hardware: Your own server
OS: Ubuntu 22.04 LTS
Network: Dedicated internet
Best for: Low latency, high control
```

---

## Environment Variables

### Required

```bash
# Trading Configuration
SYMBOL=ES                          # Market symbol (ES, NQ, YM, CL, GC)
TRADING_MODE=PAPER                 # PAPER or LIVE
RISK_PER_TRADE=1.0                 # Percentage (1.0 = 1%)
MAX_POSITIONS=1                    # Max concurrent positions

# Broker Selection
BROKER=IBKR                        # IBKR or TRADOVATE

# Interactive Brokers (if selected)
IB_ACCOUNT=your_account_id
IB_PASSWORD=your_password
IB_GATEWAY_HOST=localhost
IB_GATEWAY_PORT=7497

# Tradovate (if selected)
TRADOVATE_USERNAME=your_username
TRADOVATE_PASSWORD=your_password
TRADOVATE_ENVIRONMENT=demo         # demo or live

# Database
DB_HOST=localhost
DB_PORT=5432
DB_NAME=tradebase
DB_USER=tradebase
DB_PASSWORD=secure_password

# Redis
REDIS_HOST=localhost
REDIS_PORT=6379

# Notifications
DISCORD_WEBHOOK_URL=https://discord.com/api/webhooks/...
```

### Optional

```bash
# Strategy Parameters
ENTRY_CONFIDENCE=0.65
STOP_LOSS_ATR=1.5
TAKE_PROFIT_ATR=3.0
USE_TRAILING_STOP=true

# Logging
LOG_LEVEL=Information              # Debug, Information, Warning, Error
LOG_TO_FILE=true
LOG_PATH=/var/log/tradebase

# Performance
EVALUATION_INTERVAL_MS=1000        # Strategy evaluation frequency
HEARTBEAT_INTERVAL_MS=30000        # Health check frequency
```

---

## Quick Setup Commands

### Ubuntu/Debian

```bash
# Update system
sudo apt update && sudo apt upgrade -y

# Install Docker
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh
sudo usermod -aG docker $USER

# Install .NET 8
wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt update
sudo apt install -y dotnet-sdk-8.0

# Install Java (for IB Gateway)
sudo apt install -y openjdk-17-jre-headless xvfb

# Verify installation
docker --version
dotnet --version
java -version
```

### CentOS/RHEL

```bash
# Install Docker
sudo yum install -y docker
sudo systemctl start docker
sudo systemctl enable docker

# Install .NET 8
sudo rpm -Uvh https://packages.microsoft.com/config/centos/7/packages-microsoft-prod.rpm
sudo yum install -y dotnet-sdk-8.0

# Install Java
sudo yum install -y java-17-openjdk-headless xorg-x11-server-Xvfb
```

---

## Security Requirements

### Network Security

```yaml
Firewall: UFW or iptables (outbound only)
VPN: Optional but recommended
Encryption: TLS 1.3 for all API calls
API Keys: Stored in environment variables, never in code
```

### File Permissions

```bash
# Config files
chmod 600 .env
chmod 600 config/*.json

# Log files
chmod 644 /var/log/tradebase/*.log

# Database
chmod 600 /var/lib/postgresql/data/
```

### Secrets Management

```yaml
Development: .env file (gitignored)
Production: Docker secrets or HashiCorp Vault
Cloud: Native secret manager (AWS Secrets Manager, etc.)
```

---

## Monitoring Requirements

### Essential Metrics

```yaml
System:
  - CPU usage < 80%
  - RAM usage < 80%
  - Disk usage < 80%
  - Network latency < 50ms

Trading:
  - Connection status (up/down)
  - Order fill latency
  - Position tracking accuracy
  - P&L calculation
  - Daily loss limit (circuit breaker)
```

### Alerting Channels

```yaml
Critical: Discord @here mention
Warning: Discord regular message
Info: Log file only
```

---

## Scaling Guide

### Vertical Scaling (More Power)

```yaml
Add RAM: More markets per instance
Add CPU: Faster strategy evaluation
Add Disk: Longer analytics retention
```

### Horizontal Scaling (More Instances)

```yaml
Per Symbol: One container per market
Per Strategy: Different strategies per symbol
Per Account: Multiple broker accounts
```

### Example: 5 Markets

```yaml
# docker-compose.yml
services:
  es-trader:    # E-mini S&P 500
  nq-trader:    # E-mini NASDAQ
  ym-trader:    # E-mini Dow
  cl-trader:    # Crude Oil
  gc-trader:    # Gold
  
  shared-db:    # PostgreSQL
  shared-redis: # Redis cache
```

Total: 7 containers, 7-9 vCPUs, 14-18GB RAM

---

## Compliance Notes

### Data Retention

```yaml
Trade Logs: 7 years (US requirement)
Order Audit: 7 years
Market Data: 1 year (minimum)
System Logs: 90 days
```

### Backup Requirements

```yaml
Database: Daily automated backups
Configuration: Version controlled
Secrets: Encrypted backup
Code: Git repository
```

---

## Support Matrix

| Component | Min Version | Recommended | Tested On |
|-----------|-------------|-------------|-----------|
| Ubuntu | 20.04 | 22.04 LTS | ✅ |
| Debian | 11 | 12 | ✅ |
| CentOS | 8 | Stream 9 | ✅ |
| Docker | 20.10 | 24.0 | ✅ |
| .NET | 8.0 | 8.0 | ✅ |
| PostgreSQL | 15 | 16 | ✅ |
| Redis | 7 | 7.2 | ✅ |

---

## Troubleshooting

### Common Issues

```yaml
Issue: Connection refused to IB Gateway
Fix: Check IB Gateway is running on port 7497

Issue: High latency (> 100ms)
Fix: Move VPS closer to broker (Chicago for CME)

Issue: Out of memory
Fix: Add more RAM or reduce number of instances

Issue: Database connection failed
Fix: Check PostgreSQL is running and credentials
```

---

## Summary

**TradeBase requires:**
- ✅ Linux (any modern distribution)
- ✅ Docker + Docker Compose
- ✅ .NET 8
- ✅ Outbound internet (no inbound ports)
- ✅ 2GB RAM per trading instance
- ✅ Optional: PostgreSQL + Redis for analytics

**No Windows required. No Wine. No extra hosting.**

Deploy anywhere Docker runs! 🐧🐳
