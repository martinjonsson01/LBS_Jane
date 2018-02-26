using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot_Jane.Core.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Classroom.v1;
using Google.Apis.Sheets.v4;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Configuration;

namespace DiscordBot_Jane.Core.Services
{
    public class GoogleAuthenticateService
    {
        private readonly DiscordSocketClient _discord;
        private readonly CommandService _commands;
        private readonly IConfigurationRoot _config;
        private readonly LoggingService _logger;

        private static readonly string[] Scopes =
        {
            ClassroomService.Scope.ClassroomCoursesReadonly,
            ClassroomService.Scope.ClassroomCourseworkMeReadonly,
            ClassroomService.Scope.ClassroomAnnouncementsReadonly,
            ClassroomService.Scope.ClassroomRostersReadonly,
            ClassroomService.Scope.ClassroomProfilePhotos,
            ClassroomService.Scope.ClassroomProfileEmails,
            SheetsService.Scope.Spreadsheets,
        };

        /*/// <summary>
        /// Fired when a Google credential token has been aquired.
        /// </summary>
        public event Func<UserCredential, Task> GoogleAuthenticated
        {
            add => _googleAuthenticatedEvent.Add(value);
            remove => _googleAuthenticatedEvent.Remove(value);
        }
        private readonly AsyncEvent<Func<UserCredential, Task>> _googleAuthenticatedEvent = new AsyncEvent<Func<UserCredential, Task>>();*/
        public event EventHandler<UserCredential> GoogleAuthenticated;

        public GoogleAuthenticateService(DiscordSocketClient discord, CommandService commands, IConfigurationRoot config, LoggingService logger)
        {
            _discord = discord;
            _commands = commands;
            _config = config;
            _logger = logger;
        }

        public async void GoogleAuthenticate()
        {
            UserCredential credential;

            using (var stream =
                new FileStream("client_secret.json", FileMode.Open, FileAccess.Read))
            {
                string credPath = Environment.GetFolderPath(
                    Environment.SpecialFolder.Personal);
                credPath = Path.Combine(credPath,
                    ".credentials/classroom.googleapis.com-dotnet-discord-bot-jane-lbs.json");

                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true)).Result;
                await _logger.LogAsync(LogSeverity.Info, nameof(JaneClassroomService),
                    "Credential file saved to: " + credPath).ConfigureAwait(false);
            }

            // Invoke event.
            GoogleAuthenticated?.Invoke(this, credential);
        }
    }
}
