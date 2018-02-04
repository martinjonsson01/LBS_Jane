using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace DiscordBot_Jane.Services
{
    public class LoggingService
    {
        private readonly DiscordSocketClient _discord;
        private readonly CommandService _commands;

        private string _logDirectory { get; }
        private string _logFile => Path.Combine(_logDirectory, $"{DateTime.UtcNow:yyyy-MM-dd}.txt");

        // DiscordSocketClient and CommandService are injected automatically from the IServiceProvider.
        public LoggingService(DiscordSocketClient discord, CommandService commands)
        {
            _logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");

            _discord = discord;
            _commands = commands;

            _discord.Log += OnLogAsync;
            _commands.Log += OnLogAsync;
        }

        public Task LogAsync(LogSeverity severity, string source, string message)
        {
            // Create the log directory if it doesn't exist.
            if (!Directory.Exists(_logDirectory))
                Directory.CreateDirectory(_logDirectory);
            // Create today's log file if it doesn't exist.
            if (!File.Exists(_logFile))
                File.Create(_logFile).Dispose();

            string logText =
                $"{DateTime.UtcNow:hh:mm:ss} [{severity}] {source}: {message}";
            // Write the log text to a file.
            File.AppendAllText(_logFile, logText + "\n");

            // Write the log text to the console.
            return Console.Out.WriteLineAsync(logText);
        }

        private Task OnLogAsync(LogMessage msg)
        {
            return LogAsync(msg.Severity, msg.Source, msg.Exception?.ToString() ?? msg.Message);
        }
    }
}
