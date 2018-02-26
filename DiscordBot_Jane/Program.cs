using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DiscordBot_Jane.Core.Services;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot_Jane.Core
{
    public class Program
    {
        public static bool InDebugMode { get; private set; }
        
        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(EventHandler handler, bool add);

        private delegate bool EventHandler(CtrlType sig);

        private static EventHandler _handler;

        private enum CtrlType
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }

        /// <summary>
        /// Try not to use this whenever possible.
        /// </summary>
        public static Program Instance;

        public static void Main(string[] args)
        {
            #if DEBUG
                InDebugMode = true;
            #else
                InDebugMode = false;
            #endif
            // Change window size.
            Console.SetWindowSize(1, 1);
            // Make sure application isn't already running.
            var mutexId = $"BOT_DISCORD_JANE_DEBUG:{InDebugMode}";
            using (var mutex = new Mutex(false, mutexId))
            {
                if (!mutex.WaitOne(0, false))
                {
                    MessageBox.Show("Instans Redan Igång!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                    return;
                }
                // Some biolerplate to react to close window event, CTRL-C, kill, etc...
                _handler += ExitFromOutsideSource;
                SetConsoleCtrlHandler(_handler, true);
                // Start program.
                Instance = new Program();
                Instance.StartAsync().GetAwaiter().GetResult();
            }
        }

        private static bool ExitFromOutsideSource(CtrlType sig)
        {
            Instance._logger.LogAsync(LogSeverity.Info, nameof(Program),
                $"Exiting system due to external {sig}");
            
            // Graceful exit.
            Instance.GracefulExit();
            
            Instance._logger.LogAsync(LogSeverity.Info, nameof(Program), "Cleanup complete");

            return true;
        }

        public void GracefulExit()
        {
            // Cancel GetDataFromClassroom repeating task.
            _janeClassroomService.CancellationToken.Cancel();
            // Cancel GDocsService repeating task.
            _gDocsService.CancellationToken.Cancel();
            // Cancel CheckReminders repeating task.
            _reminderService.CancellationToken.Cancel();

            // Disconnect from discord.
            var discordStopTask =  _discord.StopAsync();
            // Wait for task to complete.
            discordStopTask.Wait(TimeSpan.FromSeconds(4));

            // Shut down application.
            Application.Exit();
        }

        private DiscordSocketClient _discord;
        private JaneClassroomService _janeClassroomService;
        private GDocsService _gDocsService;
        private ReminderService _reminderService;
        private LoggingService _logger;
        private IConfigurationRoot _config;
        private bool _visible = true;

        private async Task StartAsync()
        {
            var builder = new ConfigurationBuilder()        // Create a new instance of the config builder
                .SetBasePath(AppContext.BaseDirectory)      // Specify the default location for the config file
                .AddJsonFile("_configuration.json");        // Add this (json encoded) file to the configuration
            _config = builder.Build();                      // Build the configuration

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
                .AddSingleton<CommandHandler>()            // Add remaining services to the provider.
                .AddSingleton<LoggingService>()            // Allows us to log messages to the log file and console.
                .AddSingleton<StartupService>()            // Starts up the bot.
                .AddSingleton<ChannelService>()            // Handles creating required channels.
                .AddSingleton<JaneClassroomService>()      // Handles all interactions with Google Classroom.
                .AddSingleton<GDocsService>()              // Handles all interactions with Google Docs.
                .AddSingleton<ReminderService>()           // Schedules and executes all reminders.
                .AddSingleton<GoogleAuthenticateService>() // Authenticates with google.
                .AddSingleton<NotifyIcon>()                // Tray icon for controlling application while in background.
                .AddSingleton<Random>()                    // You get better random with a single instance than by creating a new one every time you need it.
                .AddSingleton(_config);                    // Add the configuration to the collection.

            // Create the service provider.
            var provider = services.BuildServiceProvider();

            // Initialize services.
            _logger = provider.GetRequiredService<LoggingService>();
            var notifyIcon = provider.GetRequiredService<NotifyIcon>();
            await provider.GetRequiredService<StartupService>().StartAsync().ConfigureAwait(false);
            provider.GetRequiredService<ChannelService>();
            _janeClassroomService = provider.GetRequiredService<JaneClassroomService>();
            _gDocsService = provider.GetRequiredService<GDocsService>();
            _reminderService = provider.GetRequiredService<ReminderService>();
            provider.GetRequiredService<CommandHandler>();
            _discord = provider.GetRequiredService<DiscordSocketClient>();
            // Initialize GoogleAuthenticateService last since several 
            // other services need to register their events for it first.
            // Authenticate with Google.
            provider.GetRequiredService<GoogleAuthenticateService>().GoogleAuthenticate();

            // Set up NotifyIcon.
            SetUpNotifyIcon(notifyIcon, _logger);

            // Hide console window.
            Hide();
            // Run the application. (This handles events for the NotifyIcon and similar windows things.)
            Application.Run(); // If anything blow this line gets executed then the application is shutting off.

            // Hide NotifyIcon.
            notifyIcon.Visible = false;
        }
        
        private void SetUpNotifyIcon(NotifyIcon notifyIcon, LoggingService logger)
        {
            notifyIcon.Icon = new Icon("icon.ico", 60, 60);
            notifyIcon.Visible = true;
            MenuItem[] menuList =
            {
                new MenuItem("Show/Hide Console", (s, e) => ToggleShow()) {DefaultItem = true},
                new MenuItem("-"),
                new MenuItem("Open log folder", (s, e) => Process.Start(logger.LogDirectory)),
                new MenuItem("Open config", (s, e) => Process.Start(AppContext.BaseDirectory + "_configuration.json")),
                new MenuItem("-"),
                new MenuItem("Exit", (s, e) => GracefulExit())
            };
            var clickMenu = new ContextMenu(menuList);
            notifyIcon.ContextMenu = clickMenu;
            notifyIcon.Click += (s, e) =>
            {
                if (!(e is MouseEventArgs me)) return;
                if (me.Button == MouseButtons.Left) ToggleShow();
            };
            notifyIcon.DoubleClick += (s, e) =>
            {
                ToggleShow();
                Process.Start(logger.LogFile);
            };
        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SwHide = 0;
        private const int SwShow = 5;

        private void Show()
        {
            var handle = GetConsoleWindow();
            // Show.
            ShowWindow(handle, SwShow);
            // Check window size and modify if necessary.
            if (Console.WindowWidth != 200 || Console.WindowHeight != Console.LargestWindowHeight - 20)
                Console.SetWindowSize(200, Console.LargestWindowHeight - 20);
            _visible = true;
        }

        private void Hide()
        {
            var handle = GetConsoleWindow();
            // Hide.
            ShowWindow(handle, SwHide);
            _visible = false;
        }

        private void ToggleShow()
        {
            if (_visible) Hide();
            else if (!_visible) Show();
        }
    }
}
