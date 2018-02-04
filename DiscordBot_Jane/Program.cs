using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Linq;
using System.Threading.Tasks;
using DiscordBot_Jane.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot_Jane
{
    public class Program
    {
        public static bool InDebugMode { get; private set; }

        public static void Main(string[] args)
        {
            #if DEBUG
                InDebugMode = true;
            #else
                InDebugMode = false;
            #endif
            new Program().StartAsync().GetAwaiter().GetResult();
        }
        
        private async Task StartAsync()
        {
            // Begin building the service provider.
            var services = new ServiceCollection()
                .AddSingleton(new DiscordSocketClient(new DiscordSocketConfig // Add the discord client to the service provider.
                {
                    LogLevel = LogSeverity.Verbose,
                    MessageCacheSize = 1000 // Tell Discord.Net to cache 1000 messages per channel.
                }))
                .AddSingleton(new CommandService(new CommandServiceConfig // Add the command service to the service provider.
                {
                    DefaultRunMode = RunMode.Async, // Force all commands to run async.
                    LogLevel = LogSeverity.Verbose
                }))
                .AddSingleton<CommandHandler>() // Add remaining services to the provider.
                .AddSingleton<LoggingService>()
                .AddSingleton<StartupService>()
                .AddSingleton<JaneClassroomService>()
                .AddSingleton<Random>(); // You get better random with a single instance than by creating a new one every time you need it.

            // Create the service provider.
            var provider = services.BuildServiceProvider();

            // Initialize the logging service, startup service, and command handler.
            provider.GetRequiredService<LoggingService>();
            provider.GetRequiredService<JaneClassroomService>();
            await provider.GetRequiredService<StartupService>().StartAsync();
            provider.GetRequiredService<CommandHandler>();

            // Prevent the application from closing.
            await Task.Delay(-1);
        }
    }
}
