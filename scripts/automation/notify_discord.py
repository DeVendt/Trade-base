#!/usr/bin/env python3
"""
Discord Notification Script for GitHub Actions
Sends notifications about workflow step status.
"""

import argparse
import os
import sys

# Add src to path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', '..', 'src'))

try:
    from notifications import DiscordNotifier
except ImportError:
    DiscordNotifier = None


def main():
    parser = argparse.ArgumentParser(description='Send Discord notification')
    parser.add_argument('--step', type=str, required=True, help='Step name')
    parser.add_argument('--status', type=str, required=True, help='Status (success, failure, etc.)')
    parser.add_argument('--webhook', type=str, default='', help='Discord webhook URL')
    parser.add_argument('--message', type=str, default='', help='Additional message')
    
    args = parser.parse_args()
    
    webhook_url = args.webhook or os.getenv('DISCORD_WEBHOOK_URL')
    
    if not webhook_url:
        print("No webhook URL provided, skipping notification")
        return 0
    
    if DiscordNotifier is None:
        print(f"Notification: Step '{args.step}' completed with status '{args.status}'")
        return 0
    
    try:
        import asyncio
        
        async def send():
            discord = DiscordNotifier(webhook_url)
            
            # Map status to severity
            severity_map = {
                'success': 'success',
                'failure': 'error',
                'cancelled': 'warning',
                'skipped': 'info'
            }
            severity = severity_map.get(args.status.lower(), 'info')
            
            title = f"Workflow Step: {args.step}"
            description = f"Status: {args.status.upper()}"
            if args.message:
                description += f"\n{args.message}"
            
            await discord.notify_system_alert(title, description, severity=severity)
            await discord.close()
        
        asyncio.run(send())
        print(f"Notification sent for step '{args.step}' with status '{args.status}'")
        return 0
        
    except Exception as e:
        print(f"Failed to send notification: {e}", file=sys.stderr)
        # Don't fail the workflow because of notification issues
        return 0


if __name__ == '__main__':
    sys.exit(main())
