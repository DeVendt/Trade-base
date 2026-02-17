# Start NinjaTrader Container
param(
    [string]$Symbol = $env:SYMBOL,
    [string]$Account = $env:ACCOUNT,
    [string]$TradingMode = $env:TRADING_MODE
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "🏴‍☠️ TradeBase NinjaTrader Container" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Symbol: $Symbol"
Write-Host "Account: $Account"
Write-Host "Mode: $TradingMode"
Write-Host ""

# Function to check if process is running
function Test-Process {
    param([string]$Name)
    return [bool](Get-Process -Name $Name -ErrorAction SilentlyContinue)
}

# Function to log with timestamp
function Write-Log {
    param(
        [string]$Message,
        [string]$Level = "INFO"
    )
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $color = switch ($Level) {
        "ERROR" { "Red" }
        "WARN"  { "Yellow" }
        "SUCCESS" { "Green" }
        default { "White" }
    }
    Write-Host "[$timestamp] [$Level] $Message" -ForegroundColor $color
    Add-Content -Path "C:/NinjaTrader8/log/container.log" -Value "[$timestamp] [$Level] $Message"
}

# Create log directory if not exists
if (!(Test-Path "C:/NinjaTrader8/log")) {
    New-Item -ItemType Directory -Path "C:/NinjaTrader8/log" -Force | Out-Null
}

Write-Log "Starting TradeBase NinjaTrader Container" "INFO"
Write-Log "Symbol: $Symbol, Account: $Account, Mode: $TradingMode" "INFO"

# Check if NinjaTrader files exist
if (!(Test-Path "C:/NinjaTrader8/bin/NinjaTrader.exe")) {
    Write-Log "ERROR: NinjaTrader.exe not found! Please mount NinjaTrader installation." "ERROR"
    exit 1
}

Write-Log "NinjaTrader installation found" "SUCCESS"

# Start NinjaTrader
Write-Log "Starting NinjaTrader 8..." "INFO"
try {
    $ntProcess = Start-Process -FilePath "C:/NinjaTrader8/bin/NinjaTrader.exe" `
        -ArgumentList @("/nologo") `
        -WorkingDirectory "C:/NinjaTrader8" `
        -PassThru `
        -WindowStyle Hidden
    
    Write-Log "NinjaTrader started with PID: $($ntProcess.Id)" "SUCCESS"
} catch {
    Write-Log "Failed to start NinjaTrader: $_" "ERROR"
    exit 1
}

# Wait for NinjaTrader to initialize
Write-Log "Waiting for NinjaTrader to initialize (30 seconds)..." "INFO"
Start-Sleep -Seconds 30

# Verify NinjaTrader is still running
if (!(Test-Process -Name "NinjaTrader")) {
    Write-Log "NinjaTrader process exited unexpectedly!" "ERROR"
    exit 1
}

Write-Log "NinjaTrader is running" "SUCCESS"

# Start Bridge Service
Write-Log "Starting TradeBase Bridge Service..." "INFO"
try {
    $bridgeProcess = Start-Process -FilePath "dotnet" `
        -ArgumentList "C:/Bridge/TradeBase.Bridge.dll" `
        -WorkingDirectory "C:/Bridge" `
        -PassThru `
        -WindowStyle Hidden
    
    Write-Log "Bridge Service started with PID: $($bridgeProcess.Id)" "SUCCESS"
} catch {
    Write-Log "Failed to start Bridge Service: $_" "ERROR"
    # Don't exit - NinjaTrader can still run without bridge
}

# Wait for bridge to start
Start-Sleep -Seconds 5

# Main monitoring loop
Write-Log "Entering monitoring loop..." "INFO"
$restartCount = 0
$maxRestarts = 5

while ($true) {
    Start-Sleep -Seconds 10
    
    # Check NinjaTrader
    if (!(Test-Process -Name "NinjaTrader")) {
        Write-Log "NinjaTrader process not found!" "ERROR"
        
        if ($restartCount -lt $maxRestarts) {
            $restartCount++
            Write-Log "Attempting restart ($restartCount/$maxRestarts)..." "WARN"
            
            try {
                $ntProcess = Start-Process -FilePath "C:/NinjaTrader8/bin/NinjaTrader.exe" -PassThru -WindowStyle Hidden
                Write-Log "NinjaTrader restarted with PID: $($ntProcess.Id)" "SUCCESS"
                Start-Sleep -Seconds 30
            } catch {
                Write-Log "Failed to restart NinjaTrader: $_" "ERROR"
            }
        } else {
            Write-Log "Max restarts reached. Exiting." "ERROR"
            exit 1
        }
    }
    
    # Check Bridge (optional)
    if ($bridgeProcess -and $bridgeProcess.HasExited) {
        Write-Log "Bridge Service exited. Attempting restart..." "WARN"
        try {
            $bridgeProcess = Start-Process -FilePath "dotnet" -ArgumentList "C:/Bridge/TradeBase.Bridge.dll" -PassThru -WindowStyle Hidden
            Write-Log "Bridge Service restarted" "SUCCESS"
        } catch {
            Write-Log "Failed to restart Bridge Service: $_" "ERROR"
        }
    }
    
    # Log heartbeat every 60 iterations (10 minutes)
    if ($script:heartbeatCounter -ge 60) {
        Write-Log "Heartbeat: All services running" "INFO"
        $script:heartbeatCounter = 0
    }
    $script:heartbeatCounter++
}
