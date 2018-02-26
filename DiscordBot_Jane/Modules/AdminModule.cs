using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot_Jane.Core.Services;
using Microsoft.Extensions.Configuration;

namespace DiscordBot_Jane.Core.Modules
{
    [Name("Administration")]
    [RequireContext(ContextType.Guild)]
    public class AdminModule : ModuleBase<SocketCommandContext>
    {
        private readonly IConfigurationRoot _config;
        private readonly LoggingService _logger;

        public AdminModule(IConfigurationRoot config, LoggingService logger)
        {
            _config = config;
            _logger = logger;
        }

        [Command("nödavstängning"), Alias("stäng av")]
        [Summary("Stäng av boten.")]
        [RequireOwner]
        public async Task EmergyShutdown()
        {
            // CAUSES ERROR await Context.Channel.SendFileAsync("gifs/sdsdsd.gif");
            await Context.Channel.SendFileAsync($"gifs/{_config["gifs:shutdown"] ?? "timetostop"}.gif");
            await ReplyAsync($"ripperony in pepperoni, stänger av...");
            
            await _logger.LogAsync(LogSeverity.Info, nameof(Program),
                $"Exiting system due to emergency shutdown command sent from {Context.User.Username}");
            Program.Instance.GracefulExit();
            await _logger.LogAsync(LogSeverity.Info, nameof(Program), "Cleanup complete");
        }
    }
}
