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
    public class RecipeModule : ModuleBase<SocketCommandContext>
    {
        [Command("recipeLang")]
        [Alias("rl")]
        [Summary("Gets a list of DIY recipe IDs that contain the requested Item Name string.")]
        [RequireQueueRole(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task SearchItemsAsync([Summary("Language code to search with")] string language, [Summary("Item name / item substring")][Remainder] string itemName)
        {
            if (!Globals.Bot.Config.AllowLookup)
            {
                await ReplyAsync($"{Context.User.Mention} - Lookup commands are not accepted.").ConfigureAwait(false);
                return;
            }

            var strings = GameInfo.GetStrings(language).ItemDataSource;
            await PrintItemsAsync(itemName, strings).ConfigureAwait(false);
        }

        [Command("recipe")]
        [Alias("ri", "searchDIY")]
        [Summary("Gets a list of DIY recipe IDs that contain the requested Item Name string.")]
        [RequireQueueRole(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task SearchItemsAsync([Summary("Item name / item substring")][Remainder] string itemName)
        {
            if (!Globals.Bot.Config.AllowLookup)
            {
                await ReplyAsync($"{Context.User.Mention} - Lookup commands are not accepted.").ConfigureAwait(false);
                return;
            }

            var strings = GameInfo.Strings.ItemDataSource;
            await PrintItemsAsync(itemName, strings).ConfigureAwait(false);
        }

        private async Task PrintItemsAsync(string itemName, IReadOnlyList<ComboItem> strings)
        {
            const int minLength = 2;
            if (string.IsNullOrWhiteSpace(itemName) || itemName.Length <= minLength)
            {
                var embedErr = new EmbedBuilder()
                    .WithTitle("Search Error")
                    .WithDescription($"Please enter a search term longer than {minLength} characters.")
                    .WithColor(Color.Orange)
                    .Build();

                await ReplyAsync(embed: embedErr).ConfigureAwait(false);
                return;
            }

            // Exact match first
            foreach (var item in strings)
            {
                if (!string.Equals(item.Text, itemName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!ItemParser.InvertedRecipeDictionary.TryGetValue((ushort)item.Value, out var recipeID))
                {
                    var notDiy = new EmbedBuilder()
                        .WithTitle("Not a DIY")
                        .WithDescription("Requested item is not a DIY recipe.")
                        .WithColor(Color.DarkGrey)
                        .Build();

                    await ReplyAsync(embed: notDiy).ConfigureAwait(false);
                    return;
                }

                var embed = new EmbedBuilder()
                    .WithTitle("DIY Recipe Found")
                    .AddField("Item", item.Text, true)
                    .AddField("ID (hex)", $"`{item.Value:X4}`", true)
                    .AddField("Recipe order code", $"`{recipeID:X3}000016A2`")
                    .WithColor(Color.Green)
                    .WithFooter($"Search: {itemName}")
                    .Build();

                await ReplyAsync(embed: embed).ConfigureAwait(false);
                return;
            }

            // Fuzzy matches
            var items = ItemParser.GetItemsMatching(itemName, strings).ToArray();
            var matches = new List<string>();
            foreach (var item in items)
            {
                if (!ItemParser.InvertedRecipeDictionary.TryGetValue((ushort)item.Value, out var recipeID))
                    continue;

                matches.Add($"{item.Value:X4} {item.Text}: {recipeID:X3}000016A2");
            }

            if (matches.Count == 0)
            {
                var noMatches = new EmbedBuilder()
                    .WithTitle("No matches found ")
                    .WithDescription($" No items matched your search for \"{itemName}\".")
                    .WithColor(Color.Red)
                    .WithTimestamp(DateTimeOffset.UtcNow)
                    .Build();

                await ReplyAsync(embed: noMatches).ConfigureAwait(false);
                return;
            }

            var result = string.Join(Environment.NewLine, matches);
            const int maxLength = 2000;
            if (result.Length > maxLength)
                result = result.Substring(0, maxLength) + "...[truncated]";

            var embedMatches = new EmbedBuilder()
                .WithTitle($"Search results for \"{itemName}\"")
                .WithDescription($"```text\n{result}\n```")
                .WithColor(Color.Blue)
                .WithFooter($"{matches.Count} matches")
                .WithTimestamp(DateTimeOffset.UtcNow)
                .Build();

            await ReplyAsync(embed: embedMatches).ConfigureAwait(false);
        }
    }
}
