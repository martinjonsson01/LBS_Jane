using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot_Jane.Core.Services
{
    public class StartupService
    {
        private readonly DiscordSocketClient _discord;
        private readonly CommandService _commands;
        private readonly IConfigurationRoot _config;

        // DiscordSocketClient and CommandService are injected automatically from the IServiceProvider.
        public StartupService(DiscordSocketClient discord, CommandService commands, IConfigurationRoot config)
        {
            _discord = discord;
            _commands = commands;
            _config = config;
        }

        public async Task StartAsync()
        {
            // Get the discord token from the config file
            var discordToken = _config["tokens:discord"];
            if (string.IsNullOrWhiteSpace(discordToken))
                throw new Exception("Please enter your bot's token into the `_configuration.json` file found in the applications root directory.");

            // Notify users that bot is online when connected to guild.
            _discord.GuildAvailable += async guild => await guild.TextChannels.First().SendMessageAsync("I'm online!");

            // Login to discord.
            await _discord.LoginAsync(TokenType.Bot, discordToken);
            // Connect to the websocket.
            await _discord.StartAsync();

            // Set up "currently playing" game.
            await _discord.SetGameAsync(_config["playing_game"]);

            // Load commands and modules into the command service.
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly());
        }
    }
}
