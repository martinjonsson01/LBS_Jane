using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using Google.Apis.Classroom.v1;
using Microsoft.Extensions.Configuration;

namespace DiscordBot_Jane.Core.Services
{
    public class ChannelService
    {
        //public readonly Dictionary<ulong, RestTextChannel> SpelNewsChannels = new Dictionary<ulong, RestTextChannel>();
        //public readonly Dictionary<ulong, RestTextChannel> SystemNewsChannels = new Dictionary<ulong, RestTextChannel>();
        public readonly Dictionary<ulong, SocketTextChannel> NewsChannels = new Dictionary<ulong, SocketTextChannel>();
        public readonly Dictionary<ulong, RestTextChannel> NewsChannelsRest = new Dictionary<ulong, RestTextChannel>();

        private readonly DiscordSocketClient _discord;
        private readonly CommandService _commands;
        private readonly LoggingService _logger;
        private readonly IServiceProvider _provider;
        private readonly IConfigurationRoot _config;
        
        public ChannelService(
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
            
            _discord.GuildAvailable += OnGuildAvailable;
        }

        private async Task OnGuildAvailable(SocketGuild guild)
        {
            SocketTextChannel newsChannel = null;

            // Check if channels already exist. 
            foreach (var channel in guild.TextChannels)
            {
                if (channel.Name == _config["news_channel_name"])
                {
                    newsChannel = channel;
                    if (!NewsChannels.ContainsKey(guild.Id))
                        NewsChannels.Add(guild.Id, channel);
                }
            }

            // Create channels that don't exist.
            if (newsChannel == null)
                if (!NewsChannelsRest.ContainsKey(guild.Id))
                    NewsChannelsRest.Add(guild.Id, await guild.CreateTextChannelAsync(_config["news_channel_name"]));
        }
    }
}
