using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot_Jane.Core.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Classroom.v1;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot_Jane.Core.Services
{
    public class StartupService
    {
        private readonly DiscordSocketClient _discord;
        private readonly CommandService _commands;
        private readonly IConfigurationRoot _config;
        private readonly LoggingService _logger;

        // DiscordSocketClient and CommandService are injected automatically from the IServiceProvider.
        public StartupService(DiscordSocketClient discord, CommandService commands, IConfigurationRoot config, LoggingService logger)
        {
            _discord = discord;
            _commands = commands;
            _config = config;
            _logger = logger;
        }
        
        public async Task StartAsync()
        {
            // Get the discord token from the config file depending on if debug mode or not.
            var discordToken = Program.InDebugMode ? _config["tokens:discord-dev"] : _config["tokens:discord"];
            if (String.IsNullOrWhiteSpace(discordToken))
                throw new Exception("Please enter your bot's token into the `_configuration.json` file found in the applications root directory.");
            
            // Notify users that bot is online when connected to guild.
            //_discord.GuildAvailable += async guild => await guild.TextChannels.First().SendMessageAsync("Jag är online!").ConfigureAwait(false);

            // Login to discord.
            await _discord.LoginAsync(TokenType.Bot, discordToken).ConfigureAwait(false);
            // Connect to the websocket.
            await _discord.StartAsync().ConfigureAwait(false);

            // Set up "currently playing" game.
            var playingGame = _config["playing_game"] ?? "null";
            await _discord.SetGameAsync(playingGame).ConfigureAwait(false);

            // Load commands and modules into the command service.
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly()).ConfigureAwait(false);
        }
    }
}
