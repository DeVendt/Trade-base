"""
Continuous Improvement Engine for Trade Base
Automates recurring optimization based on trade outcomes and performance metrics.
"""

from .continuous_improvement_engine import ContinuousImprovementEngine, OptimizationTask, OptimizationType
from .optimization_runner import OptimizationRunner
from .performance_analyzer import PerformanceAnalyzer

__all__ = ['ContinuousImprovementEngine', 'OptimizationRunner', 'PerformanceAnalyzer', 'OptimizationTask', 'OptimizationType']
