#!/usr/bin/env python3
"""
Automation Script: Run Improvement Cycle
Executes the 4-step continuous improvement process.

Usage:
    python run_improvement_cycle.py --step {analyze|identify|optimize|deploy} \
        [--input INPUT_FILE] [--output OUTPUT_FILE] [--strategy STRATEGY_ID]

Environment Variables:
    DATABASE_URL: PostgreSQL connection string
    DISCORD_WEBHOOK_URL: Discord webhook for notifications
"""

import asyncio
import argparse
import json
import os
import sys
from datetime import datetime
from enum import Enum
import logging

# Add src to path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', '..', 'src'))

try:
    from improvement_engine import OptimizationRunner, ContinuousImprovementEngine, OptimizationTask, OptimizationType
    from notifications import DiscordNotifier
    HAS_IMPROVEMENT_ENGINE = True
except ImportError as e:
    print(f"Warning: Could not import improvement_engine modules: {e}", file=sys.stderr)
    HAS_IMPROVEMENT_ENGINE = False
    OptimizationRunner = None
    ContinuousImprovementEngine = None
    OptimizationTask = None
    OptimizationType = None
    DiscordNotifier = None


class Step(Enum):
    ANALYZE = "analyze"
    IDENTIFY = "identify"
    OPTIMIZE = "optimize"
    DEPLOY = "deploy"


# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
    handlers=[logging.StreamHandler()]
)
logger = logging.getLogger(__name__)


async def run_analyze(strategy: str = None, input_file: str = None, output_file: str = None):
    """Step 1: Analyze performance and generate recommendations."""
    logger.info("Running analysis step...")
    
    # Mock analysis results - in real implementation, this would analyze actual performance data
    recommendations = []
    if not strategy:
        # Generate some mock recommendations for testing
        recommendations = [
            {"type": "parameter_tuning", "strategy": "strategy_1", "priority": "high"},
            {"type": "risk_adjustment", "strategy": "strategy_2", "priority": "medium"}
        ]
    else:
        recommendations = [{"type": "parameter_tuning", "strategy": strategy, "priority": "high"}]
    
    result = {
        "step": "analyze",
        "timestamp": datetime.utcnow().isoformat(),
        "recommendations_count": len(recommendations),
        "recommendations": recommendations,
        "strategy_id": strategy or "all"
    }
    
    if output_file:
        with open(output_file, 'w') as f:
            json.dump(result, f, indent=2)
        logger.info(f"Analysis results written to {output_file}")
    
    return result


async def run_identify(input_file: str = None, output_file: str = None):
    """Step 2: Identify specific optimization opportunities."""
    logger.info("Running identify step...")
    
    # Load previous results if available
    analysis_data = {}
    if input_file and os.path.exists(input_file):
        with open(input_file, 'r') as f:
            analysis_data = json.load(f)
    
    recommendations = analysis_data.get('recommendations', [])
    
    # Transform recommendations into specific optimization tasks
    optimizations = []
    for rec in recommendations:
        optimizations.append({
            "optimization_id": f"opt_{rec.get('strategy', 'unknown')}_{datetime.utcnow().timestamp()}",
            "strategy": rec.get('strategy'),
            "type": rec.get('type'),
            "priority": rec.get('priority'),
            "estimated_improvement": "5-10%"
        })
    
    result = {
        "step": "identify",
        "timestamp": datetime.utcnow().isoformat(),
        "optimizations_count": len(optimizations),
        "optimizations": optimizations,
        "optimizations_needed": len(optimizations) > 0
    }
    
    if output_file:
        with open(output_file, 'w') as f:
            json.dump(result, f, indent=2)
        logger.info(f"Identify results written to {output_file}")
    
    return result


async def run_optimize(input_file: str = None, output_file: str = None):
    """Step 3: Execute optimizations and run backtests."""
    logger.info("Running optimize step...")
    
    # Load previous results if available
    identify_data = {}
    if input_file and os.path.exists(input_file):
        with open(input_file, 'r') as f:
            identify_data = json.load(f)
    
    optimizations = identify_data.get('optimizations', [])
    
    # Run optimizations and backtests
    results = []
    backtest_passed = True
    
    for opt in optimizations:
        # Mock optimization result
        opt_result = {
            "optimization_id": opt.get('optimization_id'),
            "success": True,
            "backtest_profit": 12.5,
            "backtest_drawdown": 3.2,
            "sharpe_ratio": 1.8
        }
        results.append(opt_result)
    
    result = {
        "step": "optimize",
        "timestamp": datetime.utcnow().isoformat(),
        "optimizations_executed": len(results),
        "results": results,
        "backtest_passed": backtest_passed and len(results) > 0
    }
    
    if output_file:
        with open(output_file, 'w') as f:
            json.dump(result, f, indent=2)
        logger.info(f"Optimize results written to {output_file}")
    
    return result


async def run_deploy(input_file: str = None, output_file: str = None):
    """Step 4: Deploy with A/B testing."""
    logger.info("Running deploy step...")
    
    # Load previous results if available
    optimize_data = {}
    if input_file and os.path.exists(input_file):
        with open(input_file, 'r') as f:
            optimize_data = json.load(f)
    
    # Mock deployment
    deployed = optimize_data.get('backtest_passed', False)
    
    result = {
        "step": "deploy",
        "timestamp": datetime.utcnow().isoformat(),
        "deployed": deployed,
        "ab_test_enabled": True,
        "traffic_split": "90/10",
        "deployment_id": f"deploy_{datetime.utcnow().strftime('%Y%m%d_%H%M%S')}"
    }
    
    if output_file:
        with open(output_file, 'w') as f:
            json.dump(result, f, indent=2)
        logger.info(f"Deploy results written to {output_file}")
    
    return result


async def main():
    parser = argparse.ArgumentParser(description='Run continuous improvement cycle')
    parser.add_argument('--step', type=str, choices=['analyze', 'identify', 'optimize', 'deploy'],
                        help='Specific step to run')
    parser.add_argument('--input', type=str, help='Input JSON file from previous step')
    parser.add_argument('--output', type=str, help='Output JSON file for results')
    parser.add_argument('--strategy', type=str, help='Specific strategy to optimize')
    parser.add_argument('--discord-webhook', type=str, help='Discord webhook URL')
    parser.add_argument('--continuous', action='store_true', help='Run in continuous mode')
    parser.add_argument('--interval', type=int, default=3600, help='Interval between runs in seconds')
    
    args = parser.parse_args()
    
    # Route to specific step or full cycle
    if args.step:
        step = Step(args.step)
        
        if step == Step.ANALYZE:
            result = await run_analyze(args.strategy, args.input, args.output)
        elif step == Step.IDENTIFY:
            result = await run_identify(args.input, args.output)
        elif step == Step.OPTIMIZE:
            result = await run_optimize(args.input, args.output)
        elif step == Step.DEPLOY:
            result = await run_deploy(args.input, args.output)
        else:
            raise ValueError(f"Unknown step: {args.step}")
        
        # Print summary
        print(json.dumps(result, indent=2))
        return 0 if result.get('backtest_passed', True) else 1
    
    else:
        # Run full cycle (original behavior)
        if not HAS_IMPROVEMENT_ENGINE:
            print("Error: Improvement engine not available for full cycle", file=sys.stderr)
            return 1
        
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
            db_connection = None
            runner = OptimizationRunner(db_connection, discord)
            
            if args.continuous:
                logger.info(f"Starting continuous improvement mode (interval: {args.interval}s)")
                while True:
                    logger.info("=" * 80)
                    logger.info(f"Starting improvement cycle at {datetime.utcnow()}")
                    logger.info("=" * 80)
                    
                    try:
                        results = await runner.run_full_cycle(args.strategy)
                        for r in results:
                            status = "✅" if r.success else "❌"
                            logger.info(f"{status} {r.step.value.upper()}: {len(r.actions_taken)} actions")
                    except Exception as e:
                        logger.error(f"Cycle failed: {e}")
                        if discord:
                            await discord.notify_system_alert("Improvement Cycle Failed", str(e), severity="error")
                    
                    logger.info(f"Sleeping for {args.interval} seconds...")
                    await asyncio.sleep(args.interval)
            else:
                logger.info("Running single improvement cycle")
                results = await runner.run_full_cycle(args.strategy)
                
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
        
        return 0


if __name__ == '__main__':
    sys.exit(asyncio.run(main()))
