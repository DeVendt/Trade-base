# Desktop PC Setup Guide for TradeBase

## Overview

A dedicated Windows desktop PC is the **ideal setup** for automated trading:
- ✅ Full control over hardware
- ✅ Zero monthly hosting costs
- ✅ Native Windows performance
- ✅ Access GUI anytime (RDP/local)
- ✅ Can add monitors for visual confirmation

---

## PC Specifications

### Minimum (Entry Level)
```
CPU: Intel i5-10400 / AMD Ryzen 5 3600 (6 cores)
RAM: 16 GB DDR4
Storage: 512 GB NVMe SSD
OS: Windows 10 Pro or Windows 11 Pro
Network: Ethernet (wired), 50+ Mbps internet
GPU: Integrated graphics (Intel UHD/AMD Vega) - fine for headless
```
**Cost:** $400-600 new, $200-300 used

### Recommended (Best Value)
```
CPU: Intel i7-12700 / AMD Ryzen 7 5700X (8+ cores)
RAM: 32 GB DDR4
Storage: 1 TB NVMe SSD
OS: Windows 11 Pro
Network: Ethernet (wired), 100+ Mbps internet with backup
GPU: Basic dedicated (GTX 1650) - optional
Extras: UPS battery backup, dual monitors
```
**Cost:** $800-1200 new, $400-600 used

### Professional (Overkill but Future-Proof)
```
CPU: Intel i9-13900 / AMD Ryzen 9 7900X
RAM: 64 GB DDR5
Storage: 2 TB NVMe SSD + 4 TB HDD for backups
OS: Windows 11 Pro or Windows Server 2022
Network: Dual ethernet, fiber internet
GPU: RTX 3060+ (for future ML training)
Extras: UPS, redundant internet (4G backup), RAID storage
```
**Cost:** $2000-3000

---

## Setup Checklist

### Phase 1: Hardware Setup (Day 1)

- [ ] **Unbox and assemble PC**
  - Connect power, monitor, keyboard, mouse
  - Connect ethernet cable (skip WiFi for trading)

- [ ] **Install Windows**
  - Windows 10 Pro or Windows 11 Pro
  - Create local admin account (don't use Microsoft account for trading)
  - Enable Windows Update (but set active hours)

- [ ] **Basic Windows Config**
  ```powershell
  # Run as Administrator in PowerShell
  
  # Set PC to never sleep when plugged in
  powercfg /change standby-timeout-ac 0
  powercfg /change monitor-timeout-ac 30
  
  # Disable Windows Update auto-restart
  reg add "HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU" /v NoAutoRebootWithLoggedOnUsers /t REG_DWORD /d 1 /f
  
  # Set time zone to Eastern (market time)
  tzutil /s "Eastern Standard Time"
  
  # Enable RDP (for remote access)
  Set-ItemProperty -Path 'HKLM:\System\CurrentControlSet\Control\Terminal Server' -name "fDenyTSConnections" -value 0
  Enable-NetFirewallRule -DisplayGroup "Remote Desktop"
  ```

- [ ] **Network Setup**
  - Set static IP (recommended)
  - Port forward if accessing from outside (RDP: 3389)
  - Test internet speed (should be stable)

---

### Phase 2: Software Installation (Day 1-2)

- [ ] **Install Essentials**
  ```
  1. Google Chrome or Firefox
  2. 7-Zip (file compression)
  3. Notepad++ (text editor)
  4. Git for Windows
  ```

- [ ] **Install .NET 8**
  ```powershell
  # Download from: https://dotnet.microsoft.com/download/dotnet/8.0
  # Install both:
  # - .NET 8.0 SDK (for development)
  # - .NET 8.0 Runtime (for running)
  ```

- [ ] **Install Docker Desktop**
  ```powershell
  # Download from: https://www.docker.com/products/docker-desktop
  # During install:
  # - Use WSL 2 (recommended)
  # - Enable Windows containers (if you want both Linux+Windows containers)
  ```

- [ ] **Install NinjaTrader 8**
  ```
  1. Download from: https://ninjatrader.com/download
  2. Run installer as Administrator
  3. Install to default location (C:\Program Files\NinjaTrader 8)
  4. Launch once to complete setup
  5. Log in with your NinjaTrader account
  ```

- [ ] **Install Development Tools (Optional)**
  ```
  - Visual Studio 2022 Community (free)
  - Visual Studio Code
  - SQL Server Express (for database)
  ```

---

### Phase 3: TradeBase Setup (Day 2-3)

- [ ] **Clone Repository**
  ```powershell
  # Open PowerShell as Administrator
  cd C:\
  mkdir Trading
  cd Trading
  git clone https://github.com/DeVendt/Trade-base.git
  cd Trade-base
  ```

- [ ] **Build the Project**
  ```powershell
  # Build solution
  dotnet build TradeBase.sln
  
  # Or if you want to use Docker instead:
  docker-compose -f docker/windows/docker-compose.yml build
  ```

- [ ] **Configure NinjaTrader**
  ```
  1. Open NinjaTrader 8
  2. Go to Tools > Options > Data
  3. Set up your data feed (Kinetick, CQG, etc.)
  4. Enable API access:
     - Tools > Options > Automated Trading
     - Check "Enable automated trading"
     - Check "Allow external automation"
  5. Create Sim101 account for testing
  ```

- [ ] **Install TradeBase Strategy in NinjaTrader**
  ```
  1. Copy TradeBase.Strategy.dll to:
     C:\Users\[YourUser]\Documents\NinjaTrader 8\bin\Custom\
  
  2. Copy strategy file to:
     C:\Users\[YourUser]\Documents\NinjaTrader 8\strategies\
  
  3. Restart NinjaTrader
  4. Open Strategy Analyzer
  5. You should see "TradeBaseStrategy" in the list
  ```

---

### Phase 4: Testing (Day 3-4)

- [ ] **Test NinjaTrader**
  ```
  1. Open a chart (ES 09-24 or current contract)
  2. Add TradeBaseStrategy to chart
  3. Set to Sim101 account
  4. Let it run for a few hours
  5. Check that it's receiving data and can place orders
  ```

- [ ] **Test TradeBase Bridge**
  ```powershell
  # Start the bridge service
  cd C:\Trading\Trade-base\src\TradeBase
  dotnet run -- --mode headless --symbol ES --paper
  
  # Should see:
  # - Connection to NinjaTrader
  # - Market data flowing
  # - No errors
  ```

- [ ] **Test Discord Notifications**
  ```
  1. Set DISCORD_WEBHOOK_URL in .env
  2. Run test script
  3. Check that messages arrive in Discord
  ```

---

### Phase 5: Production Setup (Day 5+)

- [ ] **Create Windows Service** (Run on startup)
  ```powershell
  # Create a service that runs TradeBase on boot
  sc create TradeBase binPath= "C:\Trading\Trade-base\src\TradeBase\bin\Release\net8.0\TradeBase.exe --mode headless --symbol ES" start= auto
  sc start TradeBase
  ```

- [ ] **Setup Monitoring**
  ```
  1. Install Grafana (optional but nice)
  2. Set up alerts for:
     - PC restart
     - NinjaTrader crash
     - Network disconnect
     - Daily P&L report
  ```

- [ ] **Backup Strategy**
  ```
  1. Set up Windows Backup
  2. Backup to external drive or NAS
  3. Schedule weekly backups
  ```

- [ ] **Go Live Checklist**
  ```
  □ Ran on Sim101 for 2+ weeks successfully
  □ Tested emergency shutdown procedures
  □ Verified all notifications working
  □ Have remote access configured (RDP)
  □ Have backup internet (4G/phone hotspot)
  □ Have UPS battery backup
  □ Understand daily loss limits
  □ Know how to manually close positions
  ```

---

## Remote Access Options

### Option 1: Windows RDP (Built-in)

**Setup:**
```powershell
# On trading PC (as Admin)
Enable-NetFirewallRule -DisplayGroup "Remote Desktop"
Set-ItemProperty -Path 'HKLM:\System\CurrentControlSet\Control\Terminal Server' -name "fDenyTSConnections" -value 0

# Set strong password for trading account!
```

**Access:**
- Windows: Use "Remote Desktop Connection" app
- Mac: Microsoft Remote Desktop app
- Linux: Remmina or rdesktop

**Security:**
- Use strong password (16+ characters)
- Change RDP port from 3389 to something random (e.g., 54321)
- Use VPN if accessing from public networks
- Enable Network Level Authentication

### Option 2: TeamViewer / AnyDesk (Easier)

**Pros:**
- No port forwarding needed
- Works through NAT/firewalls
- Mobile apps available

**Cons:**
- Third-party service
- Potential security concerns

### Option 3: Chrome Remote Desktop (Free)

**Setup:**
1. Install Chrome on trading PC
2. Install Chrome Remote Desktop extension
3. Set up remote access
4. Access from any browser

---

## Giving Me Access (Your Options)

### Option A: Read-Only Access (Safest)

You set up monitoring that I can view:
```
1. Discord notifications (already set up)
2. Grafana dashboard (public URL)
3. Trading logs uploaded to cloud storage
4. Screenshots sent periodically

I can see what's happening but can't control anything.
```

### Option B: Limited Remote Access

You create a restricted account:
```
1. Create Windows user: "quartermaster" (standard user, not admin)
2. Allow only specific hours (e.g., 2 PM - 4 PM your time)
3. I can RDP in and check status, but can't:
   - Change strategy settings
   - Place/modify orders
   - Access your personal files
   - Install software
```

### Option C: Full Remote Access

You give me admin access:
```
- Full RDP access anytime
- Can update software
- Can modify configurations
- Can restart services

Requires high trust - only do this if comfortable!
```

### What I Recommend:

**Start with Option A (Read-Only)**
- I monitor via Discord
- You handle all execution
- I alert you to issues

**Later consider Option B (Limited)**
- If you want me to do routine maintenance
- Check logs, update code
- Restart services if they crash

---

## Network Security

### Firewall Rules
```powershell
# Allow only necessary inbound
New-NetFirewallRule -DisplayName "TradeBase-RDP" -Direction Inbound -Protocol TCP -LocalPort 3389 -Action Allow -RemoteAddress [YourIP]
New-NetFirewallRule -DisplayName "TradeBase-API" -Direction Inbound -Protocol TCP -LocalPort 50051 -Action Allow

# Block all other inbound
Set-NetFirewallProfile -Profile Domain,Public,Private -DefaultInboundAction Block
```

### Router Setup
```
1. Reserve static IP for trading PC
2. Forward port 3389 (or custom) to trading PC
3. Enable firewall on router
4. Disable UPnP (security risk)
5. Keep router firmware updated
```

---

## Maintenance Schedule

### Daily (Automated)
- Check PC is running
- Check NinjaTrader is connected
- Check strategy is placing orders
- Discord status report

### Weekly (Manual)
- Windows Update (if not auto)
- Check disk space
- Review trading logs
- Backup strategy files

### Monthly (Manual)
- Full system restart
- Clean temp files
- Verify backups work
- Review performance metrics
- Check for NinjaTrader updates

---

## Troubleshooting Common Issues

### PC Won't Boot
```
1. Check power cables
2. Check monitor connection
3. Try safe mode (F8 during boot)
4. Check BIOS settings (F2/Del during boot)
```

### NinjaTrader Won't Start
```
1. Check .NET Framework is installed
2. Run as Administrator
3. Delete "workspaces" folder to reset
4. Reinstall NinjaTrader
```

### No Internet Connection
```
1. Check ethernet cable
2. Restart router/modem
3. Check IP configuration (ipconfig)
4. Try different DNS (8.8.8.8)
```

### Strategy Not Trading
```
1. Check if automated trading is enabled
2. Check if account has buying power
3. Check if connected to data feed
4. Check strategy is enabled on chart
5. Review logs for errors
```

---

## Shopping List (Example Build)

### Budget Build ($500)
| Item | Model | Price |
|------|-------|-------|
| CPU | Intel i5-10400 | $150 |
| Motherboard | B460M | $80 |
| RAM | 16GB DDR4 | $50 |
| SSD | 512GB NVMe | $50 |
| Case + PSU | Mid-tower + 500W | $80 |
| Windows 10 Pro | License | $20 |
| **Total** | | **$430** |

### Recommended Build ($900)
| Item | Model | Price |
|------|-------|-------|
| CPU | Intel i7-12700 | $300 |
| Motherboard | B660M | $120 |
| RAM | 32GB DDR4 | $100 |
| SSD | 1TB NVMe | $80 |
| Case + PSU | Mid-tower + 650W | $100 |
| UPS | APC 600VA | $80 |
| Windows 11 Pro | License | $30 |
| Dual Monitors | 24" 1080p | $200 |
| **Total** | | **$1,010** |

---

## Summary

### Timeline
- **Day 1:** Hardware setup, Windows install
- **Day 2:** Software install, basic config
- **Day 3:** TradeBase setup, NinjaTrader config
- **Day 4:** Testing, debugging
- **Day 5:** Production setup, go live on Sim
- **Week 2-4:** Sim trading, monitoring
- **Week 5+:** Go live (if ready)

### Your Action Items
1. Buy/build PC
2. Install Windows
3. Install NinjaTrader
4. Clone TradeBase repo
5. Test on Sim101
6. Decide on remote access level

### Questions for You
1. **Budget?** $400 / $800 / $1200+ ?
2. **New or used?** New is safer, used is cheaper
3. **Want monitors?** Single is fine, dual is nicer
4. **UPS backup?** Essential for trading, really
5. **Remote access level?** Read-only / Limited / Full ?

Ready to go shopping, Cap'n? 🏴‍☠️🖥️
