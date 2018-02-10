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
            // If message to be logged is debug message and program is not in debug mode, don't log it.
            if (severity == LogSeverity.Debug && !Program.InDebugMode)
                return null;

            // Create the log directory if it doesn't exist.
            if (!Directory.Exists(_logDirectory))
                Directory.CreateDirectory(_logDirectory);
            // Create today's log file if it doesn't exist.
            if (!File.Exists(_logFile))
                File.Create(_logFile).Dispose();

            string logText =
                $"{DateTime.Now:HH:mm:ss} [{severity}] {source}: {message}";
            // Write the log text to a file.
            File.AppendAllText(_logFile, logText + "\n");
            
            // Write the log text to the console.
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Out.WriteAsync($"{DateTime.Now:HH:mm:ss} ");
            Console.ForegroundColor = GetLogSeverityColor(severity);
            var displaySeverity = $"[{severity}] ";
            //Console.Out.WriteAsync($"{displaySeverity, -10}");
            Console.Out.WriteAsync($"{displaySeverity}");
            Console.ForegroundColor = GetSourceColor(source);
            var displaySource = $"{source}: ";
            //Console.Out.WriteAsync($"{displaySource, -10}");
            Console.Out.WriteAsync($"{displaySource}");
            Console.ForegroundColor = ConsoleColor.White;
            return Console.Out.WriteLineAsync($"{message}");
            //return Console.Out.WriteLineAsync(logText);
        }

        private Task OnLogAsync(LogMessage msg)
        {
            return LogAsync(msg.Severity, msg.Source, msg.Exception?.ToString() ?? msg.Message);
        }

        private ConsoleColor GetLogSeverityColor(LogSeverity severity)
        {
            switch (severity)
            {
                case LogSeverity.Info:
                    return ConsoleColor.Blue;
                case LogSeverity.Verbose:
                    return ConsoleColor.DarkBlue;
                case LogSeverity.Debug:
                    return ConsoleColor.DarkGreen;
                case LogSeverity.Warning:
                    return ConsoleColor.Yellow;
                case LogSeverity.Error:
                    return ConsoleColor.DarkYellow;
                case LogSeverity.Critical:
                    return ConsoleColor.DarkRed;
                default:
                    return ConsoleColor.White;
            }
        }

        private ConsoleColor GetSourceColor(string source)
        {
            switch (source)
            {
                case nameof(JaneClassroomService):
                    return ConsoleColor.Magenta;
                default:
                    return ConsoleColor.Gray;
            }
        }
    }
}
