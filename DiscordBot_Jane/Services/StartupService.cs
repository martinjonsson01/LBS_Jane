using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace DiscordBot_Jane.Services
{
    public class StartupService
    {
        private readonly DiscordSocketClient _discord;
        private readonly CommandService _commands;

        // DiscordSocketClient and CommandService are injected automatically from the IServiceProvider.
        public StartupService(DiscordSocketClient discord, CommandService commands)
        {
            _discord = discord;
            _commands = commands;
        }

        public async Task StartAsync()
        {
            // Notify users that bot is online when connected to guild.
            _discord.GuildAvailable += async guild => await guild.TextChannels.First().SendMessageAsync("I'm online!");

            // Login to discord.
            await _discord.LoginAsync(TokenType.Bot, Config.DiscordToken);
            // Connect to the websocket.
            await _discord.StartAsync();

            // Set up "currently playing" game.
            await _discord.SetGameAsync("Siggeroid");

            // Load commands and modules into the command service.
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly());
        }
    }
}
