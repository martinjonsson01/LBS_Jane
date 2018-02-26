using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;

namespace DiscordBot_Jane.Core.Modules
{
    [Name("Moderation")]
    [RequireContext(ContextType.Guild)]
    public class ModerationModule : ModuleBase<SocketCommandContext>
    {
        private readonly IConfigurationRoot _config;

        public ModerationModule(IConfigurationRoot config)
        {
            _config = config;
        }

        [Command("kick"), Alias("kicka")]
        [Summary("Kicka den specifierade användaren.")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task Kick([Remainder]SocketGuildUser user)
        {
            // If user being kicked can also kick users.
            if (user.GuildPermissions.KickMembers)
            {
                await Context.Channel.SendFileAsync($"gifs/{_config["gifs:error_permission"] ?? "boi"}.gif");
                await ReplyAsync($"boi {Context.User.Mention} du kan inte kicka {user.Username} smh...");
                return;
            }
            await ReplyAsync($"hejdå {user.Mention} :wave:");
            await user.KickAsync();
        }

        [Command("kick"), Alias("kicka")]
        [Summary("Kicka den specifierade användaren, med en anledning till varför.")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task Kick(SocketGuildUser user, string reason)
        {
            // If user being kicked can also kick users.
            if (user.GuildPermissions.KickMembers)
            {
                await Context.Channel.SendFileAsync($"gifs/{_config["gifs:error_permission"] ?? "boi"}.gif");
                await ReplyAsync($"boi {Context.User.Mention} du kan inte kicka {user.Username} smh...");
                return;
            }
            await ReplyAsync($"hejdå {user.Mention} :wave:");
            await user.KickAsync(reason);
        }

        [Command("ban"), Alias("banna")]
        [Summary("Banna den specifierade användaren.")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        public async Task Ban([Remainder]SocketGuildUser user)
        {
            // If user being banned can also ban users.
            if (user.GuildPermissions.BanMembers)
            {
                await Context.Channel.SendFileAsync($"gifs/{_config["gifs:error_permission"] ?? "boi"}.gif");
                await ReplyAsync($"boiii {Context.User.Mention} det kan du inte göra...");
                return;
            }
            await ReplyAsync($"hejdå {user.Mention} :wave:");
            await Context.Guild.AddBanAsync(user, _config.GetValue("ban_prune_days", 7));
        }

        [Command("ban"), Alias("banna")]
        [Summary("Banna den specifierade användaren, med en anledning till varför.")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        public async Task Ban(SocketGuildUser user, string reason)
        {
            // If user being banned can also ban users.
            if (user.GuildPermissions.BanMembers)
            {
                await Context.Channel.SendFileAsync($"gifs/{_config["gifs:error_permission"] ?? "boi"}.gif");
                await ReplyAsync($"boiii {Context.User.Mention} det kan du inte göra...");
                return;
            }
            await ReplyAsync($"hejdå {user.Mention} :wave:");
            await Context.Guild.AddBanAsync(user, _config.GetValue("ban_prune_days", 7), reason);
        }
    }
}
