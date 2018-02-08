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

namespace DiscordBot_Jane.Services
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
        
        public ChannelService(
            DiscordSocketClient discord,
            CommandService commands,
            LoggingService logger,
            IServiceProvider provider)
        {
            _discord = discord;
            _commands = commands;
            _logger = logger;
            _provider = provider;
            
            _discord.GuildAvailable += OnGuildAvailable;
        }

        private async Task OnGuildAvailable(SocketGuild guild)
        {
            //SpelNewsChannels.Add(guild.Id, await guild.CreateTextChannelAsync("spel_utv_nyheter"));
            //SystemNewsChannels.Add(guild.Id, await guild.CreateTextChannelAsync("system_utv_nyheter"));

            SocketTextChannel newsChannel = null;

            // Check if channels already exist. 
            foreach (var channel in guild.TextChannels)
            {
                if (channel.Name == "classroom_nyheter")
                {
                    newsChannel = channel;
                    if (!NewsChannels.ContainsKey(guild.Id))
                        NewsChannels.Add(guild.Id, channel);
                }
            }

            // Create channels that don't exist.
            if (newsChannel == null)
                if (!NewsChannelsRest.ContainsKey(guild.Id))
                    NewsChannelsRest.Add(guild.Id, await guild.CreateTextChannelAsync("classroom_nyheter"));
        }
    }
}
