using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Threading.Tasks;
using DiscordBot_Jane.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot_Jane
{
    public class Program
    {
        public static void Main(string[] args)
        {
            new Program().StartAsync().GetAwaiter().GetResult();
        }
        
        private async Task StartAsync()
        {
            var services = new ServiceCollection()
                .AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
                {
                    LogLevel = LogSeverity.Verbose,
                    MessageCacheSize = 1000
                }))
                .AddSingleton(new CommandService(new CommandServiceConfig
                {
                    DefaultRunMode = RunMode.Async,
                    LogLevel = LogSeverity.Verbose
                }))
                .AddSingleton<CommandHandler>()
                .AddSingleton<LoggingService>()
                .AddSingleton<StartupService>()
                .AddSingleton<Random>();
        }
    }
}
