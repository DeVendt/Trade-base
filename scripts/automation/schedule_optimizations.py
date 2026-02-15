#!/usr/bin/env python3
"""
Automation Script: Schedule Recurring Optimizations
Sets up the optimization queue with recurring tasks.

Usage:
    python schedule_optimizations.py [--discord-webhook URL]
"""

import asyncio
import argparse
import os
import sys
import uuid
from datetime import datetime

sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', '..', 'src'))

from improvement_engine import ContinuousImprovementEngine, OptimizationTask, OptimizationType
from notifications import DiscordNotifier


async def setup_default_schedule(engine: ContinuousImprovementEngine):
    """Set up default recurring optimization tasks."""
    
    tasks = [
        # Hourly tasks
        OptimizationTask(
            task_id=str(uuid.uuid4()),
            task_type=OptimizationType.HYPERPARAMETER,
            component_id="main_strategy",
            frequency="hourly",
            priority=3,
            config={"params": ["stop_loss", "take_profit", "position_size"]}
        ),
        
        # Daily tasks
        OptimizationTask(
            task_id=str(uuid.uuid4()),
            task_type=OptimizationType.MODEL_RETRAIN,
            component_id="direction_predictor",
            frequency="daily",
            priority=2,
            config={"min_samples": 10000, "validation_split": 0.2}
        ),
        OptimizationTask(
            task_id=str(uuid.uuid4()),
            task_type=OptimizationType.FEATURE_SELECTION,
            component_id="feature_set_v1",
            frequency="daily",
            priority=4,
            config={"method": "importance_threshold", "threshold": 0.01}
        ),
        OptimizationTask(
            task_id=str(uuid.uuid4()),
            task_type=OptimizationType.STRATEGY_WEIGHTS,
            component_id="portfolio",
            frequency="daily",
            priority=3,
            config={"lookback_days": 30, "optimization_metric": "sharpe_ratio"}
        ),
        
        # Weekly tasks
        OptimizationTask(
            task_id=str(uuid.uuid4()),
            task_type=OptimizationType.RISK_PARAMS,
            component_id="risk_manager",
            frequency="weekly",
            priority=1,
            config={"max_drawdown_limit": 0.15, "var_confidence": 0.95}
        ),
    ]
    
    for task in tasks:
        await engine.add_task(task)
        print(f"Added: {task.task_type.value} ({task.frequency}) for {task.component_id}")
    
    return tasks


async def main():
    parser = argparse.ArgumentParser(description='Schedule recurring optimizations')
    parser.add_argument('--discord-webhook', type=str, help='Discord webhook URL')
    parser.add_argument('--clear-existing', action='store_true', help='Clear existing tasks')
    args = parser.parse_args()
    
    webhook_url = args.discord_webhook or os.getenv('DISCORD_WEBHOOK_URL')
    discord = DiscordNotifier(webhook_url) if webhook_url else None
    
    print("Setting up recurring optimization schedule...")
    print("=" * 60)
    
    # Create engine
    db_connection = None
    engine = ContinuousImprovementEngine(db_connection, discord)
    
    # Clear existing if requested
    if args.clear_existing:
        print("Clearing existing tasks...")
        # Implementation would clear DB table
    
    # Setup default schedule
    tasks = await setup_default_schedule(engine)
    
    print("=" * 60)
    print(f"Scheduled {len(tasks)} optimization tasks")
    print("\nTask Summary:")
    print(f"  - Hourly: {sum(1 for t in tasks if t.frequency == 'hourly')}")
    print(f"  - Daily: {sum(1 for t in tasks if t.frequency == 'daily')}")
    print(f"  - Weekly: {sum(1 for t in tasks if t.frequency == 'weekly')}")
    
    if discord:
        await discord.notify_system_alert(
            "📅 Optimization Schedule Created",
            f"{len(tasks)} recurring tasks scheduled\nHourly: {sum(1 for t in tasks if t.frequency == 'hourly')}\nDaily: {sum(1 for t in tasks if t.frequency == 'daily')}\nWeekly: {sum(1 for t in tasks if t.frequency == 'weekly')}",
            severity="success"
        )
        await discord.close()


if __name__ == '__main__':
    asyncio.run(main())
