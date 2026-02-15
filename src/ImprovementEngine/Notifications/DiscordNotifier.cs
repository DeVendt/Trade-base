using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ImprovementEngine.Notifications
{
    /// <summary>
    /// Sends notifications to Discord about system improvements and alerts
    /// </summary>
    public class DiscordNotifier
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<DiscordNotifier> _logger;
        private readonly string _webhookUrl;
        private readonly string _environment;

        public DiscordNotifier(
            HttpClient httpClient,
            ILogger<DiscordNotifier> logger,
            string webhookUrl,
            string environment = "production")
        {
            _httpClient = httpClient;
            _logger = logger;
            _webhookUrl = webhookUrl;
            _environment = environment;
        }

        /// <summary>
        /// Send a model deployment notification
        /// </summary>
        public async Task<string> NotifyModelDeployedAsync(
            string modelVersion,
            string previousVersion,
            decimal improvementPercent,
            string sharpeRatio,
            string triggerReason)
        {
            var embed = new
            {
                title = "🚀 Model Deployed",
                color = 3066993, // Green
                fields = new[]
                {
                    new { name = "Model Version", value = $"`{modelVersion}`", inline = true },
                    new { name = "Previous Version", value = $"`{previousVersion}`", inline = true },
                    new { name = "Improvement", value = $"+{improvementPercent:F2}%", inline = true },
                    new { name = "Sharpe Ratio", value = sharpeRatio, inline = true },
                    new { name = "Trigger", value = triggerReason, inline = true },
                    new { name = "Environment", value = _environment, inline = true }
                },
                timestamp = DateTime.UtcNow.ToString("O"),
                footer = new { text = "AI Trading Platform - Improvement Engine" }
            };

            return await SendEmbedAsync(embed);
        }

        /// <summary>
        /// Send a performance degradation alert
        /// </summary>
        public async Task<string> NotifyPerformanceAlertAsync(
            string strategyId,
            string metric,
            decimal currentValue,
            decimal threshold,
            string action)
        {
            var embed = new
            {
                title = "⚠️ Performance Alert",
                color = 15158332, // Orange
                fields = new[]
                {
                    new { name = "Strategy", value = strategyId, inline = true },
                    new { name = "Metric", value = metric, inline = true },
                    new { name = "Current Value", value = currentValue.ToString("F4"), inline = true },
                    new { name = "Threshold", value = threshold.ToString("F4"), inline = true },
                    new { name = "Action Taken", value = action, inline = false }
                },
                timestamp = DateTime.UtcNow.ToString("O"),
                footer = new { text = "AI Trading Platform - Monitoring" }
            };

            return await SendEmbedAsync(embed);
        }

        /// <summary>
        /// Send a critical error alert requiring immediate attention
        /// </summary>
        public async Task<string> NotifyCriticalAlertAsync(
            string component,
            string error,
            string impact)
        {
            var embed = new
            {
                title = "🚨 Critical Alert",
                color = 15158332, // Red
                description = $"Component `{component}` has encountered a critical error.",
                fields = new[]
                {
                    new { name = "Error", value = $"```{error}```", inline = false },
                    new { name = "Impact", value = impact, inline = false },
                    new { name = "Time", value = $"<t:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:F>", inline = false }
                },
                timestamp = DateTime.UtcNow.ToString("O"),
                footer = new { text = "AI Trading Platform - IMMEDIATE ACTION REQUIRED" }
            };

            return await SendEmbedAsync(embed, mentionEveryone: true);
        }

        /// <summary>
        /// Send optimization results
        /// </summary>
        public async Task<string> NotifyOptimizationCompleteAsync(
            string taskType,
            string componentId,
            decimal improvementPercent,
            string details)
        {
            var embed = new
            {
                title = "🔧 Optimization Complete",
                color = 3447003, // Blue
                fields = new[]
                {
                    new { name = "Task", value = taskType, inline = true },
                    new { name = "Component", value = componentId, inline = true },
                    new { name = "Improvement", value = $"{improvementPercent:F2}%", inline = true },
                    new { name = "Details", value = details, inline = false }
                },
                timestamp = DateTime.UtcNow.ToString("O"),
                footer = new { text = "AI Trading Platform - Continuous Improvement" }
            };

            return await SendEmbedAsync(embed);
        }

        /// <summary>
        /// Send daily summary report
        /// </summary>
        public async Task<string> NotifyDailySummaryAsync(
            int tradesCount,
            decimal pnl,
            decimal winRate,
            string bestStrategy,
            string modelAccuracy)
        {
            var pnlEmoji = pnl >= 0 ? "📈" : "📉";
            var color = pnl >= 0 ? 3066993 : 15158332;

            var embed = new
            {
                title = $"{pnlEmoji} Daily Trading Summary",
                color = color,
                fields = new[]
                {
                    new { name = "Trades", value = tradesCount.ToString(), inline = true },
                    new { name = "P&L", value = $"${pnl:N2}", inline = true },
                    new { name = "Win Rate", value = $"{winRate:P1}", inline = true },
                    new { name = "Best Strategy", value = bestStrategy, inline = true },
                    new { name = "Model Accuracy", value = modelAccuracy, inline = true },
                    new { name = "Date", value = DateTime.UtcNow.ToString("yyyy-MM-dd"), inline = true }
                },
                timestamp = DateTime.UtcNow.ToString("O"),
                footer = new { text = "AI Trading Platform - Daily Report" }
            };

            return await SendEmbedAsync(embed);
        }

        /// <summary>
        /// Send A/B test results
        /// </summary>
        public async Task<string> NotifyABTestResultAsync(
            string testName,
            string controlVersion,
            string treatmentVersion,
            string winner,
            decimal improvementPercent,
            string action)
        {
            var embed = new
            {
                title = "🧪 A/B Test Result",
                color = winner == treatmentVersion ? 3066993 : 9807270,
                fields = new[]
                {
                    new { name = "Test", value = testName, inline = false },
                    new { name = "Control", value = $"`{controlVersion}`", inline = true },
                    new { name = "Treatment", value = $"`{treatmentVersion}`", inline = true },
                    new { name = "Winner", value = $"`{winner}`", inline = true },
                    new { name = "Improvement", value = $"{improvementPercent:F2}%", inline = true },
                    new { name = "Action", value = action, inline = false }
                },
                timestamp = DateTime.UtcNow.ToString("O"),
                footer = new { text = "AI Trading Platform - A/B Testing" }
            };

            return await SendEmbedAsync(embed);
        }

        /// <summary>
        /// Send rollback notification
        /// </summary>
        public async Task<string> NotifyRollbackAsync(
            string component,
            string fromVersion,
            string toVersion,
            string reason)
        {
            var embed = new
            {
                title = "↩️ Rollback Executed",
                color = 9807270, // Grey
                fields = new[]
                {
                    new { name = "Component", value = component, inline = true },
                    new { name = "From", value = $"`{fromVersion}`", inline = true },
                    new { name = "To", value = $"`{toVersion}`", inline = true },
                    new { name = "Reason", value = reason, inline = false }
                },
                timestamp = DateTime.UtcNow.ToString("O"),
                footer = new { text = "AI Trading Platform - Rollback System" }
            };

            return await SendEmbedAsync(embed);
        }

        /// <summary>
        /// Send feature importance update
        /// </summary>
        public async Task<string> NotifyFeatureImportanceAsync(
            string modelVersion,
            string topFeatures,
            string removedFeatures)
        {
            var embed = new
            {
                title = "📊 Feature Analysis Update",
                color = 3447003,
                fields = new[]
                {
                    new { name = "Model", value = $"`{modelVersion}`", inline = true },
                    new { name = "Top Features", value = topFeatures, inline = false },
                    new { name = "Removed Features", value = removedFeatures, inline = false }
                },
                timestamp = DateTime.UtcNow.ToString("O"),
                footer = new { text = "AI Trading Platform - Feature Engineering" }
            };

            return await SendEmbedAsync(embed);
        }

        /// <summary>
        /// Send contract rollover warning
        /// </summary>
        public async Task<string> NotifyContractRolloverAsync(
            string symbol,
            string currentContract,
            string newContract,
            int daysToExpiry)
        {
            var (color, status, mention) = daysToExpiry switch
            {
                <= 2 => (15158332, "🚨 URGENT: New entries paused", true),
                <= 5 => (15158332, "⚠️ WARNING: Reduce position size", false),
                _ => (3447003, "ℹ️ INFO: Plan rollover", false)
            };

            var embed = new
            {
                title = $"📅 Contract Rollover Approaching - {symbol}",
                description = $"Current contract expires in {daysToExpiry} days",
                color = color,
                fields = new[]
                {
                    new { name = "Symbol", value = symbol, inline = true },
                    new { name = "Current Contract", value = $"`{currentContract}`", inline = true },
                    new { name = "New Contract", value = $"`{newContract}`", inline = true },
                    new { name = "Days to Expiry", value = daysToExpiry.ToString(), inline = true },
                    new { name = "Status", value = status, inline = false }
                },
                timestamp = DateTime.UtcNow.ToString("O"),
                footer = new { text = "Futures Trading - Contract Management" }
            };

            return await SendEmbedAsync(embed, mentionEveryone: mention);
        }

        /// <summary>
        /// Send margin call warning
        /// </summary>
        public async Task<string> NotifyMarginCallAsync(
            string account,
            decimal marginUsed,
            decimal marginAvailable,
            decimal marginPercent)
        {
            var embed = new
            {
                title = "🚨 MARGIN CALL WARNING",
                description = $"Account `{account}` margin usage is critical!",
                color = 15158332, // Red
                fields = new[]
                {
                    new { name = "Account", value = account, inline = true },
                    new { name = "Margin Used", value = $"${marginUsed:N2}", inline = true },
                    new { name = "Margin Available", value = $"${marginAvailable:N2}", inline = true },
                    new { name = "Usage %", value = $"{marginPercent:F1}%", inline = true },
                    new { name = "Action", value = "🛑 Reducing position size automatically", inline = false }
                },
                timestamp = DateTime.UtcNow.ToString("O"),
                footer = new { text = "Risk Management - IMMEDIATE ACTION" }
            };

            return await SendEmbedAsync(embed, mentionEveryone: true);
        }

        /// <summary>
        /// Send circuit breaker triggered notification
        /// </summary>
        public async Task<string> NotifyCircuitBreakerAsync(
            string reason,
            decimal dailyPnL,
            decimal dailyLossLimit,
            decimal accountValue)
        {
            var embed = new
            {
                title = "⛔ CIRCUIT BREAKER TRIGGERED",
                description = "Trading has been automatically halted",
                color = 15158332, // Red
                fields = new[]
                {
                    new { name = "Reason", value = reason, inline = false },
                    new { name = "Daily P&L", value = $"${dailyPnL:N2}", inline = true },
                    new { name = "Loss Limit", value = $"${dailyLossLimit:N2}", inline = true },
                    new { name = "Account Value", value = $"${accountValue:N2}", inline = true },
                    new { name = "Action Required", value = "🔒 Manual review needed to resume trading", inline = false }
                },
                timestamp = DateTime.UtcNow.ToString("O"),
                footer = new { text = "Circuit Breaker System - Trading Halted" }
            };

            return await SendEmbedAsync(embed, mentionEveryone: true);
        }

        /// <summary>
        /// Send slippage alert
        /// </summary>
        public async Task<string> NotifySlippageAlertAsync(
            string symbol,
            decimal expectedPrice,
            decimal actualPrice,
            decimal slippageTicks,
            string orderType)
        {
            var slippageCost = Math.Abs(actualPrice - expectedPrice);

            var embed = new
            {
                title = $"⚠️ High Slippage Detected - {symbol}",
                description = $"Execution quality degraded for {orderType} order",
                color = 15158332, // Orange
                fields = new[]
                {
                    new { name = "Symbol", value = symbol, inline = true },
                    new { name = "Expected Price", value = $"${expectedPrice:N2}", inline = true },
                    new { name = "Actual Price", value = $"${actualPrice:N2}", inline = true },
                    new { name = "Slippage", value = $"{slippageTicks:F1} ticks (${slippageCost:N2})", inline = true },
                    new { name = "Recommendation", value = "Consider using limit orders or avoid high volatility periods", inline = false }
                },
                timestamp = DateTime.UtcNow.ToString("O"),
                footer = new { text = "Execution Quality Monitor" }
            };

            return await SendEmbedAsync(embed);
        }

        /// <summary>
        /// Send session change notification
        /// </summary>
        public async Task<string> NotifySessionChangeAsync(
            string symbol,
            string previousSession,
            string newSession,
            double liquidityScore)
        {
            var sessionEmojis = new Dictionary<string, string>
            {
                ["RTH"] = "☀️",
                ["PreMarket"] = "🌅",
                ["PostMarket"] = "🌆",
                ["Overnight"] = "🌙",
                ["Asian"] = "🌏",
                ["European"] = "🌍"
            };

            var prevEmoji = sessionEmojis.GetValueOrDefault(previousSession, "⏰");
            var newEmoji = sessionEmojis.GetValueOrDefault(newSession, "⏰");

            var fields = new List<object>
            {
                new { name = "Symbol", value = symbol, inline = true },
                new { name = "New Session", value = newSession, inline = true },
                new { name = "Liquidity Score", value = $"{liquidityScore:F0}/100", inline = true }
            };

            if (liquidityScore < 50)
            {
                fields.Add(new { name = "⚠️ Warning", value = "Low liquidity period - spreads may widen", inline = false });
            }

            var embed = new
            {
                title = $"🕐 Session Change - {symbol}",
                description = $"{prevEmoji} {previousSession} → {newEmoji} {newSession}",
                color = 10181046, // Purple
                fields = fields.ToArray(),
                timestamp = DateTime.UtcNow.ToString("O"),
                footer = new { text = "Session Monitor" }
            };

            return await SendEmbedAsync(embed);
        }

        /// <summary>
        /// Send connection status update
        /// </summary>
        public async Task<string> NotifyConnectionStatusAsync(
            string status,
            double latencyMs,
            int reconnectAttempt = 0)
        {
            var statusColors = new Dictionary<string, int>
            {
                ["connected"] = 3066993,
                ["disconnected"] = 15158332,
                ["reconnecting"] = 15158332,
                ["degraded"] = 15158332
            };

            var statusEmojis = new Dictionary<string, string>
            {
                ["connected"] = "✅",
                ["disconnected"] = "❌",
                ["reconnecting"] = "🔄",
                ["degraded"] = "⚡"
            };

            var color = statusColors.GetValueOrDefault(status, 3447003);
            var emoji = statusEmojis.GetValueOrDefault(status, "ℹ️");

            var fields = new List<object>
            {
                new { name = "Status", value = status, inline = true },
                new { name = "Latency", value = $"{latencyMs:F1}ms", inline = true }
            };

            if (reconnectAttempt > 0)
            {
                fields.Add(new { name = "Reconnect Attempt", value = $"#{reconnectAttempt}", inline = true });
            }

            if (latencyMs > 100)
            {
                fields.Add(new { name = "⚠️ Warning", value = "High latency detected - execution quality may be affected", inline = false });
            }

            var embed = new
            {
                title = $"{emoji} NinjaTrader Connection {status}",
                description = "Connection status update",
                color = color,
                fields = fields.ToArray(),
                timestamp = DateTime.UtcNow.ToString("O"),
                footer = new { text = "Connection Monitor" }
            };

            var mention = status is "disconnected" or "degraded";
            return await SendEmbedAsync(embed, mentionEveryone: mention);
        }

        /// <summary>
        /// Send position scaled notification
        /// </summary>
        public async Task<string> NotifyPositionScaledAsync(
            string symbol,
            string action,
            int contracts,
            int newTotal,
            decimal avgPrice,
            string reason)
        {
            var actionEmojis = new Dictionary<string, string>
            {
                ["scale_in"] = "📈",
                ["scale_out"] = "📉",
                ["breakeven_stop"] = "🛡️",
                ["trailing_stop"] = "🎯"
            };

            var emoji = actionEmojis.GetValueOrDefault(action, "📝");

            var embed = new
            {
                title = $"{emoji} Position Scaled - {symbol}",
                description = $"{action.Replace("_", " ")}: {contracts} contracts",
                color = 3066993, // Teal
                fields = new[]
                {
                    new { name = "Symbol", value = symbol, inline = true },
                    new { name = "Action", value = action.Replace("_", " "), inline = true },
                    new { name = "Contracts", value = contracts.ToString(), inline = true },
                    new { name = "New Total", value = newTotal.ToString(), inline = true },
                    new { name = "Avg Price", value = $"${avgPrice:N2}", inline = true },
                    new { name = "Reason", value = reason, inline = false }
                },
                timestamp = DateTime.UtcNow.ToString("O"),
                footer = new { text = "Position Management" }
            };

            return await SendEmbedAsync(embed);
        }

        /// <summary>
        /// Send weekly summary
        /// </summary>
        public async Task<string> NotifyWeeklySummaryAsync(
            int tradesCount,
            decimal winRate,
            decimal netPnL,
            decimal grossProfit,
            decimal grossLoss,
            decimal profitFactor,
            string weekStart,
            string weekEnd)
        {
            var pnlEmoji = netPnL >= 0 ? "📈" : "📉";
            var color = netPnL >= 0 ? 3066993 : 15158332;

            var embed = new
            {
                title = $"{pnlEmoji} Weekly Performance Summary",
                description = $"Week of {weekStart} to {weekEnd}",
                color = color,
                fields = new[]
                {
                    new { name = "Total Trades", value = tradesCount.ToString(), inline = true },
                    new { name = "Win Rate", value = $"{winRate:P1}", inline = true },
                    new { name = "Net P&L", value = $"${netPnL:N2}", inline = true },
                    new { name = "Gross Profit", value = $"${grossProfit:N2}", inline = true },
                    new { name = "Gross Loss", value = $"${grossLoss:N2}", inline = true },
                    new { name = "Profit Factor", value = $"{profitFactor:F2}", inline = true }
                },
                timestamp = DateTime.UtcNow.ToString("O"),
                footer = new { text = "Weekly Report" }
            };

            return await SendEmbedAsync(embed);
        }

        private async Task<string> SendEmbedAsync(object embed, bool mentionEveryone = false)
        {
            try
            {
                var payload = new
                {
                    content = mentionEveryone ? "@everyone" : null,
                    embeds = new[] { embed }
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(_webhookUrl, content);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Discord notification failed: {StatusCode} - {Response}",
                        response.StatusCode, await response.Content.ReadAsStringAsync());
                    return null;
                }

                // Extract message ID from response if possible
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Discord notification sent successfully");
                
                return responseContent; // Contains message ID
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send Discord notification");
                return null;
            }
        }
    }
}
