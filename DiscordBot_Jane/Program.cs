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
                .AddSingleton<LoggingService>() // Allows us to log messages to the log file and console.
                .AddSingleton<StartupService>() // Starts up the bot.
                .AddSingleton<ChannelService>() // Handles creating required channels.
                .AddSingleton<JaneClassroomService>() // Handles all interactions with Google Classroom.
                .AddSingleton<Random>(); // You get better random with a single instance than by creating a new one every time you need it.

            // Create the service provider.
            var provider = services.BuildServiceProvider();

            // Initialize the logging service, startup service, and command handler (along with other services).
            provider.GetRequiredService<LoggingService>();
            await provider.GetRequiredService<StartupService>().StartAsync();
            provider.GetRequiredService<ChannelService>();
            provider.GetRequiredService<JaneClassroomService>();
            provider.GetRequiredService<CommandHandler>();

            // Prevent the application from closing.
            await Task.Delay(-1);
        }
    }
}
