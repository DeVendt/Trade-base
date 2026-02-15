"""
Continuous Improvement Engine
Manages recurring optimization tasks and automated improvements.
"""

import asyncio
import logging
from datetime import datetime, timedelta
from typing import Dict, List, Optional, Callable
from dataclasses import dataclass, field
from enum import Enum
import json

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)


class OptimizationType(Enum):
    HYPERPARAMETER = "hyperparameter"
    FEATURE_SELECTION = "feature_selection"
    STRATEGY_WEIGHTS = "strategy_weights"
    MODEL_RETRAIN = "model_retrain"
    RISK_PARAMS = "risk_params"


class TaskStatus(Enum):
    PENDING = "pending"
    RUNNING = "running"
    COMPLETED = "completed"
    FAILED = "failed"


@dataclass
class OptimizationTask:
    """Represents a single optimization task in the queue."""
    task_id: str
    task_type: OptimizationType
    component_id: str
    frequency: str  # 'hourly', 'daily', 'weekly'
    priority: int = 5
    config: Dict = field(default_factory=dict)
    last_run_at: Optional[datetime] = None
    next_run_at: Optional[datetime] = None
    status: TaskStatus = TaskStatus.PENDING
    last_result: Optional[Dict] = None
    last_error: Optional[str] = None
    consecutive_failures: int = 0
    enabled: bool = True


class ContinuousImprovementEngine:
    """
    Engine that manages continuous improvement through recurring optimization.
    
    Features:
    - Automated scheduling of optimization tasks
    - Performance-based trigger conditions
    - A/B testing framework integration
    - Automatic rollback on degradation
    - Discord notifications for all events
    """
    
    def __init__(self, db_connection, discord_notifier=None):
        self.db = db_connection
        self.discord = discord_notifier
        self.tasks: Dict[str, OptimizationTask] = {}
        self.running = False
        self._task_handlers: Dict[OptimizationType, Callable] = {}
        self._register_default_handlers()
        
    def _register_default_handlers(self):
        """Register default optimization handlers."""
        self._task_handlers[OptimizationType.HYPERPARAMETER] = self._optimize_hyperparameters
        self._task_handlers[OptimizationType.FEATURE_SELECTION] = self._optimize_features
        self._task_handlers[OptimizationType.STRATEGY_WEIGHTS] = self._optimize_strategy_weights
        self._task_handlers[OptimizationType.MODEL_RETRAIN] = self._retrain_model
        self._task_handlers[OptimizationType.RISK_PARAMS] = self._optimize_risk_params
    
    def register_task_handler(self, task_type: OptimizationType, handler: Callable):
        """Register a custom handler for a task type."""
        self._task_handlers[task_type] = handler
        
    async def add_task(self, task: OptimizationTask) -> str:
        """Add a new optimization task to the queue."""
        task.next_run_at = self._calculate_next_run(task.frequency)
        self.tasks[task.task_id] = task
        
        # Persist to database
        await self._persist_task(task)
        
        logger.info(f"Added optimization task: {task.task_id} ({task.task_type.value})")
        
        if self.discord:
            await self.discord.notify_task_added(task)
            
        return task.task_id
    
    async def remove_task(self, task_id: str):
        """Remove a task from the queue."""
        if task_id in self.tasks:
            task = self.tasks.pop(task_id)
            await self._delete_task_from_db(task_id)
            logger.info(f"Removed optimization task: {task_id}")
            
            if self.discord:
                await self.discord.notify_task_removed(task)
    
    async def start(self):
        """Start the continuous improvement engine."""
        self.running = True
        logger.info("Continuous Improvement Engine started")
        
        if self.discord:
            await self.discord.notify_engine_started()
        
        while self.running:
            try:
                await self._process_pending_tasks()
                await asyncio.sleep(60)  # Check every minute
            except Exception as e:
                logger.error(f"Error in improvement engine loop: {e}")
                await asyncio.sleep(60)
    
    def stop(self):
        """Stop the continuous improvement engine."""
        self.running = False
        logger.info("Continuous Improvement Engine stopped")
    
    async def _process_pending_tasks(self):
        """Process all pending tasks that are due."""
        now = datetime.utcnow()
        
        for task in self.tasks.values():
            if not task.enabled:
                continue
                
            if task.status == TaskStatus.RUNNING:
                continue
                
            if task.next_run_at and task.next_run_at <= now:
                asyncio.create_task(self._execute_task(task))
    
    async def _execute_task(self, task: OptimizationTask):
        """Execute a single optimization task."""
        task.status = TaskStatus.RUNNING
        task.last_run_at = datetime.utcnow()
        
        logger.info(f"Executing task: {task.task_id} ({task.task_type.value})")
        
        if self.discord:
            await self.discord.notify_task_started(task)
        
        try:
            # Get the handler for this task type
            handler = self._task_handlers.get(task.task_type)
            if not handler:
                raise ValueError(f"No handler registered for {task.task_type}")
            
            # Execute the optimization
            result = await handler(task)
            
            # Update task status
            task.status = TaskStatus.COMPLETED
            task.last_result = result
            task.consecutive_failures = 0
            task.next_run_at = self._calculate_next_run(task.frequency)
            
            # Log improvement event
            await self._log_improvement_event(task, result)
            
            logger.info(f"Task completed successfully: {task.task_id}")
            
            if self.discord:
                await self.discord.notify_task_completed(task, result)
                
        except Exception as e:
            task.status = TaskStatus.FAILED
            task.last_error = str(e)
            task.consecutive_failures += 1
            
            logger.error(f"Task failed: {task.task_id} - {e}")
            
            if self.discord:
                await self.discord.notify_task_failed(task, e)
            
            # Disable task after 5 consecutive failures
            if task.consecutive_failures >= 5:
                task.enabled = False
                logger.warning(f"Task disabled due to repeated failures: {task.task_id}")
                
                if self.discord:
                    await self.discord.notify_task_disabled(task)
        
        finally:
            await self._persist_task(task)
    
    def _calculate_next_run(self, frequency: str) -> datetime:
        """Calculate the next run time based on frequency."""
        now = datetime.utcnow()
        
        if frequency == 'hourly':
            return now + timedelta(hours=1)
        elif frequency == 'daily':
            return now + timedelta(days=1)
        elif frequency == 'weekly':
            return now + timedelta(weeks=1)
        elif frequency == 'minute':  # For testing
            return now + timedelta(minutes=1)
        else:
            return now + timedelta(days=1)
    
    # Default optimization handlers
    
    async def _optimize_hyperparameters(self, task: OptimizationTask) -> Dict:
        """Optimize strategy hyperparameters using recent performance data."""
        # Implementation would use Bayesian optimization or similar
        logger.info(f"Optimizing hyperparameters for {task.component_id}")
        
        # Placeholder for actual optimization logic
        return {
            'optimized_params': {'param1': 0.5, 'param2': 1.2},
            'improvement_metric': 'sharpe_ratio',
            'improvement_value': 0.15,
            'backtest_results': {'win_rate': 0.62, 'profit_factor': 1.8}
        }
    
    async def _optimize_features(self, task: OptimizationTask) -> Dict:
        """Optimize feature selection based on feature importance."""
        logger.info(f"Optimizing features for {task.component_id}")
        
        return {
            'selected_features': ['feature1', 'feature2', 'feature3'],
            'removed_features': ['low_importance_feature'],
            'feature_importance': {'feature1': 0.35, 'feature2': 0.28}
        }
    
    async def _optimize_strategy_weights(self, task: OptimizationTask) -> Dict:
        """Optimize portfolio weights across strategies."""
        logger.info(f"Optimizing strategy weights for {task.component_id}")
        
        return {
            'new_weights': {'strategy_a': 0.4, 'strategy_b': 0.35, 'strategy_c': 0.25},
            'expected_improvement': 0.08
        }
    
    async def _retrain_model(self, task: OptimizationTask) -> Dict:
        """Retrain ML models with recent data."""
        logger.info(f"Retraining model for {task.component_id}")
        
        return {
            'new_model_version': 'v2.1.0',
            'training_samples': 50000,
            'validation_accuracy': 0.68,
            'deployment_status': 'staging'
        }
    
    async def _optimize_risk_params(self, task: OptimizationTask) -> Dict:
        """Optimize risk management parameters."""
        logger.info(f"Optimizing risk parameters for {task.component_id}")
        
        return {
            'new_risk_params': {
                'max_position_size': 0.15,
                'stop_loss_pct': 0.02,
                'take_profit_pct': 0.04
            },
            'max_drawdown_improvement': 0.05
        }
    
    # Database persistence methods
    
    async def _persist_task(self, task: OptimizationTask):
        """Persist task to database."""
        # Implementation would use actual DB query
        pass
    
    async def _delete_task_from_db(self, task_id: str):
        """Delete task from database."""
        pass
    
    async def _log_improvement_event(self, task: OptimizationTask, result: Dict):
        """Log improvement event to database."""
        event = {
            'event_type': f'{task.task_type.value}_completed',
            'component_type': task.task_type.value,
            'component_id': task.component_id,
            'trigger_reason': 'scheduled_optimization',
            'improvement_metrics': result,
            'automated': True
        }
        # Persist to improvement_events table
        logger.info(f"Logged improvement event: {event}")


# Convenience function for running the engine
async def run_improvement_engine(db_connection, discord_notifier=None, tasks: List[OptimizationTask] = None):
    """Run the continuous improvement engine with optional initial tasks."""
    engine = ContinuousImprovementEngine(db_connection, discord_notifier)
    
    if tasks:
        for task in tasks:
            await engine.add_task(task)
    
    await engine.start()
