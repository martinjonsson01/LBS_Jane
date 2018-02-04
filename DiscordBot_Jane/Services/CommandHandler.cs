using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;

namespace DiscordBot_Jane.Services
{
    public class CommandHandler
    {
        private readonly DiscordSocketClient _discord;
        private readonly CommandService _commands;
        private readonly IServiceProvider _provider;

        public CommandHandler(
            DiscordSocketClient discord,
            CommandService commands,
            IServiceProvider provider)
        {
            _discord = discord;
            _commands = commands;
            _provider = provider;

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
            if (msg.HasCharPrefix(Config.Prefix, ref argPos) || msg.HasMentionPrefix(_discord.CurrentUser, ref argPos))
            {
                // Execute the command.
                var result = await _commands.ExecuteAsync(context, argPos, _provider);

                // If not successful, reply with the error.
                if (!result.IsSuccess)
                    await context.Channel.SendMessageAsync(result.ToString());
            }
        }
    }
}
