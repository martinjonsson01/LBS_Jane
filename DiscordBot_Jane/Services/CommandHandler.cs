using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;

namespace DiscordBot_Jane.Services
{
    public class CommandHandler
    {
        private readonly DiscordSocketClient _discord;
        private readonly CommandService _commands;
        private readonly LoggingService _logger;
        private readonly IServiceProvider _provider;
        private readonly IConfigurationRoot _config;

        public CommandHandler(
            DiscordSocketClient discord,
            CommandService commands,
            LoggingService logger,
            IServiceProvider provider,
            IConfigurationRoot config)
        {
            _discord = discord;
            _commands = commands;
            _logger = logger;
            _provider = provider;
            _config = config;

            _discord.MessageReceived += OnMessageRecievedAsync;
        }

        private async Task OnMessageRecievedAsync(SocketMessage socketMessage)
        {
            // Ensure the message is from a user or a bot.
            if (!(socketMessage is SocketUserMessage msg)) return;
            // Ignore self when checking commands.
            if (msg.Author.Id == _discord.CurrentUser.Id) return;
            
            // Create the command context.
            var context = new SocketCommandContext(_discord, msg);

            int argPos = 0;
            // Check if the message has a valid command Prefix.
            if (msg.HasStringPrefix(_config["trigger"], ref argPos, StringComparison.InvariantCultureIgnoreCase) || msg.HasMentionPrefix(_discord.CurrentUser, ref argPos))
            {
                // Execute the command.
                var result = await _commands.ExecuteAsync(context, argPos, _provider);

                // If not successful, log the error.
                if (!result.IsSuccess && result is ExecuteResult exr)
                    await _logger.LogAsync(LogSeverity.Error, "Commands", exr.Exception?.ToString() ?? exr.ErrorReason);
            }
        }
    }
}
