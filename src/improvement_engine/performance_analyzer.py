"""
Performance Analyzer
Analyzes trading performance and identifies patterns for improvement.
"""

import logging
from datetime import datetime, timedelta
from typing import Dict, List, Optional, Tuple
from dataclasses import dataclass
from statistics import mean, stdev
import numpy as np

logger = logging.getLogger(__name__)


@dataclass
class PerformanceSnapshot:
    """Snapshot of performance metrics at a point in time."""
    timestamp: datetime
    win_rate: float
    profit_factor: float
    sharpe_ratio: float
    max_drawdown: float
    total_trades: int
    net_pnl: float


@dataclass
class PerformanceTrend:
    """Trend analysis for performance metrics."""
    metric_name: str
    direction: str  # 'improving', 'declining', 'stable'
    change_percent: float
    volatility: float
    recent_average: float
    historical_average: float


class PerformanceAnalyzer:
    """
    Analyzes trading performance metrics and identifies trends.
    
    Features:
    - Trend detection for key metrics
    - Regime-specific performance analysis
    - Drawdown analysis
    - Trade distribution analysis
    """
    
    def __init__(self, db_connection):
        self.db = db_connection
        self.history: List[PerformanceSnapshot] = []
        
    async def analyze_recent_performance(
        self,
        strategy_id: Optional[str] = None,
        days: int = 30
    ) -> Dict:
        """
        Perform comprehensive performance analysis.
        
        Returns:
            Dictionary with all analysis results
        """
        end_date = datetime.utcnow()
        start_date = end_date - timedelta(days=days)
        
        results = {
            'period': f"{start_date.date()} to {end_date.date()}",
            'summary': {},
            'trends': {},
            'alerts': [],
            'recommendations': []
        }
        
        # Get basic metrics
        metrics = await self._get_metrics(strategy_id, start_date, end_date)
        results['summary'] = metrics
        
        # Analyze trends
        trends = await self._analyze_trends(strategy_id)
        results['trends'] = trends
        
        # Generate alerts
        alerts = self._generate_alerts(metrics, trends)
        results['alerts'] = alerts
        
        # Generate recommendations
        recommendations = self._generate_recommendations(metrics, trends, alerts)
        results['recommendations'] = recommendations
        
        return results
    
    async def _get_metrics(
        self,
        strategy_id: Optional[str],
        start_date: datetime,
        end_date: datetime
    ) -> Dict:
        """Get basic performance metrics."""
        # Query database for metrics
        # This is a placeholder - implement actual queries
        
        return {
            'total_trades': 150,
            'winning_trades': 82,
            'losing_trades': 68,
            'win_rate': 0.547,
            'gross_profit': 12500,
            'gross_loss': 6800,
            'net_pnl': 5700,
            'profit_factor': 1.84,
            'average_win': 152.44,
            'average_loss': 100.0,
            'largest_win': 850,
            'largest_loss': 420,
            'sharpe_ratio': 1.35,
            'sortino_ratio': 1.95,
            'max_drawdown': 0.085,
            'max_drawdown_duration_days': 5,
            'calmar_ratio': 2.1
        }
    
    async def _analyze_trends(
        self,
        strategy_id: Optional[str]
    ) -> Dict[str, PerformanceTrend]:
        """Analyze trends in key metrics."""
        trends = {}
        
        # Get historical snapshots
        snapshots = await self._get_historical_snapshots(strategy_id, days=90)
        
        if len(snapshots) < 7:
            logger.warning("Insufficient historical data for trend analysis")
            return trends
        
        # Analyze each metric
        metrics_to_track = ['win_rate', 'sharpe_ratio', 'max_drawdown', 'profit_factor']
        
        for metric in metrics_to_track:
            trend = self._calculate_trend(snapshots, metric)
            if trend:
                trends[metric] = trend
        
        return trends
    
    def _calculate_trend(
        self,
        snapshots: List[PerformanceSnapshot],
        metric: str
    ) -> Optional[PerformanceTrend]:
        """Calculate trend for a specific metric."""
        if len(snapshots) < 7:
            return None
        
        # Get values
        values = [getattr(s, metric) for s in snapshots]
        
        # Split into recent and historical
        mid_point = len(values) // 2
        recent = values[mid_point:]
        historical = values[:mid_point]
        
        if not recent or not historical:
            return None
        
        recent_avg = mean(recent)
        historical_avg = mean(historical)
        
        # Calculate change
        if historical_avg != 0:
            change_pct = ((recent_avg - historical_avg) / abs(historical_avg)) * 100
        else:
            change_pct = 0
        
        # Determine direction
        if abs(change_pct) < 5:
            direction = 'stable'
        elif change_pct > 0:
            direction = 'improving' if metric != 'max_drawdown' else 'declining'
        else:
            direction = 'declining' if metric != 'max_drawdown' else 'improving'
        
        # Calculate volatility
        if len(values) > 1:
            volatility = stdev(values) / abs(mean(values)) if mean(values) != 0 else 0
        else:
            volatility = 0
        
        return PerformanceTrend(
            metric_name=metric,
            direction=direction,
            change_percent=change_pct,
            volatility=volatility,
            recent_average=recent_avg,
            historical_average=historical_avg
        )
    
    def _generate_alerts(
        self,
        metrics: Dict,
        trends: Dict[str, PerformanceTrend]
    ) -> List[Dict]:
        """Generate alerts based on metrics and trends."""
        alerts = []
        
        # Win rate alerts
        win_rate = metrics.get('win_rate', 0.5)
        if win_rate < 0.45:
            alerts.append({
                'severity': 'critical',
                'metric': 'win_rate',
                'value': win_rate,
                'threshold': 0.45,
                'message': f'Win rate {win_rate:.1%} below critical threshold'
            })
        elif win_rate < 0.50:
            alerts.append({
                'severity': 'warning',
                'metric': 'win_rate',
                'value': win_rate,
                'threshold': 0.50,
                'message': f'Win rate {win_rate:.1%} below optimal'
            })
        
        # Drawdown alerts
        max_dd = metrics.get('max_drawdown', 0)
        if max_dd > 0.15:
            alerts.append({
                'severity': 'critical',
                'metric': 'max_drawdown',
                'value': max_dd,
                'threshold': 0.15,
                'message': f'Max drawdown {max_dd:.1%} exceeds 15% limit'
            })
        elif max_dd > 0.10:
            alerts.append({
                'severity': 'warning',
                'metric': 'max_drawdown',
                'value': max_dd,
                'threshold': 0.10,
                'message': f'Max drawdown {max_dd:.1%} elevated'
            })
        
        # Sharpe ratio alerts
        sharpe = metrics.get('sharpe_ratio', 1.0)
        if sharpe < 0.5:
            alerts.append({
                'severity': 'critical',
                'metric': 'sharpe_ratio',
                'value': sharpe,
                'threshold': 0.5,
                'message': f'Sharpe ratio {sharpe:.2f} critically low'
            })
        elif sharpe < 1.0:
            alerts.append({
                'severity': 'warning',
                'metric': 'sharpe_ratio',
                'value': sharpe,
                'threshold': 1.0,
                'message': f'Sharpe ratio {sharpe:.2f} below optimal'
            })
        
        # Trend-based alerts
        for metric, trend in trends.items():
            if trend.direction == 'declining' and abs(trend.change_percent) > 10:
                alerts.append({
                    'severity': 'warning',
                    'metric': metric,
                    'trend': trend.direction,
                    'change': f'{trend.change_percent:.1f}%',
                    'message': f'{metric} declining by {trend.change_percent:.1f}%'
                })
        
        return alerts
    
    def _generate_recommendations(
        self,
        metrics: Dict,
        trends: Dict[str, PerformanceTrend],
        alerts: List[Dict]
    ) -> List[str]:
        """Generate improvement recommendations."""
        recommendations = []
        
        # Based on alerts
        for alert in alerts:
            if alert['metric'] == 'win_rate' and alert['severity'] == 'critical':
                recommendations.append("Perform hyperparameter optimization immediately")
                recommendations.append("Review entry signal criteria")
            
            if alert['metric'] == 'max_drawdown' and alert['severity'] == 'critical':
                recommendations.append("Tighten stop-loss parameters")
                recommendations.append("Reduce position sizes")
            
            if alert['metric'] == 'sharpe_ratio' and alert['severity'] == 'critical':
                recommendations.append("Retrain prediction models")
                recommendations.append("Optimize strategy weights")
        
        # Based on trends
        for metric, trend in trends.items():
            if trend.direction == 'declining':
                if metric == 'win_rate':
                    recommendations.append("Analyze losing trades for pattern changes")
                elif metric == 'sharpe_ratio':
                    recommendations.append("Review risk-adjusted returns")
        
        # Based on current metrics
        profit_factor = metrics.get('profit_factor', 1.5)
        if profit_factor < 1.5:
            recommendations.append("Optimize take-profit targets")
        
        avg_win = metrics.get('average_win', 0)
        avg_loss = metrics.get('average_loss', 1)
        if avg_loss > 0 and avg_win / avg_loss < 1.5:
            recommendations.append("Improve risk/reward ratio (target 2:1 minimum)")
        
        return list(set(recommendations))  # Remove duplicates
    
    async def _get_historical_snapshots(
        self,
        strategy_id: Optional[str],
        days: int
    ) -> List[PerformanceSnapshot]:
        """Get historical performance snapshots."""
        # Query database for historical data
        # Placeholder implementation
        return []
    
    def calculate_consecutive_stats(self, trades: List[Dict]) -> Dict:
        """Calculate consecutive win/loss statistics."""
        if not trades:
            return {}
        
        # Sort by time
        sorted_trades = sorted(trades, key=lambda x: x.get('exit_time', ''))
        
        # Calculate streaks
        current_streak = 0
        current_type = None
        max_win_streak = 0
        max_loss_streak = 0
        
        streaks = []
        
        for trade in sorted_trades:
            pnl = trade.get('net_pnl', 0)
            trade_type = 'win' if pnl > 0 else 'loss'
            
            if trade_type == current_type:
                current_streak += 1
            else:
                if current_streak > 0:
                    streaks.append({'type': current_type, 'length': current_streak})
                current_streak = 1
                current_type = trade_type
            
            if trade_type == 'win':
                max_win_streak = max(max_win_streak, current_streak)
            else:
                max_loss_streak = max(max_loss_streak, current_streak)
        
        # Add final streak
        if current_streak > 0:
            streaks.append({'type': current_type, 'length': current_streak})
        
        return {
            'max_win_streak': max_win_streak,
            'max_loss_streak': max_loss_streak,
            'current_streak': current_streak,
            'current_streak_type': current_type,
            'total_streaks': len(streaks),
            'avg_streak_length': mean([s['length'] for s in streaks]) if streaks else 0
        }
    
    def analyze_time_patterns(self, trades: List[Dict]) -> Dict:
        """Analyze performance patterns by time of day, day of week."""
        patterns = {
            'by_hour': {},
            'by_day': {},
            'by_month': {}
        }
        
        for trade in trades:
            exit_time = trade.get('exit_time')
            if not exit_time:
                continue
            
            # Parse time
            try:
                dt = datetime.fromisoformat(exit_time.replace('Z', '+00:00'))
                hour = dt.hour
                weekday = dt.strftime('%A')
                month = dt.strftime('%B')
                
                pnl = trade.get('net_pnl', 0)
                
                # Aggregate by hour
                if hour not in patterns['by_hour']:
                    patterns['by_hour'][hour] = {'trades': 0, 'pnl': 0, 'wins': 0}
                patterns['by_hour'][hour]['trades'] += 1
                patterns['by_hour'][hour]['pnl'] += pnl
                if pnl > 0:
                    patterns['by_hour'][hour]['wins'] += 1
                
                # Aggregate by day
                if weekday not in patterns['by_day']:
                    patterns['by_day'][weekday] = {'trades': 0, 'pnl': 0, 'wins': 0}
                patterns['by_day'][weekday]['trades'] += 1
                patterns['by_day'][weekday]['pnl'] += pnl
                if pnl > 0:
                    patterns['by_day'][weekday]['wins'] += 1
                
            except (ValueError, AttributeError):
                continue
        
        # Calculate win rates
        for period in patterns:
            for key in patterns[period]:
                stats = patterns[period][key]
                if stats['trades'] > 0:
                    stats['win_rate'] = stats['wins'] / stats['trades']
                    stats['avg_pnl'] = stats['pnl'] / stats['trades']
        
        return patterns
