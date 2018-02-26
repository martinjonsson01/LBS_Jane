using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using static DiscordBot_Jane.Core.Utils.CompressionUtils;

namespace DiscordBot_Jane.Core.Services
{
    public class LoggingService
    {
        private readonly DiscordSocketClient _discord;
        private readonly CommandService _commands;
        private readonly NotifyIcon _notifyIcon;
        private readonly IConfigurationRoot _config;

        private static ReaderWriterLock rwl = new ReaderWriterLock();

        public string LogDirectory { get; }
        public string LogFile => GetLogFile(DateTime.Now);

        // DiscordSocketClient and CommandService are injected automatically from the IServiceProvider.
        public LoggingService(DiscordSocketClient discord, CommandService commands, NotifyIcon notifyIcon, IConfigurationRoot config)
        {
            LogDirectory = Path.Combine(AppContext.BaseDirectory, "logs");

            _discord = discord;
            _commands = commands;
            _notifyIcon = notifyIcon;
            _config = config;
            
            _discord.Log += OnLogAsync;
            _commands.Log += OnLogAsync;
        }

        public Task LogAsync(LogSeverity severity, string source, string message)
        {
            // If message to be logged is debug message and program is not in debug mode, don't log it.
            if (severity == LogSeverity.Debug && !Program.InDebugMode)
                return null;

            // If logging an error, display a notification to the host as well.
            if (severity == LogSeverity.Error || severity == LogSeverity.Critical)
            {
                var errorString = severity == LogSeverity.Error ? "Error" : "Critical Error";
                _notifyIcon.BalloonTipTitle = $"{errorString} in {Application.ProductName}";
                _notifyIcon.BalloonTipText = message;
                _notifyIcon.ShowBalloonTip(1000);
            }

            // Create the log directory if it doesn't exist.
            if (!Directory.Exists(LogDirectory))
                Directory.CreateDirectory(LogDirectory);
            // Create today's log file if it doesn't exist and compress old log file.
            if (!File.Exists(LogFile))
            {
                // Create today's log file.
                File.Create(LogFile).Dispose();
                // Get old log file if it exists.
                var oldLogFile = GetLogFile(DateTime.Now.Subtract(TimeSpan.FromDays(1)));
                if (File.Exists(oldLogFile))
                {
                    // Compress it.
                    Compress(oldLogFile, LogDirectory);
                    // Delete uncompressed version.
                    File.Delete(oldLogFile);
                }
            }

            try
            {
                rwl.AcquireWriterLock(200);
                try
                {
                    string logText =
                        $"{DateTime.Now:HH:mm:ss.fff} [{severity}] {source}: {message}";
                    // Write the log text to a file.
                    File.AppendAllText(LogFile, logText + "\n");
                }
                finally
                {
                    rwl.ReleaseWriterLock();
                }
            }
            catch (ApplicationException)
            {
                Console.WriteLine("Error: Writer lock request timed out.");
            }
            
            // Write the log text to the console.
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Out.WriteAsync($"{DateTime.Now:HH:mm:ss.fff} ");
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

        public List<IList<object>> GetLogData(bool reverse = false)
        {
            var logs = new List<IList<object>>();

            try
            {
                rwl.AcquireReaderLock(200);
                try
                {
                    var rows = File.ReadAllLines(LogFile).ToList();
                    foreach (var row in rows)
                    {
                        var date = row.Split(new[] { ' ' }, 3);
                        var rowData = new List<object>
                        {
                            date[0],
                            date[1],
                            date[2]
                        };
                        logs.Add(rowData);
                    }
                    if (reverse)
                        logs.Reverse();
                }
                finally
                {
                    rwl.ReleaseReaderLock();
                }
            }
            catch (ApplicationException)
            {
                Console.WriteLine("Error: Reader lock request timed out.");
            }
            return logs;
        }

        private async Task OnLogAsync(LogMessage msg)
        {
            if (msg.Exception is CommandException e)
            {
                // Inform user about command error.
                await e.Context.Channel.SendFileAsync($"gifs/{_config["gifs:error_command"] ?? "sweat"}.gif").ConfigureAwait(false);
                await e.Context.Channel
                    .SendMessageAsync(
                        $"... Verkar som om något gick fel... (ser ut som att {e.InnerException?.Message ?? "idk"})")
                    .ConfigureAwait(false);
            }
            await LogAsync(msg.Severity, msg.Source, msg.Exception?.ToString() ?? msg.Message).ConfigureAwait(false);
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
                case nameof(ReminderService):
                    return ConsoleColor.DarkYellow;
                case nameof(GDocsService):
                    return ConsoleColor.DarkMagenta;
                default:
                    return ConsoleColor.Gray;
            }
        }

        private string GetLogFile(DateTime dateTime)
        {
            return Path.Combine(LogDirectory, $"{dateTime:yyyy-MM-dd}.txt");
        }
    }
}
