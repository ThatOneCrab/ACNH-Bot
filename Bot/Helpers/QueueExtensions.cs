using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Discord.Net;
using NHSE.Core;
using System.Threading.Tasks;
using System.Linq;
using System;
using System.Collections.Concurrent;

namespace SysBot.ACNHOrders
{
    public static class QueueExtensions
    {
        const int ArriveTime = 90;
        const int SetupTime = 95;

        
        public static async Task AddToQueueAsync(this SocketCommandContext Context, OrderRequest<Item> itemReq, string player, SocketUser trader)
        {
            IUserMessage test;
            try
            {
                var helperEmbed = new EmbedBuilder()
                    .WithTitle("Queue Notification")
                    .WithDescription("I've added you to the queue! I'll message you here when your order is ready.")
                    .WithColor(Color.Green)
                    .WithFooter(footer => footer.WithText("ACNH Orders"))
                    .WithCurrentTimestamp()
                    .Build();

                test = await trader.SendMessageAsync(embed: helperEmbed).ConfigureAwait(false);
            }
            catch (HttpException ex)
            {
                var noAccessMsg = Context.User == trader
                    ? "You must enable private messages in order to be queued!"
                    : $"{player} must enable private messages in order for them to be queued!";

                var errorEmbed = new EmbedBuilder()
                    .WithTitle("Queue Error")
                    .WithDescription($"{ex.HttpCode}: {ex.Reason}!")
                    .WithColor(Color.Red)
                    .AddField("Action", noAccessMsg, inline: false)
                    .WithFooter(footer => footer.WithText("ACNH Orders"))
                    .WithCurrentTimestamp()
                    .Build();

                await Context.Channel.SendMessageAsync(embed: errorEmbed).ConfigureAwait(false);
                return;
            }

            // Try adding and receive a pre-built Embed describing the result
            var result = AttemptAddToQueue(itemReq, trader.Mention, trader.Username, out var statusEmbed);

            // Notify in channel and DM using the same embed
            await Context.Channel.SendMessageAsync(embed: statusEmbed).ConfigureAwait(false);
            await trader.SendMessageAsync(embed: statusEmbed).ConfigureAwait(false);

            // Clean Up
            if (result)
            {
                // Delete the user's join message for privacy
                if (!Context.IsPrivate)
                    await Context.Message.DeleteAsync(RequestOptions.Default).ConfigureAwait(false);
            }
            else
            {
                // Delete our "I'm adding you!" DM to avoid confusion when add failed
                await test.DeleteAsync().ConfigureAwait(false);
            }
        }

        public static bool AddToQueueSync(IACNHOrderNotifier<Item> itemReq, string playerMention, string playerNameId, out string msg)
        {
            var result = AttemptAddToQueue(itemReq, playerMention, playerNameId, out var embed);
            // Fallback to description for backward-compatible message content
            msg = embed?.Description ?? string.Empty;

            return result;
        }

        // this sucks
        private static bool AttemptAddToQueue(IACNHOrderNotifier<Item> itemReq, string traderMention, string traderDispName, out Embed statusEmbed)
        {
                    var orders = Globals.Hub.Orders;
                    var orderArray = orders.ToArray();
                    var order = Array.Find(orderArray, x => x.UserGuid == itemReq.UserGuid);
                    if (order != null)
                    {
                        var eb = new EmbedBuilder()
                            .WithTitle("Queue Error")
                            .WithColor(Color.Red)
                            .WithFooter(footer => footer.WithText("ACNH Orders"))
                            .WithCurrentTimestamp();

                        if (!order.SkipRequested)
                        {
                            eb.WithDescription($"{traderMention} - Sorry, you are already in the queue.");
                        }
                        else
                        {
                            eb.WithDescription($"{traderMention} - You have been recently removed from the queue. Please wait a while before attempting to enter the queue again.");
                        }

                        // Optionally add an action field to guide the user
                        eb.AddField("Action", "If you believe this is a mistake, wait a few moments and try again or contact a moderator.", inline: false);

                        statusEmbed = eb.Build();
                        return false;
            }

            if (Globals.Bot.CurrentUserName == traderDispName)
            {
                var eb = new EmbedBuilder()
                    .WithTitle("Queue Error")
                    .WithColor(Color.Red)
                    .WithDescription($"{traderMention} - Failed to queue your order as it is the current processing order. Please wait a few seconds for the queue to clear if you've already completed it.")
                    .WithFooter(footer => footer.WithText("ACNH Orders"))
                    .WithCurrentTimestamp()
                    .Build();

                statusEmbed = eb;
                return false;
            }

            var position = orderArray.Length + 1;
            var idToken = Globals.Bot.Config.OrderConfig.ShowIDs ? $" (ID {itemReq.OrderID})" : string.Empty;
            var baseMsg = $"{traderMention} - Added you to the order queue{idToken}. Your position is: **{position}**";

            if (position > 1)
                baseMsg += $". Your predicted ETA is {GetETA(position)}";
            else
                baseMsg += ". Your order will start after the current order is complete!";

            var successBuilder = new EmbedBuilder()
                .WithTitle("Added to Queue")
                .WithColor(Color.Green)
                .WithDescription(baseMsg)
                .WithFooter(footer => footer.WithText("ACNH Orders"))
                .WithCurrentTimestamp();

            if (itemReq.VillagerOrder != null)
            {
                var villagerName = GameInfo.Strings.GetVillager(itemReq.VillagerOrder.GameName);
                successBuilder.AddField("Waiting on island", villagerName, inline: false);
                successBuilder.AddField($"Note", "Ensure you can collect them within the order timeframe.", inline: false);
            }

            // Enqueue now that all checks are done
            Globals.Hub.Orders.Enqueue(itemReq);

            statusEmbed = successBuilder.Build();
            return true;
        }

        public static int GetPosition(ulong id, out OrderRequest<Item>? order)
        {
            var orders = Globals.Hub.Orders;
            var orderArray = orders.ToArray().Where(x => !x.SkipRequested).ToArray();
            var orderFound = Array.Find(orderArray, x => x.UserGuid == id);
            if (orderFound != null && !orderFound.SkipRequested)
            {
                if (orderFound is OrderRequest<Item> oreq)
                {
                    order = oreq;
                    return Array.IndexOf(orderArray, orderFound) + 1;
                }
            }

            order = null;
            return -1;
        }

        public static string GetETA(int pos)
        {
            int minSeconds = ArriveTime + SetupTime + Globals.Bot.Config.OrderConfig.UserTimeAllowed + Globals.Bot.Config.OrderConfig.WaitForArriverTime;
            int addSeconds = ArriveTime + Globals.Bot.Config.OrderConfig.UserTimeAllowed + Globals.Bot.Config.OrderConfig.WaitForArriverTime;
            var timeSpan = TimeSpan.FromSeconds(minSeconds + (addSeconds * (pos-1)));
            if (timeSpan.Hours > 0)
                return string.Format("{0:D2}h:{1:D2}m:{2:D2}s", timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds);
            else
                return string.Format("{0:D2}m:{1:D2}s", timeSpan.Minutes, timeSpan.Seconds);
        }

        private static ulong ID = 0;
        private static object IDAccessor = new();
        public static ulong GetNextID()
        {
            lock(IDAccessor)
            {
                return ID++;
            }
        }

        public static void ClearQueue<T>(this ConcurrentQueue<T> queue)
        {
            T item; // weird runtime error
#pragma warning disable CS8600
            while (queue.TryDequeue(out item)) { } // do nothing
#pragma warning restore CS8600
        }

        public static string GetQueueString()
        {
            var orders = Globals.Hub.Orders;
            var orderArray = orders.ToArray().Where(x => !x.SkipRequested).ToArray();
            string orderString = string.Empty;
            foreach (var ord in orderArray)
                orderString += $"{ord.VillagerName} \r\n";

            return orderString;
        }
    }
}
