using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using NHSE.Core;

namespace SysBot.ACNHOrders
{
    // ReSharper disable once UnusedType.Global
    public class ItemModule : ModuleBase<SocketCommandContext>
    {
        [Command("lookupLang")]
        [Alias("ll")]
        [Summary("Gets a list of items that contain the request string.")]
        [RequireQueueRole(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task SearchItemsAsync([Summary("Language code to search with")] string language, [Summary("Item name / item substring")][Remainder] string itemName)
        {
            

            if (!Globals.Bot.Config.AllowLookup)
            {
                await ReplyLookupDisabledAsync().ConfigureAwait(false);
                return;
            }

            const int minLength = 2;
            if (string.IsNullOrWhiteSpace(itemName) || itemName.Length <= minLength)
            {
                await ReplyAsync($"Please enter a search term longer than {minLength} characters.").ConfigureAwait(false);
                return;
            }

            var strings = GameInfo.GetStrings(language).ItemDataSource;

            var exact = ItemParser.GetItem(itemName, strings);
            if (!exact.IsNone)
            {
                var msg = $"{exact.ItemId:X4} {itemName}";
                if (msg == "02F8 vine")
                {
                    msg = "3107 vine";
                }
                if (msg == "02F7 glowing moss")
                {
                    msg = "3106 glowing moss";
                }

                var exactEmbed = new EmbedBuilder()
                    .WithTitle("Exact match")
                    .WithDescription(Format.Code(msg))
                    .WithColor(new Color(0x2ECC71)) // green tint
                    .WithTimestamp(DateTimeOffset.UtcNow)
                    .Build();

                await ReplyAsync(embed: exactEmbed).ConfigureAwait(false);
                return;
            }

            var matches = ItemParser.GetItemsMatching(itemName, strings).ToArray();
            if (matches.Length == 0)
            {
                var noMatchEmbed = new EmbedBuilder()
                    .WithTitle("No matches found")
                    .WithDescription($"{Context.User.Mention} - No items matched your search for \"{itemName}\".")
                    .WithColor(new Color(0xE74C3C)) // red tint
                    .WithTimestamp(DateTimeOffset.UtcNow)
                    .Build();

                await ReplyAsync(embed: noMatchEmbed).ConfigureAwait(false);
                return;
            }

            var result = string.Join(Environment.NewLine, matches.Select(z => $"{z.Value:X4} {z.Text}"));

            const int maxLength = 500;
            var embedBuilder = new EmbedBuilder()
                .WithTitle($"Search results for \"{itemName}\"")
                .WithColor(new Color(0x3498DB)) // blue tint
                .WithTimestamp(DateTimeOffset.UtcNow);

            if (result.Length > maxLength)
            {
                // Order by similarity and rebuild result, then truncate for embed
                var ordered = matches.OrderBy(z => LevenshteinDistance.Compute(z.Text, itemName));
                result = string.Join(Environment.NewLine, ordered.Select(z => $"{z.Value:X4} {z.Text}"));

                var truncated = result.Length > maxLength
                    ? result.Substring(0, maxLength) + "...[truncated]"
                    : result;

                embedBuilder
                    .WithDescription(Format.Code(truncated))
                    .WithFooter($"{matches.Length} matches (truncated)");
            }
            else
            {
                embedBuilder
                    .WithDescription(Format.Code(result))
                    .WithFooter($"{matches.Length} matches");
            }

            await ReplyAsync(embed: embedBuilder.Build()).ConfigureAwait(false);
        }

        [Command("lookup")]
        [Alias("li", "search")]
        [Summary("Gets a list of items that contain the request string.")]
        [RequireQueueRole(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task SearchItemsAsync([Summary("Item name / item substring")][Remainder] string itemName)
        {
            if (!Globals.Bot.Config.AllowLookup)
            {
                await ReplyLookupDisabledAsync().ConfigureAwait(false);
                return;
            }
            var strings = GameInfo.Strings.ItemDataSource;
            await PrintItemsAsync(itemName, strings).ConfigureAwait(false);
        }

   

        private async Task PrintItemsAsync(string itemName, IReadOnlyList<ComboItem> strings)
        {
            const int minLength = 2;
            if (itemName.Length <= minLength)
            {
                await ReplyAsync($"Please enter a search term longer than {minLength} characters.").ConfigureAwait(false);
                return;
            }

            var exact = ItemParser.GetItem(itemName, strings);
            if (!exact.IsNone)
            {
                var msg = $"{exact.ItemId:X4} {itemName}";
                if (msg == "02F8 vine")
                {
                    msg = "3107 vine";
                }
                if (msg == "02F7 glowing moss")
                {
                    msg = "3106 glowing moss";
                }
                await ReplyAsync(Format.Code(msg)).ConfigureAwait(false);
                return;
            }

            var matches = ItemParser.GetItemsMatching(itemName, strings).ToArray();
            var result = string.Join(Environment.NewLine, matches.Select(z => $"{z.Value:X4} {z.Text}"));

            if (result.Length == 0)
            {
                var embed = new EmbedBuilder()
                    .WithTitle("No matches found")
                    .WithDescription($"{Context.User.Mention} - No items matched your search for \"{itemName}\".")
                    .WithColor(new Color(0xE74C3C)) // red tint
                    .WithTimestamp(DateTimeOffset.UtcNow)
                    .Build();

                await ReplyAsync(embed: embed).ConfigureAwait(false);
                return;
            }



            const int maxLength = 500;

            var embedBuilder = new EmbedBuilder()
                .WithTitle($"Search results for \"{itemName}\"")
                .WithColor(new Color(0x3498DB)) // blue tint
                .WithTimestamp(DateTimeOffset.UtcNow);

            if (result.Length > maxLength)
            {
                // Order by similarity and rebuild result, then truncate for embed
                var ordered = matches.OrderBy(z => LevenshteinDistance.Compute(z.Text, itemName));
                result = string.Join(Environment.NewLine, ordered.Select(z => $"{z.Value:X4} {z.Text}"));

                var truncated = result.Length > maxLength
                    ? result.Substring(0, maxLength) + "...[truncated]"
                    : result;

                embedBuilder
                    .WithDescription(Format.Code(truncated))
                    .WithFooter($"{matches.Length} matches (truncated)");
            }
            else
            {
                embedBuilder
                    .WithDescription(Format.Code(result))
                    .WithFooter($"{matches.Length} matches");
            }

            await ReplyAsync(embed: embedBuilder.Build()).ConfigureAwait(false);
        }

        [Command("item")]
        [Summary("Gets the info for an item.")]
        [RequireQueueRole(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task GetItemInfoAsync([Summary("Item ID (in hex)")] string itemHex)
        {
            

            if (!Globals.Bot.Config.AllowLookup)
            {
                await ReplyLookupDisabledAsync().ConfigureAwait(false);
                return;
            }

            ushort itemID = ItemParser.GetID(itemHex);
            if (itemID == Item.NONE)
            {
                var invalidEmbed = new EmbedBuilder()
                    .WithTitle("Invalid item requested")
                    .WithDescription($"{Context.User.Mention} - The item ID you provided could not be parsed.")
                    .WithColor(new Color(0xE74C3C)) // red tint
                    .WithTimestamp(DateTimeOffset.UtcNow)
                    .Build();

                await ReplyAsync(embed: invalidEmbed).ConfigureAwait(false);
                return;
            }

            var name = GameInfo.Strings.GetItemName(itemID);
            var result = ItemInfo.GetItemInfo(itemID);

            if (string.IsNullOrEmpty(result))
            {
                var noDataEmbed = new EmbedBuilder()
                    .WithTitle("No customization data available")
                    .WithDescription($"{Context.User.Mention} - No customization data available for the requested item ({name}).")
                    .WithColor(new Color(0xE74C3C)) // red tint
                    .WithTimestamp(DateTimeOffset.UtcNow)
                    .Build();

                await ReplyAsync(embed: noDataEmbed).ConfigureAwait(false);
            }
            else
            {
                var infoEmbed = new EmbedBuilder()
                    .WithTitle($"Item info: {name}")
                    .WithDescription(Format.Code(result))
                    .WithColor(new Color(0x2ECC71)) // green tint
                    .WithFooter($"Item ID: {itemID:X4}")
                    .WithTimestamp(DateTimeOffset.UtcNow)
                    .Build();

                await ReplyAsync(embed: infoEmbed).ConfigureAwait(false);
            }
        }

        [Command("stack")]
        [Summary("Stacks an item and prints the hex code.")]
        [RequireQueueRole(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task StackAsync([Summary("Item ID (in hex)")] string itemHex, [Summary("Count of items in the stack")] int count)
        {
            if (!Globals.Bot.Config.AllowLookup)
            {
                await ReplyLookupDisabledAsync().ConfigureAwait(false);
                return;
            }

            ushort itemID = ItemParser.GetID(itemHex);
            if (itemID == Item.NONE || count < 1 || count > 99)
            {
                var errorEmbed = new EmbedBuilder()
                    .WithTitle("Invalid item requested")
                    .WithDescription($"{Context.User.Mention} - The item or count you specified is invalid. Please provide a valid hex ID and a count between 1 and 99.")
                    .WithColor(new Color(0xE74C3C)) // red tint
                    .WithTimestamp(DateTimeOffset.UtcNow)
                    .Build();

                await ReplyAsync(embed: errorEmbed).ConfigureAwait(false);
                return;
            }

            var ct = count - 1; // value 0 => count of 1
            var item = new Item(itemID) { Count = (ushort)ct };
            var msg = ItemParser.GetItemText(item);

            var successEmbed = new EmbedBuilder()
                .WithTitle("Stacked Item")
                .WithDescription(Format.Code(msg))
                .WithColor(new Color(0x2ECC71)) // green tint
                .WithTimestamp(DateTimeOffset.UtcNow)
                .WithFooter($"Count: {count}")
                .Build();

            await ReplyAsync(embed: successEmbed).ConfigureAwait(false);
        }

        [Command("customize")]
        [Summary("Customizes an item and prints the hex code.")]
        [RequireQueueRole(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task CustomizeAsync([Summary("Item ID (in hex)")] string itemHex, [Summary("First customization value")] int cust1, [Summary("Second customization value")] int cust2)
            => await CustomizeAsync(itemHex, cust1 + cust2).ConfigureAwait(false);

        public async Task CustomizeAsync([Summary("Item ID (in hex)")] string itemHex, [Summary("Customization value sum")] int sum)
        {
         

            if (!Globals.Bot.Config.AllowLookup)
            {
                await ReplyLookupDisabledAsync().ConfigureAwait(false);
                return;
            }

            ushort itemID = ItemParser.GetID(itemHex);
            if (itemID == Item.NONE)
            {
                var invalidEmbed = new EmbedBuilder()
                    .WithTitle("Invalid item requested")
                    .WithDescription($"{Context.User.Mention} - The item ID you provided could not be parsed.")
                    .WithColor(new Color(0xE74C3C)) // red tint
                    .WithTimestamp(DateTimeOffset.UtcNow)
                    .Build();

                await ReplyAsync(embed: invalidEmbed).ConfigureAwait(false);
                return;
            }

            if (sum <= 0)
            {
                var noDataEmbed = new EmbedBuilder()
                    .WithTitle("No customization data specified")
                    .WithDescription($"{Context.User.Mention} - No customization data specified.")
                    .WithColor(new Color(0xE74C3C)) // red tint
                    .WithTimestamp(DateTimeOffset.UtcNow)
                    .Build();

                await ReplyAsync(embed: noDataEmbed).ConfigureAwait(false);
                return;
            }

            var remake = ItemRemakeUtil.GetRemakeIndex(itemID);
            if (remake < 0)
            {
                var noRemakeEmbed = new EmbedBuilder()
                    .WithTitle("No customization data available")
                    .WithDescription($"{Context.User.Mention} - No customization data available for the requested item.")
                    .WithColor(new Color(0xE74C3C)) // red tint
                    .WithTimestamp(DateTimeOffset.UtcNow)
                    .Build();

                await ReplyAsync(embed: noRemakeEmbed).ConfigureAwait(false);
                return;
            }

            int body = sum & 7;
            int fabric = sum >> 5;
            if (fabric > 7 || ((fabric << 5) | body) != sum)
            {
                var invalidEmbed = new EmbedBuilder()
                    .WithTitle("Invalid customization data specified")
                    .WithDescription($"{Context.User.Mention} - The customization data you provided appears to be invalid.")
                    .WithColor(new Color(0xE74C3C)) // red tint
                    .WithTimestamp(DateTimeOffset.UtcNow)
                    .Build();

                await ReplyAsync(embed: invalidEmbed).ConfigureAwait(false);
                return;
            }

            var info = ItemRemakeInfoData.List[remake];
            bool hasBody = body == 0 || body <= info.ReBodyPatternNum;
            bool hasFabric = fabric == 0 || info.GetFabricDescription(fabric) != "Invalid";

            if (!hasBody || !hasFabric)
            {
                var warnEmbed = new EmbedBuilder()
                    .WithTitle("Requested customization appears invalid")
                    .WithDescription($"{Context.User.Mention} - Requested customization for item appears to be invalid.")
                    .WithColor(new Color(0xE67E22)) // orange tint for warning
                    .WithTimestamp(DateTimeOffset.UtcNow)
                    .Build();

                await ReplyAsync(embed: warnEmbed).ConfigureAwait(false);
                // Intentionally not returning to preserve original behavior (send notice, then still show the result).
            }

            var item = new Item(itemID) { BodyType = body, PatternChoice = fabric };
            var msg = ItemParser.GetItemText(item);

            var successEmbed = new EmbedBuilder()
                .WithTitle("Customized Item")
                .WithDescription(Format.Code(msg))
                .WithColor(new Color(0x2ECC71)) // green tint
                .WithFooter($"Item ID: {itemID:X4}")
                .WithTimestamp(DateTimeOffset.UtcNow)
                .Build();

            await ReplyAsync(embed: successEmbed).ConfigureAwait(false);
        }

        // Helper: sends a standardized embed indicating lookup commands are disabled
        private async Task ReplyLookupDisabledAsync()
        {
            var embed = new EmbedBuilder()
                .WithTitle("Lookup Disabled")
                .WithDescription($"{Context.User.Mention} - Lookup commands are not accepted.")
                .WithColor(new Color(0xE74C3C)) // a red tint
                .WithTimestamp(DateTimeOffset.UtcNow)
                .Build();

            await ReplyAsync(embed: embed).ConfigureAwait(false);
        }
    }
}
