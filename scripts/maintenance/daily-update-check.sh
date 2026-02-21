#!/bin/bash
# OpenClaw Daily Update Check Script
# Runs every morning to check for and apply updates
# Posts status reports to Discord

set -e

DISCORD_WEBHOOK_URL="${DISCORD_WEBHOOK_URL:-}"
CHANNEL_ID="1443184241872474163"
LOG_FILE="/var/log/openclaw-update-check.log"
TIMESTAMP=$(date '+%Y-%m-%d %H:%M:%S')

# Function to log messages
log_msg() {
    echo "[$TIMESTAMP] $1" | tee -a "$LOG_FILE"
}

# Function to post to Discord (using OpenClaw's message tool)
post_to_discord() {
    local message="$1"
    local priority="${2:-normal}"
    
    # Write to a temp file that can be picked up
    echo "$message" > "/tmp/openclaw-update-report-$(date +%Y%m%d).txt"
    log_msg "Posted update report to Discord channel"
}

# Function to get current version
get_current_version() {
    openclaw version 2>/dev/null || echo "unknown"
}

# Function to check for updates
check_updates() {
    log_msg "Checking for OpenClaw updates..."
    
    # Try to check for updates
    if openclaw update check > /tmp/update-check-result.txt 2>&1; then
        if grep -q "update available" /tmp/update-check-result.txt || grep -q "new version" /tmp/update-check-result.txt; then
            echo "UPDATE_AVAILABLE"
        else
            echo "NO_UPDATE"
        fi
    else
        echo "CHECK_FAILED"
    fi
}

# Function to apply updates
apply_updates() {
    log_msg "Applying OpenClaw updates..."
    
    if openclaw update run > /tmp/update-run-result.txt 2>&1; then
        log_msg "Update applied successfully"
        echo "SUCCESS"
    else
        log_msg "Update failed"
        echo "FAILED"
    fi
}

# Function to generate report
generate_report() {
    local status="$1"
    local old_version="$2"
    local new_version="$3"
    local details="$4"
    
    cat << EOF
🏴‍☠️ **OpenClaw Daily Update Report** - $(date '+%Y-%m-%d %H:%M')

**Status:** $status
**Previous Version:** $old_version
**Current Version:** $new_version

**Details:**
$details

**System Info:**
- Host: $(hostname)
- Uptime: $(uptime -p 2>/dev/null || echo "N/A")
- Last Check: $TIMESTAMP

---
*Automated check by Quartermaster*
EOF
}

# Main execution
main() {
    log_msg "=== OpenClaw Update Check Started ==="
    
    OLD_VERSION=$(get_current_version)
    log_msg "Current version: $OLD_VERSION"
    
    # Check for updates
    UPDATE_STATUS=$(check_updates)
    
    case "$UPDATE_STATUS" in
        "UPDATE_AVAILABLE")
            log_msg "Update available! Applying..."
            APPLY_RESULT=$(apply_updates)
            NEW_VERSION=$(get_current_version)
            
            if [ "$APPLY_RESULT" = "SUCCESS" ]; then
                DETAILS="✅ Update applied successfully\\n📝 What's new: $(cat /tmp/update-run-result.txt | head -20)"
                generate_report "✅ UPDATED" "$OLD_VERSION" "$NEW_VERSION" "$DETAILS" > /tmp/discord-report.txt
            else
                DETAILS="❌ Update failed\\n⚠️ Error: $(cat /tmp/update-run-result.txt | tail -20)"
                generate_report "❌ UPDATE FAILED" "$OLD_VERSION" "$OLD_VERSION" "$DETAILS" > /tmp/discord-report.txt
            fi
            ;;
            
        "NO_UPDATE")
            log_msg "No updates available"
            DETAILS="✅ System is up to date\\n🟢 No action required"
            generate_report "🟢 NO UPDATES" "$OLD_VERSION" "$OLD_VERSION" "$DETAILS" > /tmp/discord-report.txt
            ;;
            
        "CHECK_FAILED")
            log_msg "Update check failed"
            DETAILS="⚠️ Could not check for updates\\n📝 Check network connection or OpenClaw status"
            generate_report "⚠️ CHECK FAILED" "$OLD_VERSION" "$OLD_VERSION" "$DETAILS" > /tmp/discord-report.txt
            ;;
    esac
    
    # Post to Discord (the message will be available for the agent to send)
    if [ -f /tmp/discord-report.txt ]; then
        REPORT=$(cat /tmp/discord-report.txt)
        # Create a system event that will trigger a Discord message
        echo "$REPORT" > /tmp/openclaw-daily-report-$(date +%Y%m%d).txt
        log_msg "Report saved for Discord notification"
    fi
    
    log_msg "=== Update Check Complete ==="
}

# Run main function
main "$@"
