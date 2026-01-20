using System;
using System.Linq;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using NHSE.Core;
using NHSE.Villagers;

namespace SysBot.ACNHOrders
{
    // ReSharper disable once UnusedType.Global
    public class VillagerModule : ModuleBase<SocketCommandContext>
    {

        [Command("injectVillager"), Alias("iv")]
        [Summary("Injects a villager based on the internal name.")]
        [RequireQueueRole(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task InjectVillagerAsync(int index, string internalName) => await InjectVillagers(index, new string[1] { internalName });
        

        [Command("injectVillager"), Alias("iv")]
        [Summary("Injects a villager based on the internal name.")]
        [RequireQueueRole(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task InjectVillagerAsync(string internalName) => await InjectVillagerAsync(0, internalName).ConfigureAwait(false);

        [Command("multiVillager"), Alias("mvi", "injectVillagerMulti", "superUltraInjectionGiveMeMoreVillagers")]
        [Summary("Injects multiple villagers based on the internal names.")]
        [RequireQueueRole(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task InjectVillagerMultiAsync([Remainder]string names) => await InjectVillagers(0, names.Split(new string[2] { ",", " ", }, StringSplitOptions.RemoveEmptyEntries));

        private async Task InjectVillagers(int startIndex, string[] villagerNames)
        {
            if (!Globals.Bot.Config.DodoModeConfig.LimitedDodoRestoreOnlyMode)
            {
                await ReplyAsync($"{Context.User.Mention} - Villagers cannot be injected in order mode.").ConfigureAwait(false);
                return;
            }

            if (!Globals.Bot.Config.AllowVillagerInjection)
            {
                await ReplyAsync($"{Context.User.Mention} - Villager injection is currently disabled.").ConfigureAwait(false);
                return;
            }

            var bot = Globals.Bot;
            int index = startIndex;
            int count = villagerNames.Length;

            if (count < 1)
            {
                await ReplyAsync($"{Context.User.Mention} - No villager names in command").ConfigureAwait(false);
                return;
            }

            foreach (var nameLookup in villagerNames)
            {
                var internalName = nameLookup;
                var nameSearched = internalName;

                if (!VillagerResources.IsVillagerDataKnown(internalName))
                    internalName = GameInfo.Strings.VillagerMap.FirstOrDefault(z => string.Equals(z.Value, internalName, StringComparison.InvariantCultureIgnoreCase)).Key;

                if (internalName == default)
                {
                    await ReplyAsync($"{Context.User.Mention} - {nameSearched} is not a valid internal villager name.");
                    return;
                }

                if (index > byte.MaxValue || index < 0)
                {
                    await ReplyAsync($"{Context.User.Mention} - {index} is not a valid index");
                    return;
                }

                int slot = index;

                var replace = VillagerResources.GetVillager(internalName);
                var user = Context.User;
                var mention = Context.User.Mention;

                var extraMsg = string.Empty;
                if (VillagerOrderParser.IsUnadoptable(internalName))
                    extraMsg += " Please note that you will not be able to adopt this villager.";

                var request = new VillagerRequest(Context.User.Username, replace, (byte)index, GameInfo.Strings.GetVillager(internalName))
                {
                    OnFinish = success =>
                    {
                        var reply = success
                            ? $"{nameSearched} has been injected by the bot at Index {slot}. Please go talk to them!{extraMsg}"
                            : "Failed to inject villager. Please tell the bot owner to look at the logs!";
                        Task.Run(async () => await ReplyAsync($"{reply}").ConfigureAwait(false));
                    }
                };

                bot.VillagerInjections.Enqueue(request);

                index = (index + 1) % 10;
            }

            var addMsg = count > 1 ? $"Villager inject request for {count} villagers have" : "Villager inject request has";
            var msg = $":{addMsg} been added to the queue and will be injected momentarily. I will reply to you once this has completed.";
            await ReplyAsync(msg).ConfigureAwait(false);
        }

        [Command("villagers"), Alias("vl", "villagerList")]
        [Summary("Prints the list of villagers currently on the island.")]
        [RequireQueueRole(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task GetVillagerListAsync()
        {
            if (!Globals.Bot.Config.DodoModeConfig.LimitedDodoRestoreOnlyMode)
            {
                var noticeEmbed = new Discord.EmbedBuilder()
                    .WithTitle("Villager Replacement Notice")
                    .WithDescription($"{Context.User.Mention} - Villagers on the island may be replaceable by adding them to your order command.")
                    .WithColor(Discord.Color.Orange)
                    .WithFooter(footer => footer.Text = $"Requested by {Context.User.Username}");

                await ReplyAsync(embed: noticeEmbed.Build()).ConfigureAwait(false);
                return;
            }

            var villagersText = string.IsNullOrWhiteSpace(Globals.Bot.Villagers.LastVillagers)
                ? "No villagers are currently listed."
                : Globals.Bot.Villagers.LastVillagers;

            var listEmbed = new Discord.EmbedBuilder()
                .WithTitle($"Villagers on {Globals.Bot.TownName}")
                .WithDescription(villagersText)
                .WithColor(Discord.Color.Green)
                .WithFooter(footer =>
                {
                    footer.Text = $"Requested by {Context.User.Username}";
                    footer.IconUrl = Context.User.GetAvatarUrl();
                });

            await ReplyAsync(embed: listEmbed.Build()).ConfigureAwait(false);
        }
        

        

        [Command("villagerName")]
        [Alias("vn", "nv", "name")]
        [Summary("Gets the internal name of a villager.")]
        [RequireQueueRole(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task GetVillagerInternalNameAsync([Summary("Villager name")][Remainder] string villagerName)
        {
            var strings = GameInfo.Strings;
            await ReplyVillagerName(strings, villagerName).ConfigureAwait(false);
        }

        private async Task ReplyVillagerName(GameStrings strings, string villagerName)
        {
            if (!Globals.Bot.Config.AllowLookup)
            {
                var disabledEmbed = new Discord.EmbedBuilder()
                    .WithTitle("Lookup Disabled")
                    .WithDescription($"{Context.User.Mention} - Lookup commands are not accepted.")
                    .WithColor(Discord.Color.Orange)
                    .WithFooter(footer => footer.Text = $"Requested by {Context.User.Username}");

                await ReplyAsync(embed: disabledEmbed.Build()).ConfigureAwait(false);
                return;
            }

            // Sanitize input: remove spaces and lowercase anything that was after the first space
            var sanitizedInput = villagerName ?? string.Empty;
            if (sanitizedInput.Contains(' '))
            {
                var parts = sanitizedInput
                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length > 0)
                {
                    var first = parts[0];
                    var rest = parts.Skip(1)
                                    .Select(p => p.ToLowerInvariant());

                    sanitizedInput = first + string.Concat(rest);
                }
                else
                {
                    sanitizedInput = string.Empty;
                }
            }

            // Defensive: remove any remaining spaces
            sanitizedInput = sanitizedInput.Replace(" ", string.Empty);

            var map = strings.VillagerMap;
            var result = map.FirstOrDefault(z =>
                string.Equals(sanitizedInput, z.Value.Replace(" ", string.Empty), StringComparison.InvariantCultureIgnoreCase));

            if (string.IsNullOrWhiteSpace(result.Key))
            {
                var notFoundEmbed = new Discord.EmbedBuilder()
                    .WithTitle("Villager Not Found")
                    .WithDescription($"No villager found for name `{villagerName}`.")
                    .WithColor(Discord.Color.Red)
                    .WithTimestamp(DateTimeOffset.UtcNow);

                await ReplyAsync(embed: notFoundEmbed.Build()).ConfigureAwait(false);
                return;
            }

            // Remove spaces and lowercase the name
            var successEmbed = new Discord.EmbedBuilder()
                .WithTitle($"Search results for \"{villagerName}\"")
                .WithColor(Discord.Color.Green)
                .AddField("Villager Name", result.Value ?? "Unknown", inline: true)
                .AddField("Internal Name", result.Key ?? "Unknown", inline: true)
                .AddField("Order Format", $"villager:{result.Key}", inline: false)
                .WithThumbnailUrl($"https://raw.githubusercontent.com/ThatOneCrab/ACNH-Sprites/refs/heads/main/{sanitizedInput}_nh.png")
                .WithTimestamp(DateTimeOffset.UtcNow);

            await ReplyAsync(embed: successEmbed.Build()).ConfigureAwait(false);
        }
    }
}