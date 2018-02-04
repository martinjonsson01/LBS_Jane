using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace DiscordBot_Jane.Modules
{
    [Name("Moderation")]
    [RequireContext(ContextType.Guild)]
    public class ModerationModule : ModuleBase<SocketCommandContext>
    {
        [Command("kick"), Alias("kicka")]
        [Summary("Kicka den specifierade användaren.")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task Kick([Remainder]SocketGuildUser user)
        {
            await ReplyAsync($"hejdå {user.Mention} :wave:");
            await user.KickAsync();
        }

        [Command("kick"), Alias("kicka")]
        [Summary("Kicka den specifierade användaren, med en anledning till varför.")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task Kick(SocketGuildUser user, string reason)
        {
            await ReplyAsync($"hejdå {user.Mention} :wave:");
            await user.KickAsync(reason);
        }

        [Command("ban"), Alias("banna")]
        [Summary("Banna den specifierade användaren.")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        public async Task Ban([Remainder]SocketGuildUser user)
        {
            await ReplyAsync($"hejdå {user.Mention} :wave:");
            await Context.Guild.AddBanAsync(user, Config.BanPruneDays);
        }

        [Command("ban"), Alias("banna")]
        [Summary("Banna den specifierade användaren, med en anledning till varför.")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        public async Task Ban(SocketGuildUser user, string reason)
        {
            await ReplyAsync($"hejdå {user.Mention} :wave:");
            await Context.Guild.AddBanAsync(user, Config.BanPruneDays, reason);
        }
    }
}
