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
using Google.Apis.Auth.OAuth2;
using Google.Apis.Classroom.v1;
using Google.Apis.Classroom.v1.Data;
using Google.Apis.Requests;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace DiscordBot_Jane.Services
{
    public class JaneClassroomService
    {
        public List<Course> Courses = new List<Course>();
        public Dictionary<Course, List<CourseWork>> CourseWorks = new Dictionary<Course, List<CourseWork>>();
        public Dictionary<Course, List<Announcement>> Announcements = new Dictionary<Course, List<Announcement>>();

        private readonly DiscordSocketClient _discord;
        private readonly CommandService _commands;
        private readonly LoggingService _logger;
        private readonly IServiceProvider _provider;

        private static string[] Scopes =
        {
            ClassroomService.Scope.ClassroomCoursesReadonly,
            ClassroomService.Scope.ClassroomCourseworkMeReadonly,
            ClassroomService.Scope.ClassroomAnnouncementsReadonly,
            ClassroomService.Scope.ClassroomPushNotifications
        };
        private static string ApplicationName = "LBS Jane Discord Bot";

        private CancellationToken _token = default(CancellationToken);

        public JaneClassroomService(
            DiscordSocketClient discord,
            CommandService commands,
            LoggingService logger,
            IServiceProvider provider)
        {
            _discord = discord;
            _commands = commands;
            _logger = logger;
            _provider = provider;
            _discord.Ready += StartPollDataFromClassroomTask;
        }

        private async Task StartPollDataFromClassroomTask()
        {
            ClassroomService service = await ClassroomAuthenticate();
            
            while (!_token.IsCancellationRequested)
            {
                var oldCourseWorks = CourseWorks;
                var oldAnnouncements = Announcements;
                await GetDataFromClassroomTask(service);

                //var newCourseWorks = new Dictionary<Course, List<CourseWork>>();
                foreach (var pair in oldCourseWorks.Except(CourseWorks))
                {
                    //newCourseWorks[pair.Key] = pair.Value;
                    await HandleNewCourseAnnouncement(pair.Key, pair.Value);
                }
                //var newAnnouncements = new Dictionary<Course, List<Announcement>>();
                foreach (var pair in oldAnnouncements.Except(Announcements))
                {
                    //newAnnouncements[pair.Key] = pair.Value;
                    await HandleNewCourseWork(pair.Key, pair.Value);
                }

                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(Config.ClassroomCallIntervalMinutes), _token);
                }
                catch (TaskCanceledException e)
                {
                    await _logger.LogAsync(LogSeverity.Error, nameof(ClassroomService), e.Message);
                    break;
                }
            }
        }

        private async Task HandleNewCourseAnnouncement(Course course, List<CourseWork> courseWorks)
        {
            if (Program.InDebugMode)
                await _logger.LogAsync(LogSeverity.Info, nameof(ClassroomService), $"New course work for {course.Name}: {courseWorks[0].Title} : {courseWorks[0].Description}");
            await ((ISocketMessageChannel)_discord.Guilds.ElementAt(0).GetChannel(397542059596316673))
                .SendMessageAsync($"New course work for {course.Name}: {courseWorks[0].Title} : {courseWorks[0].Description}, <!153217095625211905>");
        }

        private async Task HandleNewCourseWork(Course course, List<Announcement> announcements)
        {
            if (Program.InDebugMode)
                await _logger.LogAsync(LogSeverity.Info, nameof(ClassroomService), $"New announcement for {course.Name}: {announcements[0].Text}, <!153217095625211905>");
            await ((ISocketMessageChannel)_discord.Guilds.ElementAt(0).GetChannel(397542059596316673))
                .SendMessageAsync($"New announcement for {course.Name}: {announcements[0].Text}");
        }

        private async Task GetDataFromClassroomTask(ClassroomService service)
        {
            // Define request parameters.
            var coursesRequest = service.Courses.List();
            coursesRequest.PageSize = 20;

            // Prepare batch request.
            var batchCourseInfoRequest = new BatchRequest(service);

            // List courses.
            ListCoursesResponse response = await coursesRequest.ExecuteAsync(_token);
            if (Program.InDebugMode)
                await _logger.LogAsync(LogSeverity.Info, nameof(ClassroomService), "Courses:");
            if (response.Courses != null && response.Courses.Count > 0)
            {
                foreach (var course in response.Courses)
                {
                    if (Program.InDebugMode)
                        await _logger.LogAsync(LogSeverity.Info, nameof(ClassroomService), $"{course.Name} ({course.Id})");
                    
                    // Add course to dictionaries.
                    CourseWorks.Add(course, new List<CourseWork>());
                    Announcements.Add(course, new List<Announcement>());

                    // Add course work request for this course to batch request.
                    batchCourseInfoRequest.Queue<ListCourseWorkResponse>(service.Courses.CourseWork.List(course.Id),
                        async (courseWorkResponse, error, i, message) =>
                        {
                            // Add course work to course work dictionary.
                            if (courseWorkResponse.CourseWork != null)
                                CourseWorks[course] = courseWorkResponse.CourseWork.ToList();

                            // Log the latest course work if in debug mode.
                            if (Program.InDebugMode)
                            {
                                var firstCourseWorkTitle = Config.ClassroomNoCourseWork;
                                if (courseWorkResponse.CourseWork?[0] != null)
                                    firstCourseWorkTitle = courseWorkResponse.CourseWork[0].Title;
                                
                                await _logger.LogAsync(LogSeverity.Info, nameof(ClassroomService),
                                    $"[{course.Name}] Latest work: {firstCourseWorkTitle}");
                            }
                        });
                    
                    // Add course work request for this course to batch request.
                    batchCourseInfoRequest.Queue<ListAnnouncementsResponse>(service.Courses.Announcements.List(course.Id),
                        async (courseAnnouncementsResponse, error, i, message) =>
                        {
                            // Add announcements to announcements dictionary.
                            if (courseAnnouncementsResponse.Announcements != null)
                                Announcements[course] = courseAnnouncementsResponse.Announcements.ToList();

                            // Log the latest announcement if in debug mode.
                            if (Program.InDebugMode)
                            {
                                var firstCourseWorkTitle = Config.ClassroomNoCourseAnnouncements;
                                if (courseAnnouncementsResponse.Announcements?[0] != null)
                                    firstCourseWorkTitle = courseAnnouncementsResponse.Announcements[0].Text;

                                await _logger.LogAsync(LogSeverity.Info, nameof(ClassroomService),
                                    $"[{course.Name}] Latest announcement: {firstCourseWorkTitle}");
                            }
                        });
                }
                // Execute batch request.
                await batchCourseInfoRequest.ExecuteAsync(_token);
            }
            else
            {
                await _logger.LogAsync(LogSeverity.Error, nameof(ClassroomService), "No courses found.");
            }
        }

        private async Task<ClassroomService> ClassroomAuthenticate()
        {
            UserCredential credential;

            using (var stream =
                new FileStream("client_secret.json", FileMode.Open, FileAccess.Read))
            {
                string credPath = System.Environment.GetFolderPath(
                    System.Environment.SpecialFolder.Personal);
                credPath = Path.Combine(credPath, ".credentials/classroom.googleapis.com-dotnet-discord-bot-jane-lbs.json");

                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true)).Result;
                await _logger.LogAsync(LogSeverity.Info, nameof(ClassroomService),
                    "Credential file saved to: " + credPath);
            }

            // Create Classroom API service.
            var service = new ClassroomService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });
            return service;
        }
    }
}
