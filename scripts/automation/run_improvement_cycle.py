#!/usr/bin/env python3
"""
Automation Script: Run Improvement Cycle
Executes the 4-step continuous improvement process.

Usage:
    python run_improvement_cycle.py [--strategy STRATEGY_ID] [--discord-webhook URL]

Environment Variables:
    DATABASE_URL: PostgreSQL connection string
    DISCORD_WEBHOOK_URL: Discord webhook for notifications
"""

import asyncio
import argparse
import os
import sys
from datetime import datetime
import logging

# Add src to path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', '..', 'src'))

from improvement_engine import OptimizationRunner, ContinuousImprovementEngine, OptimizationTask, OptimizationType
from notifications import DiscordNotifier

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
    handlers=[
        logging.StreamHandler(),
        logging.FileHandler(f'improvement_cycle_{datetime.now().strftime("%Y%m%d_%H%M%S")}.log')
    ]
)
logger = logging.getLogger(__name__)


async def main():
    parser = argparse.ArgumentParser(description='Run continuous improvement cycle')
    parser.add_argument('--strategy', type=str, help='Specific strategy to optimize')
    parser.add_argument('--discord-webhook', type=str, help='Discord webhook URL')
    parser.add_argument('--continuous', action='store_true', help='Run in continuous mode')
    parser.add_argument('--interval', type=int, default=3600, help='Interval between runs in seconds (default: 3600)')
    args = parser.parse_args()
    
    # Get Discord webhook
    webhook_url = args.discord_webhook or os.getenv('DISCORD_WEBHOOK_URL')
    discord = DiscordNotifier(webhook_url) if webhook_url else None
    
    if discord:
        await discord.notify_system_alert(
            "🔄 Improvement Cycle Starting",
            f"Mode: {'Continuous' if args.continuous else 'Single run'}\nStrategy: {args.strategy or 'All'}",
            severity="info"
        )
    
    try:
        # Create optimization runner
        db_connection = None  # Initialize with actual DB connection
        runner = OptimizationRunner(db_connection, discord)
        
        if args.continuous:
            logger.info(f"Starting continuous improvement mode (interval: {args.interval}s)")
            
            while True:
                logger.info("=" * 80)
                logger.info(f"Starting improvement cycle at {datetime.utcnow()}")
                logger.info("=" * 80)
                
                try:
                    results = await runner.run_full_cycle(args.strategy)
                    
                    # Log results
                    for result in results:
                        status = "✅" if result.success else "❌"
                        logger.info(f"{status} {result.step.value.upper()}: {len(result.actions_taken)} actions")
                
                except Exception as e:
                    logger.error(f"Cycle failed: {e}")
                    if discord:
                        await discord.notify_system_alert(
                            "Improvement Cycle Failed",
                            str(e),
                            severity="error"
                        )
                
                logger.info(f"Sleeping for {args.interval} seconds...")
                await asyncio.sleep(args.interval)
        
        else:
            # Single run
            logger.info("Running single improvement cycle")
            results = await runner.run_full_cycle(args.strategy)
            
            # Print summary
            print("\n" + "=" * 80)
            print("IMPROVEMENT CYCLE SUMMARY")
            print("=" * 80)
            
            for result in results:
                status = "✅ SUCCESS" if result.success else "❌ FAILED"
                print(f"\n{status} - {result.step.value.upper()}")
                print(f"  Actions: {len(result.actions_taken)}")
                if result.actions_taken:
                    for action in result.actions_taken:
                        print(f"    - {action}")
                if result.error:
                    print(f"  Error: {result.error}")
            
            # Send completion notification
            if discord:
                success_count = sum(1 for r in results if r.success)
                await discord.notify_system_alert(
                    f"Improvement Cycle Complete ({success_count}/{len(results)} successful)",
                    "Check logs for detailed results",
                    severity="success" if success_count == len(results) else "warning"
                )
    
    except KeyboardInterrupt:
        logger.info("Interrupted by user")
        if discord:
            await discord.notify_system_alert("Improvement Cycle Interrupted", "Stopped by user", severity="warning")
    
    finally:
        if discord:
            await discord.close()


if __name__ == '__main__':
    asyncio.run(main())
