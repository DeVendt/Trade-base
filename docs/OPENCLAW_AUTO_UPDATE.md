# OpenClaw Automated Update System

## Overview

Daily automated checks for OpenClaw updates with Discord reporting to channel: `home-server-information`

## Schedule

- **When:** Every day at 8:00 AM Amsterdam (CET/CEST)
- **Where:** Runs on the OpenClaw gateway server
- **Reports to:** Discord channel #home-server-information

## What It Does

1. **Checks for OpenClaw updates**
2. **If updates available:** Automatically applies them
3. **Posts report to Discord** with:
   - Update status (updated / no updates / failed)
   - Version information (before/after)
   - What's changed (if updated)
   - Warnings or tips
   - System status

## Components

### 1. Update Check Script
**Location:** `/home/cptvendt/.openclaw/scripts/daily-update-check.sh`

Functions:
- Checks current OpenClaw version
- Runs `openclaw update check`
- Applies updates if available
- Generates status report
- Saves report for Discord notification

### 2. Cronjob
**Name:** `openclaw-daily-update-check`  
**Schedule:** `0 8 * * *` (daily 8:00 AM)  
**Timezone:** Europe/Amsterdam

### 3. Discord Integration
- Posts to: #home-server-information
- Includes: Status, versions, warnings, tips
- Format: Rich markdown with emojis

## Report Format

```
🏴‍☠️ OpenClaw Daily Update Report - 2025-02-22 08:00

Status: ✅ UPDATED
Previous Version: 2026.2.17
Current Version: 2026.2.18

Details:
✅ Update applied successfully
📝 What's new: Bug fixes, performance improvements

System Info:
- Host: vendtpiratecabin
- Uptime: 5 days, 3 hours
- Last Check: 2025-02-22 08:00:00

---
*Automated check by Quartermaster*
```

## Manual Execution

### Run Update Check Now
```bash
/home/cptvendt/.openclaw/scripts/daily-update-check.sh
```

### Check Update Status
```bash
openclaw update check
```

### Apply Updates Manually
```bash
openclaw update run
```

## Troubleshooting

### No Discord Notification
- Check cronjob status: `openclaw cron list`
- Verify channel ID: `1443184241872474163`
- Check bot permissions in Discord

### Update Check Fails
- Check network connection
- Verify OpenClaw status: `openclaw gateway status`
- Check logs: `/var/log/openclaw-update-check.log`

### Script Errors
- Check script permissions: `ls -la /home/cptvendt/.openclaw/scripts/`
- Verify OpenClaw CLI works: `openclaw version`

## Logs

**Update Check Log:**
```
/var/log/openclaw-update-check.log
```

**Temp Reports:**
```
/tmp/openclaw-daily-report-YYYYMMDD.txt
/tmp/update-check-result.txt
/tmp/update-run-result.txt
```

## Modifying the Schedule

### Change Time
```bash
openclaw cron update openclaw-daily-update-check \
  --schedule "0 9 * * *" \
  --timezone "Europe/Amsterdam"
```

### Pause Updates
```bash
openclaw cron update openclaw-daily-update-check --enabled false
```

### Resume Updates
```bash
openclaw cron update openclaw-daily-update-check --enabled true
```

## Security Notes

- Script runs with user permissions (not root)
- No sensitive data in logs
- Discord webhook uses existing OpenClaw integration
- Updates are applied automatically (tested/stable only)

## Backup

This script is committed to the TradeBase repository:
```
scripts/maintenance/openclaw-update-check.sh
```

## Maintenance

**Monthly:**
- Review update logs
- Check for failed updates
- Verify Discord notifications working

**Quarterly:**
- Review and update script if needed
- Test manual update process
- Verify backup of script

---

**Created:** 2025-02-21  
**Maintainer:** Quartermaster  
**Version:** 1.0
