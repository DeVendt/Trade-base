"""
Discord Notification System
Sends real-time updates about improvements, trades, and system events to Discord.
"""

import aiohttp
import logging
from datetime import datetime
from typing import Dict, Optional, List
from dataclasses import dataclass, asdict
from enum import Enum
import json

logger = logging.getLogger(__name__)


class NotificationType(Enum):
    INFO = "info"
    SUCCESS = "success"
    WARNING = "warning"
    ERROR = "error"
    TRADE = "trade"
    IMPROVEMENT = "improvement"


@dataclass
class DiscordEmbed:
    """Discord embed structure for rich notifications."""
    title: str
    description: str
    color: int = 0x3498db  # Default blue
    fields: List[Dict] = None
    timestamp: str = None
    footer: Dict = None
    
    def __post_init__(self):
        if self.fields is None:
            self.fields = []
        if self.timestamp is None:
            self.timestamp = datetime.utcnow().isoformat()


class DiscordWebhook:
    """Discord webhook client for sending notifications."""
    
    # Color codes for different notification types
    COLORS = {
        NotificationType.INFO: 0x3498db,      # Blue
        NotificationType.SUCCESS: 0x2ecc71,   # Green
        NotificationType.WARNING: 0xf39c12,   # Orange
        NotificationType.ERROR: 0xe74c3c,     # Red
        NotificationType.TRADE: 0x9b59b6,     # Purple
        NotificationType.IMPROVEMENT: 0x1abc9c  # Teal
    }
    
    def __init__(self, webhook_url: str, username: str = "TradeBase Bot", avatar_url: Optional[str] = None):
        self.webhook_url = webhook_url
        self.username = username
        self.avatar_url = avatar_url
        self._session: Optional[aiohttp.ClientSession] = None
    
    async def _get_session(self) -> aiohttp.ClientSession:
        """Get or create aiohttp session."""
        if self._session is None or self._session.closed:
            self._session = aiohttp.ClientSession()
        return self._session
    
    async def send_message(self, content: str, embeds: List[DiscordEmbed] = None) -> bool:
        """Send a message to Discord webhook."""
        try:
            session = await self._get_session()
            
            payload = {
                "username": self.username,
                "content": content,
                "avatar_url": self.avatar_url
            }
            
            if embeds:
                payload["embeds"] = [self._embed_to_dict(e) for e in embeds]
            
            async with session.post(self.webhook_url, json=payload) as response:
                if response.status in (200, 204):
                    logger.debug(f"Discord message sent successfully")
                    return True
                else:
                    logger.error(f"Failed to send Discord message: {response.status}")
                    return False
                    
        except Exception as e:
            logger.error(f"Error sending Discord message: {e}")
            return False
    
    def _embed_to_dict(self, embed: DiscordEmbed) -> Dict:
        """Convert DiscordEmbed to dictionary."""
        return {
            "title": embed.title,
            "description": embed.description,
            "color": embed.color,
            "fields": embed.fields,
            "timestamp": embed.timestamp,
            "footer": embed.footer
        }
    
    async def close(self):
        """Close the aiohttp session."""
        if self._session and not self._session.closed:
            await self._session.close()


class DiscordNotifier:
    """
    High-level Discord notifier for Trade Base events.
    Provides formatted notifications for all system events.
    """
    
    def __init__(self, webhook_url: str, notification_channel: str = "improvements"):
        self.webhook = DiscordWebhook(webhook_url)
        self.channel = notification_channel
        self.enabled = True
    
    # Engine Lifecycle Notifications
    
    async def notify_engine_started(self):
        """Notify when the improvement engine starts."""
        if not self.enabled:
            return
            
        embed = DiscordEmbed(
            title="🚀 Continuous Improvement Engine Started",
            description="The automated optimization system is now running.",
            color=DiscordWebhook.COLORS[NotificationType.SUCCESS]
        )
        embed.fields = [
            {"name": "Status", "value": "Running", "inline": True},
            {"name": "Mode", "value": "Automated", "inline": True}
        ]
        
        await self.webhook.send_message("", [embed])
    
    async def notify_engine_stopped(self):
        """Notify when the improvement engine stops."""
        if not self.enabled:
            return
            
        embed = DiscordEmbed(
            title="🛑 Continuous Improvement Engine Stopped",
            description="The automated optimization system has been stopped.",
            color=DiscordWebhook.COLORS[NotificationType.WARNING]
        )
        
        await self.webhook.send_message("", [embed])
    
    # Task Notifications
    
    async def notify_task_added(self, task):
        """Notify when a new optimization task is added."""
        if not self.enabled:
            return
            
        embed = DiscordEmbed(
            title="➕ New Optimization Task Added",
            description=f"Task `{task.task_id}` has been added to the queue.",
            color=DiscordWebhook.COLORS[NotificationType.INFO]
        )
        embed.fields = [
            {"name": "Type", "value": task.task_type.value, "inline": True},
            {"name": "Component", "value": task.component_id, "inline": True},
            {"name": "Frequency", "value": task.frequency, "inline": True}
        ]
        
        await self.webhook.send_message("", [embed])
    
    async def notify_task_removed(self, task):
        """Notify when a task is removed."""
        if not self.enabled:
            return
            
        embed = DiscordEmbed(
            title="➖ Optimization Task Removed",
            description=f"Task `{task.task_id}` has been removed.",
            color=DiscordWebhook.COLORS[NotificationType.WARNING]
        )
        
        await self.webhook.send_message("", [embed])
    
    async def notify_task_started(self, task):
        """Notify when a task starts execution."""
        if not self.enabled:
            return
            
        embed = DiscordEmbed(
            title="▶️ Optimization Started",
            description=f"Running {task.task_type.value} for `{task.component_id}`",
            color=DiscordWebhook.COLORS[NotificationType.INFO]
        )
        
        await self.webhook.send_message("", [embed])
    
    async def notify_task_completed(self, task, result: Dict):
        """Notify when a task completes successfully."""
        if not self.enabled:
            return
            
        embed = DiscordEmbed(
            title="✅ Optimization Completed",
            description=f"{task.task_type.value} for `{task.component_id}` finished successfully.",
            color=DiscordWebhook.COLORS[NotificationType.SUCCESS]
        )
        
        # Add result fields
        fields = []
        for key, value in result.items():
            if isinstance(value, (int, float, str)):
                fields.append({"name": key.replace('_', ' ').title(), "value": str(value), "inline": True})
        
        if fields:
            embed.fields = fields[:25]  # Discord limit
        
        await self.webhook.send_message("", [embed])
    
    async def notify_task_failed(self, task, error: Exception):
        """Notify when a task fails."""
        if not self.enabled:
            return
            
        embed = DiscordEmbed(
            title="❌ Optimization Failed",
            description=f"{task.task_type.value} for `{task.component_id}` failed.",
            color=DiscordWebhook.COLORS[NotificationType.ERROR]
        )
        embed.fields = [
            {"name": "Error", "value": str(error)[:1000], "inline": False},
            {"name": "Consecutive Failures", "value": str(task.consecutive_failures), "inline": True}
        ]
        
        await self.webhook.send_message("<@here>", [embed])  # Mention @here for failures
    
    async def notify_task_disabled(self, task):
        """Notify when a task is disabled due to repeated failures."""
        if not self.enabled:
            return
            
        embed = DiscordEmbed(
            title="🚫 Task Auto-Disabled",
            description=f"Task `{task.task_id}` has been disabled after {task.consecutive_failures} failures.",
            color=DiscordWebhook.COLORS[NotificationType.ERROR]
        )
        embed.fields = [
            {"name": "Component", "value": task.component_id, "inline": True},
            {"name": "Last Error", "value": str(task.last_error)[:500], "inline": False}
        ]
        
        await self.webhook.send_message("<@here>", [embed])
    
    # Trade Notifications
    
    async def notify_trade_executed(self, trade_data: Dict):
        """Notify when a trade is executed."""
        if not self.enabled:
            return
            
        pnl = trade_data.get('net_pnl', 0)
        is_win = pnl > 0
        
        embed = DiscordEmbed(
            title=f"{'🟢' if is_win else '🔴'} Trade {'WIN' if is_win else 'LOSS'}",
            description=f"{trade_data.get('direction')} {trade_data.get('symbol')} @ {trade_data.get('exit_price')}",
            color=0x2ecc71 if is_win else 0xe74c3c
        )
        embed.fields = [
            {"name": "P&L", "value": f"${pnl:,.2f}", "inline": True},
            {"name": "Strategy", "value": trade_data.get('strategy_id', 'N/A'), "inline": True},
            {"name": "Duration", "value": f"{trade_data.get('duration_seconds', 0)//60}m", "inline": True}
        ]
        
        await self.webhook.send_message("", [embed])
    
    # Performance Notifications
    
    async def notify_performance_degradation(self, strategy_id: str, metrics: Dict):
        """Notify when performance degrades."""
        if not self.enabled:
            return
            
        embed = DiscordEmbed(
            title="⚠️ Performance Degradation Detected",
            description=f"Strategy `{strategy_id}` is showing degraded performance.",
            color=DiscordWebhook.COLORS[NotificationType.WARNING]
        )
        embed.fields = [
            {"name": "Win Rate", "value": f"{metrics.get('win_rate', 0):.1%}", "inline": True},
            {"name": "Drawdown", "value": f"{metrics.get('max_drawdown', 0):.1%}", "inline": True},
            {"name": "Action", "value": "Auto-optimization triggered", "inline": False}
        ]
        
        await self.webhook.send_message("<@here>", [embed])
    
    async def notify_new_model_deployed(self, model_version: str, metrics: Dict):
        """Notify when a new model is deployed."""
        if not self.enabled:
            return
            
        embed = DiscordEmbed(
            title="🤖 New Model Deployed",
            description=f"Model version `{model_version}` is now in production.",
            color=DiscordWebhook.COLORS[NotificationType.IMPROVEMENT]
        )
        embed.fields = [
            {"name": "Accuracy", "value": f"{metrics.get('accuracy', 0):.1%}", "inline": True},
            {"name": "Precision", "value": f"{metrics.get('precision', 0):.1%}", "inline": True},
            {"name": "Recall", "value": f"{metrics.get('recall', 0):.1%}", "inline": True}
        ]
        
        await self.webhook.send_message("", [embed])
    
    async def notify_daily_summary(self, summary: Dict):
        """Send daily performance summary."""
        if not self.enabled:
            return
            
        net_pnl = summary.get('net_pnl', 0)
        
        embed = DiscordEmbed(
            title="📊 Daily Performance Summary",
            description=f"Trading summary for {datetime.now().strftime('%Y-%m-%d')}",
            color=0x3498db if net_pnl >= 0 else 0xe74c3c
        )
        embed.fields = [
            {"name": "Total Trades", "value": str(summary.get('total_trades', 0)), "inline": True},
            {"name": "Win Rate", "value": f"{summary.get('win_rate', 0):.1%}", "inline": True},
            {"name": "Net P&L", "value": f"${net_pnl:,.2f}", "inline": True},
            {"name": "Sharpe Ratio", "value": f"{summary.get('sharpe_ratio', 0):.2f}", "inline": True},
            {"name": "Max Drawdown", "value": f"{summary.get('max_drawdown', 0):.1%}", "inline": True}
        ]
        
        await self.webhook.send_message("", [embed])
    
    # Futures-Specific Notifications
    
    async def notify_contract_rollover(self, symbol: str, current_contract: str, new_contract: str, days_to_expiry: int):
        """Notify when futures contract rollover is approaching."""
        if not self.enabled:
            return
        
        # Determine severity based on days remaining
        if days_to_expiry <= 2:
            color = 0xe74c3c  # Red - urgent
            mention = "<@here>"
            status = "🚨 URGENT: New entries paused"
        elif days_to_expiry <= 5:
            color = 0xf39c12  # Orange - warning
            mention = ""
            status = "⚠️ WARNING: Reduce position size"
        else:
            color = 0x3498db  # Blue - info
            mention = ""
            status = "ℹ️ INFO: Plan rollover"
        
        embed = DiscordEmbed(
            title=f"📅 Contract Rollover Approaching - {symbol}",
            description=f"Current contract expires in {days_to_expiry} days",
            color=color
        )
        embed.fields = [
            {"name": "Symbol", "value": symbol, "inline": True},
            {"name": "Current Contract", "value": current_contract, "inline": True},
            {"name": "New Contract", "value": new_contract, "inline": True},
            {"name": "Days to Expiry", "value": str(days_to_expiry), "inline": True},
            {"name": "Status", "value": status, "inline": False}
        ]
        
        await self.webhook.send_message(mention, [embed])
    
    async def notify_margin_call(self, account: str, margin_used: float, margin_available: float, margin_percent: float):
        """Notify when margin usage is critical."""
        if not self.enabled:
            return
        
        embed = DiscordEmbed(
            title="🚨 MARGIN CALL WARNING",
            description=f"Account `{account}` margin usage is critical!",
            color=0xe74c3c
        )
        embed.fields = [
            {"name": "Account", "value": account, "inline": True},
            {"name": "Margin Used", "value": f"${margin_used:,.2f}", "inline": True},
            {"name": "Margin Available", "value": f"${margin_available:,.2f}", "inline": True},
            {"name": "Usage %", "value": f"{margin_percent:.1f}%", "inline": True},
            {"name": "Action", "value": "🛑 Reducing position size automatically", "inline": False}
        ]
        
        await self.webhook.send_message("<@here>", [embed])
    
    async def notify_circuit_breaker(self, reason: str, daily_pnl: float, daily_loss_limit: float, account_value: float):
        """Notify when circuit breaker triggers and trading is halted."""
        if not self.enabled:
            return
        
        embed = DiscordEmbed(
            title="⛔ CIRCUIT BREAKER TRIGGERED",
            description="Trading has been automatically halted",
            color=0xe74c3c
        )
        embed.fields = [
            {"name": "Reason", "value": reason, "inline": False},
            {"name": "Daily P&L", "value": f"${daily_pnl:,.2f}", "inline": True},
            {"name": "Loss Limit", "value": f"${daily_loss_limit:,.2f}", "inline": True},
            {"name": "Account Value", "value": f"${account_value:,.2f}", "inline": True},
            {"name": "Action Required", "value": "🔒 Manual review needed to resume trading", "inline": False}
        ]
        
        await self.webhook.send_message("<@here>", [embed])
    
    async def notify_slippage_alert(self, symbol: str, expected_price: float, actual_price: float, slippage_ticks: float, order_type: str):
        """Notify when slippage exceeds acceptable threshold."""
        if not self.enabled:
            return
        
        slippage_cost = abs(actual_price - expected_price)
        
        embed = DiscordEmbed(
            title=f"⚠️ High Slippage Detected - {symbol}",
            description=f"Execution quality degraded for {order_type} order",
            color=0xf39c12
        )
        embed.fields = [
            {"name": "Symbol", "value": symbol, "inline": True},
            {"name": "Expected Price", "value": f"${expected_price:,.2f}", "inline": True},
            {"name": "Actual Price", "value": f"${actual_price:,.2f}", "inline": True},
            {"name": "Slippage", "value": f"{slippage_ticks:.1f} ticks (${slippage_cost:.2f})", "inline": True},
            {"name": "Recommendation", "value": "Consider using limit orders or avoid high volatility periods", "inline": False}
        ]
        
        await self.webhook.send_message("", [embed])
    
    async def notify_session_change(self, symbol: str, previous_session: str, new_session: str, liquidity_score: float):
        """Notify when trading session changes (RTH, overnight, etc.)."""
        if not self.enabled:
            return
        
        session_emoji = {
            "RTH": "☀️",
            "PreMarket": "🌅",
            "PostMarket": "🌆",
            "Overnight": "🌙",
            "Asian": "🌏",
            "European": "🌍"
        }
        
        prev_emoji = session_emoji.get(previous_session, "⏰")
        new_emoji = session_emoji.get(new_session, "⏰")
        
        embed = DiscordEmbed(
            title=f"🕐 Session Change - {symbol}",
            description=f"{prev_emoji} {previous_session} → {new_emoji} {new_session}",
            color=0x9b59b6
        )
        embed.fields = [
            {"name": "Symbol", "value": symbol, "inline": True},
            {"name": "New Session", "value": new_session, "inline": True},
            {"name": "Liquidity Score", "value": f"{liquidity_score:.0f}/100", "inline": True}
        ]
        
        # Add warning for low liquidity sessions
        if liquidity_score < 50:
            embed.fields.append({
                "name": "⚠️ Warning",
                "value": "Low liquidity period - spreads may widen",
                "inline": False
            })
        
        await self.webhook.send_message("", [embed])
    
    async def notify_connection_status(self, status: str, latency_ms: float, reconnect_attempt: int = 0):
        """Notify about NinjaTrader connection status."""
        if not self.enabled:
            return
        
        status_colors = {
            "connected": 0x2ecc71,
            "disconnected": 0xe74c3c,
            "reconnecting": 0xf39c12,
            "degraded": 0xe67e22
        }
        
        status_emojis = {
            "connected": "✅",
            "disconnected": "❌",
            "reconnecting": "🔄",
            "degraded": "⚡"
        }
        
        color = status_colors.get(status, 0x3498db)
        emoji = status_emojis.get(status, "ℹ️")
        
        embed = DiscordEmbed(
            title=f"{emoji} NinjaTrader Connection {status.title()}",
            description=f"Connection status update",
            color=color
        )
        embed.fields = [
            {"name": "Status", "value": status.title(), "inline": True},
            {"name": "Latency", "value": f"{latency_ms:.1f}ms", "inline": True}
        ]
        
        if reconnect_attempt > 0:
            embed.fields.append({
                "name": "Reconnect Attempt",
                "value": f"#{reconnect_attempt}",
                "inline": True
            })
        
        if latency_ms > 100:
            embed.fields.append({
                "name": "⚠️ Warning",
                "value": "High latency detected - execution quality may be affected",
                "inline": False
            })
        
        mention = "<@here>" if status in ("disconnected", "degraded") else ""
        await self.webhook.send_message(mention, [embed])
    
    async def notify_position_scaled(self, symbol: str, action: str, contracts: int, new_total: int, avg_price: float, reason: str):
        """Notify when position is scaled in or out."""
        if not self.enabled:
            return
        
        action_emojis = {
            "scale_in": "📈",
            "scale_out": "📉",
            "breakeven_stop": "🛡️",
            "trailing_stop": "🎯"
        }
        
        emoji = action_emojis.get(action, "📝")
        
        embed = DiscordEmbed(
            title=f"{emoji} Position Scaled - {symbol}",
            description=f"{action.replace('_', ' ').title()}: {contracts} contracts",
            color=0x1abc9c
        )
        embed.fields = [
            {"name": "Symbol", "value": symbol, "inline": True},
            {"name": "Action", "value": action.replace('_', ' ').title(), "inline": True},
            {"name": "Contracts", "value": str(contracts), "inline": True},
            {"name": "New Total", "value": str(new_total), "inline": True},
            {"name": "Avg Price", "value": f"${avg_price:,.2f}", "inline": True},
            {"name": "Reason", "value": reason, "inline": False}
        ]
        
        await self.webhook.send_message("", [embed])
    
    async def notify_weekly_summary(self, summary: Dict):
        """Send weekly performance summary."""
        if not self.enabled:
            return
        
        net_pnl = summary.get('net_pnl', 0)
        is_profitable = net_pnl >= 0
        
        embed = DiscordEmbed(
            title=f"{'📈' if is_profitable else '📉'} Weekly Performance Summary",
            description=f"Week of {summary.get('week_start', 'Unknown')} to {summary.get('week_end', 'Unknown')}",
            color=0x2ecc71 if is_profitable else 0xe74c3c
        )
        embed.fields = [
            {"name": "Total Trades", "value": str(summary.get('total_trades', 0)), "inline": True},
            {"name": "Win Rate", "value": f"{summary.get('win_rate', 0):.1%}", "inline": True},
            {"name": "Net P&L", "value": f"${net_pnl:,.2f}", "inline": True},
            {"name": "Gross Profit", "value": f"${summary.get('gross_profit', 0):,.2f}", "inline": True},
            {"name": "Gross Loss", "value": f"${summary.get('gross_loss', 0):,.2f}", "inline": True},
            {"name": "Profit Factor", "value": f"{summary.get('profit_factor', 0):.2f}", "inline": True},
            {"name": "Best Trade", "value": f"${summary.get('best_trade', 0):,.2f}", "inline": True},
            {"name": "Worst Trade", "value": f"${summary.get('worst_trade', 0):,.2f}", "inline": True},
            {"name": "Avg Trade", "value": f"${summary.get('avg_trade', 0):,.2f}", "inline": True}
        ]
        
        await self.webhook.send_message("", [embed])
    
    # System Notifications
    
    async def notify_system_alert(self, title: str, message: str, severity: NotificationType = NotificationType.WARNING):
        """Send a generic system alert."""
        if not self.enabled:
            return
            
        embed = DiscordEmbed(
            title=title,
            description=message,
            color=DiscordWebhook.COLORS[severity]
        )
        
        mention = "<@here>" if severity in (NotificationType.ERROR, NotificationType.WARNING) else ""
        await self.webhook.send_message(mention, [embed])
    
    async def close(self):
        """Close the webhook connection."""
        await self.webhook.close()


# Convenience function for quick notifications
async def send_discord_notification(webhook_url: str, message: str, notification_type: NotificationType = NotificationType.INFO):
    """Send a simple Discord notification."""
    webhook = DiscordWebhook(webhook_url)
    
    embed = DiscordEmbed(
        title="TradeBase Notification",
        description=message,
        color=DiscordWebhook.COLORS[notification_type]
    )
    
    await webhook.send_message("", [embed])
    await webhook.close()
