# Health check for NinjaTrader container
param(
    [string]$BridgeUrl = "http://localhost:50052/health",
    [int]$Timeout = 5
)

try {
    # Check if NinjaTrader process is running
    $ntProcess = Get-Process -Name "NinjaTrader" -ErrorAction SilentlyContinue
    if (-not $ntProcess) {
        Write-Output "UNHEALTHY: NinjaTrader process not running"
        exit 1
    }
    
    # Check if Bridge is responding
    try {
        $response = Invoke-RestMethod -Uri $BridgeUrl -TimeoutSec $Timeout -ErrorAction Stop
        if ($response.status -eq "healthy") {
            Write-Output "HEALTHY"
            exit 0
        } else {
            Write-Output "UNHEALTHY: Bridge reports unhealthy status"
            exit 1
        }
    } catch {
        # Bridge check failed, but NinjaTrader is still running
        # This is a warning state, not critical
        Write-Output "HEALTHY (Bridge unreachable but NT running)"
        exit 0
    }
} catch {
    Write-Output "UNHEALTHY: $_"
    exit 1
}
