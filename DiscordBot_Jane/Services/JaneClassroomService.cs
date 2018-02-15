using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot_Jane.Core.Equality_Comparers;
using DiscordBot_Jane.Core.Utils;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Classroom.v1;
using Google.Apis.Classroom.v1.Data;
using Google.Apis.Requests;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Configuration;
using Color = Discord.Color;
using Timer = System.Timers.Timer;

namespace DiscordBot_Jane.Core.Services
{
    public class JaneClassroomService
    {
        public readonly List<Course> Courses = new List<Course>();
        public readonly Dictionary<string, IList<Teacher>> CourseTeachers = new Dictionary<string, IList<Teacher>>();
        public readonly Dictionary<string, KeyValuePair<Course, List<CourseWork>>> CourseWorks = new Dictionary<string, KeyValuePair<Course, List<CourseWork>>>();
        public readonly Dictionary<string, KeyValuePair<Course, List<Announcement>>> Announcements = new Dictionary<string, KeyValuePair<Course, List<Announcement>>>();

        private readonly DiscordSocketClient _discord;
        private readonly CommandService _commands;
        private readonly LoggingService _logger;
        private readonly IServiceProvider _provider;
        private readonly ChannelService _channelService;
        private readonly IConfigurationRoot _config;
        private readonly Random _random;

        private static readonly string[] Scopes =
        {
            ClassroomService.Scope.ClassroomCoursesReadonly,
            ClassroomService.Scope.ClassroomCourseworkMeReadonly,
            ClassroomService.Scope.ClassroomAnnouncementsReadonly,
            ClassroomService.Scope.ClassroomRostersReadonly,
            ClassroomService.Scope.ClassroomProfilePhotos,
            ClassroomService.Scope.ClassroomProfileEmails
        };

        private const string ApplicationName = "LBS Jane Discord Bot";

        private CancellationToken _token = default(CancellationToken);

        public JaneClassroomService(
            DiscordSocketClient discord,
            CommandService commands,
            LoggingService logger,
            IServiceProvider provider,
            ChannelService channelService,
            IConfigurationRoot config,
            Random random)
        {
            _discord = discord;
            _commands = commands;
            _logger = logger;
            _provider = provider;
            _channelService = channelService;
            _config = config;
            _random = random;

            _discord.Ready += StartPollDataFromClassroomTask;
        }

        private async Task StartPollDataFromClassroomTask()
        {
            _discord.Ready -= StartPollDataFromClassroomTask;

            using (ClassroomService service = await ClassroomAuthenticate())
            {
                // Get an initial set of data from classroom.
                await GetDataFromClassroomTask(service);

                // Get teachers data for courses. (This does not need to be constantly updated since it changes rarely.)
                await ClassroomUtils.GetTeachersForCourses(service, Courses, async (course, response, error) =>
                {
                    if (error != null)
                        await _logger.LogAsync(LogSeverity.Error, nameof(ClassroomUtils), error.Message);
                    else
                        CourseTeachers.Add(course.Name, response.Teachers);
                });

                /* 
                 * This loop does not end until application exits.
                 * This is the order of operations:
                 * 1. Get new data from classroom.
                 * 2. Check if anything new has been added to CourseWorks or Announcements and handle any new things.
                 * 3. Sleep for a period of time.
                 * 4. Repeat.
                 */
                var firstLoop = true;
                while (!_token.IsCancellationRequested)
                {
                    var oldCourseWorks = CourseWorks.ToDictionary(entry => entry.Key,
                                                                  entry => entry.Value);
                    var oldAnnouncements = Announcements.ToDictionary(entry => entry.Key,
                                                                      entry => entry.Value);

                    // No need to get data from classroom if this is the first  
                    // loop since it was already called before entering the loop.
                    if (firstLoop)
                        firstLoop = false;
                    else
                    {
                        // Time how long fetching data takes.
                        var stopWatch = new Stopwatch();
                        await _logger.LogAsync(LogSeverity.Info, nameof(JaneClassroomService),
                            "Fetching data from Classroom...");
                        stopWatch.Start();
                        // Fetch new data from classroom.
                        await GetDataFromClassroomTask(service);
                        // Stop timer and log how long fetching data from Classroom took.
                        stopWatch.Stop();
                        await _logger.LogAsync(LogSeverity.Info, nameof(JaneClassroomService),
                            $"Fetching data from Classroom completed! Took {stopWatch.ElapsedMilliseconds} ms");
                        
                        /*if (Program.InDebugMode)
                        {
                            // TODO: Remove this, this is for testing.
                            var temp = oldCourseWorks[oldCourseWorks.Keys.ElementAt(4)].Value[0];
                            CourseWorks[CourseWorks.Keys.ElementAt(4)].Value.Add(new CourseWork
                            {
                                AlternateLink = temp.AlternateLink,
                                Title = "THIS IS A TEST#" + _random.Next(1111, 9999),
                                Id = "123"
                            });

                            // TODO: Remove this, this is for testing.
                            CourseWorks[CourseWorks.Keys.ElementAt(1)].Value[0].DueDate = new Date {Day = 13, Month = 2, Year = 2018};
                            CourseWorks[CourseWorks.Keys.ElementAt(1)].Value[0].DueTime = new TimeOfDay {Hours = 12, Minutes = 30};
                        }*/

                        // Time how long checking for changes takes.
                        stopWatch.Reset();
                        await _logger.LogAsync(LogSeverity.Info, nameof(JaneClassroomService),
                            "Checking for changes in data...");
                        stopWatch.Start();
                        foreach (var pair in CourseWorks.Except(oldCourseWorks,
                            new CourseWorkDictionaryEqualityComparer()))
                        {
                            await _logger.LogAsync(LogSeverity.Debug, nameof(JaneClassroomService), $"CourseWork change detected in course {pair.Key}");
                            foreach (var newCourseWork in pair.Value.Value.Except(oldCourseWorks[pair.Key].Value, new CourseWorkEqualityComparer()))
                            {
                                // Handle in every guild.
                                foreach (var guild in _discord.Guilds)
                                {
                                    await HandleNewCourseWork(pair.Value.Key, newCourseWork, guild);
                                }
                            }
                        }
                        foreach (var pair in Announcements.Except(oldAnnouncements,
                            new CourseAnnouncementDictionaryEqualityComparer()))
                        {
                            await _logger.LogAsync(LogSeverity.Info, nameof(JaneClassroomService), $"Announcement change detected in course {pair.Key}");
                            foreach (var newAnnouncement in pair.Value.Value.Except(oldAnnouncements[pair.Key].Value, new CourseAnnouncementEqualityComparer()))
                            {
                                // Handle in every guild.
                                foreach (var guild in _discord.Guilds)
                                {
                                    await HandleNewCourseAnnouncement(pair.Value.Key, newAnnouncement, guild);
                                }
                            }
                        }
                        // Stop timer and print how long checking for changes in data took.
                        stopWatch.Stop();
                        await _logger.LogAsync(LogSeverity.Info, nameof(JaneClassroomService),
                            $"Checking for changes in data completed! Took {stopWatch.ElapsedMilliseconds} ms");
                    }

                    try
                    {
                        double interval = _config.GetValue("classroom_interval_minutes", 1.0);
                        await Task.Delay(TimeSpan.FromMinutes(interval), _token);
                    }
                    catch (TaskCanceledException e)
                    {
                        await _logger.LogAsync(LogSeverity.Error, nameof(JaneClassroomService), e.Message);
                        break;
                    }
                }
            }
        }

        private async Task HandleNewCourseWork(Course course, CourseWork newCourseWork, SocketGuild guild)
        {
            var formattedCourseWork =
                newCourseWork.Title
                .Replace("\n", " ")
                .Truncate(_config.GetValue("truncate_latest_messages", 25));
            await _logger.LogAsync(LogSeverity.Info, nameof(JaneClassroomService),
                $"Announcing new course work for \"{course.Name}\": \"{formattedCourseWork}\" in guild \"{guild.Name}\"");

            DateTimeOffset dateTimeOffset = ClassroomUtils.GetDateTimeOffsetFromDateAndTimeOfDay(newCourseWork.DueDate, newCourseWork.DueTime);

            // Find the teacher who created this announcement.
            var teacherPhotoUrl = newCourseWork.GetTeacher(course, CourseTeachers)?.Profile?.PhotoUrl ?? "https://cdn.discordapp.com/embed/avatars/0.png";

            // Create embed for new course work.
            var builder = new EmbedBuilder()
                .WithTitle(newCourseWork.Title ?? "Ingen titel")
                .WithDescription(newCourseWork.Description ?? "Ingen beskrivning")
                .WithUrl(newCourseWork.AlternateLink)
                .WithColor(new Color(54, 71, 79))
                .WithTimestamp(dateTimeOffset)
                .WithFooter(footer =>
                {
                    footer
                        .WithText("Inlämning")
                        .WithIconUrl(_config["urls:IconWhiteClock"]);
                })
                .WithThumbnailUrl(_config["urls:IconWhiteTask"])
                .WithAuthor(author =>
                {
                    var teacherImage = _config[$"classroom_courses:{course.Name}:TeacherImage"] ?? $"https:{teacherPhotoUrl}";
                    author
                        .WithName($"{course.Name} – {course.Section}")
                        .WithUrl(course.AlternateLink)
                        .WithIconUrl(teacherImage);
                });

            // Add materials as fields.
            if (newCourseWork.Materials != null)
                ClassroomUtils.AddMaterialsAsField(newCourseWork.Materials, builder);

            // Build embed.
            var embed = builder.Build();

            // Get mentions of all roles that should be mentioned.
            string announceMessage = ClassroomUtils.GetAnnounceMessageWithRolesMention(course, guild, _config, "NewCourseWork");

            // Announce new course work.
            await ClassroomUtils.AnnounceNews(guild, embed, announceMessage, _channelService, _logger);

            // Link all course work materials.
            //await LinkMaterialsNew(newCourseWork.Materials, guild.GetTextChannel(397542059596316673));
        }

        private async Task HandleNewCourseAnnouncement(Course course, Announcement newAnnouncement, SocketGuild guild)
        {
            var formattedAnnouncement =
                newAnnouncement.Text
                .Replace("\n", " ")
                .Truncate(_config.GetValue("truncate_latest_messages", 25));
            await _logger.LogAsync(LogSeverity.Info, nameof(JaneClassroomService),
                $"New announcement for \"{course.Name}\": \"{formattedAnnouncement}\" in guild \"{guild.Name}\"");

            DateTimeOffset creationTime = DateTimeOffset.MinValue;
            if (newAnnouncement.CreationTime is DateTime creationDateTime)
            {
                creationTime = new DateTimeOffset(creationDateTime);
            }

            // Find the teacher who created this announcement.
            var teacher = newAnnouncement.GetTeacher(course, CourseTeachers);
            var teacherName = teacher?.Profile?.Name?.FullName ?? "Error: Ingen lärare hittad";
            var teacherPhotoUrl = newAnnouncement.GetTeacher(course, CourseTeachers)?.Profile?.PhotoUrl ?? "https://cdn.discordapp.com/embed/avatars/0.png";

            // Create embed for new course announcement.
            var builder = new EmbedBuilder()
                .WithTitle(teacherName)
                .WithDescription(newAnnouncement.Text)
                .WithUrl(newAnnouncement.AlternateLink)
                .WithColor(new Color(0xF17A16))
                .WithTimestamp(creationTime)
                .WithFooter(footer =>
                {
                    footer
                        .WithText("Lades upp")
                        .WithIconUrl(_config["urls:IconWhiteClock"]);
                })
                .WithThumbnailUrl(_config["urls:IconWhiteAnnouncement"])
                .WithAuthor(author =>
                {
                    var teacherImage = _config[$"classroom_courses:{course.Name}:TeacherImage"] ?? $"https:{teacherPhotoUrl}";
                    author
                        .WithName($"{course.Name} - {course.Section}")
                        .WithUrl(course.AlternateLink)
                        .WithIconUrl(teacherImage);
                });

            // Add materials as fields.
            if (newAnnouncement.Materials != null)
                ClassroomUtils.AddMaterialsAsField(newAnnouncement.Materials, builder);

            // Build embed.
            var embed = builder.Build();

            // Get mentions of all roles that should be mentioned.
            string announceMessage = ClassroomUtils.GetAnnounceMessageWithRolesMention(course, guild, _config);

            // Announce new course announcement.
            await ClassroomUtils.AnnounceNews(guild, embed, announceMessage, _channelService, _logger);

            // Link all announcement materials.
            //await LinkMaterialsNew(newAnnouncement.Materials, guild.GetTextChannel(397542059596316673));
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
            
            if (response.Courses != null && response.Courses.Count > 0)
            {
                foreach (var course in response.Courses)
                {
                    // Add course to dictionaries.
                    if (!CourseWorks.ContainsKey(course.Name))
                        CourseWorks.Add(course.Name, new KeyValuePair<Course, List<CourseWork>>(course, new List<CourseWork>()));
                    if (!Announcements.ContainsKey(course.Name))
                        Announcements.Add(course.Name, new KeyValuePair<Course, List<Announcement>>(course, new List<Announcement>()));
                    if (!Courses.Contains(course))
                        Courses.Add(course);

                    // Add course work request for this course to batch request.
                    batchCourseInfoRequest.Queue<ListCourseWorkResponse>(
                        service.Courses.CourseWork.List(course.Id),
                        async (courseWorkResponse, error, i, message) =>
                        {
                            // Make sure there are no errors.
                            if (error != null)
                            {
                                await _logger.LogAsync(LogSeverity.Error, nameof(ClassroomUtils), error.Message);
                                return;
                            }

                            // Add course work to course work dictionary.
                            if (courseWorkResponse.CourseWork != null)
                            CourseWorks[course.Name] = new KeyValuePair<Course, List<CourseWork>>(course, courseWorkResponse.CourseWork.ToList());

                            // Log the latest course work if in debug mode.
                            if (Program.InDebugMode)
                            {
                                var firstCourseWorkTitle = _config["messages:ClassroomNoCourseWork"];
                                if (courseWorkResponse.CourseWork?[0] != null)
                                    firstCourseWorkTitle = courseWorkResponse.CourseWork[0].Title;

                                var latestCourseWork =
                                    firstCourseWorkTitle
                                    .Replace("\n", " ")
                                    .Truncate(_config.GetValue("truncate_latest_messages", 25));
                                await _logger.LogAsync(LogSeverity.Debug, nameof(JaneClassroomService),
                                    $"[{course.Name}] Latest work: \"{latestCourseWork}\"");
                            }
                        });

                    // Add course work request for this course to batch request.
                    batchCourseInfoRequest.Queue<ListAnnouncementsResponse>(
                        service.Courses.Announcements.List(course.Id),
                        async (courseAnnouncementsResponse, error, i, message) =>
                        {
                            // Make sure there are no errors.
                            if (error != null)
                            {
                                await _logger.LogAsync(LogSeverity.Error, nameof(ClassroomUtils), error.Message);
                                return;
                            }

                            // Add announcements to announcements dictionary.
                            if (courseAnnouncementsResponse.Announcements != null)
                            Announcements[course.Name] = new KeyValuePair<Course, List<Announcement>>(course, courseAnnouncementsResponse.Announcements.ToList());

                            // Log the latest announcement if in debug mode.
                            if (Program.InDebugMode)
                            {
                                var firstCourseWorkTitle = _config["messages:ClassroomNoCourseAnnouncements"];
                                if (courseAnnouncementsResponse.Announcements?[0] != null)
                                    firstCourseWorkTitle =
                                        courseAnnouncementsResponse.Announcements[0].Text;

                                var latestAnnouncement =
                                    firstCourseWorkTitle
                                    .Replace("\n", " ")
                                    .Truncate(_config.GetValue("truncate_latest_messages", 25));
                                await _logger.LogAsync(LogSeverity.Debug, nameof(JaneClassroomService),
                                    $"[{course.Name}] Latest announcement: \"{latestAnnouncement}\"");
                            }
                        });
                }
                // Execute batch request.
                await batchCourseInfoRequest.ExecuteAsync(_token);
            }
            else
            {
                await _logger.LogAsync(LogSeverity.Error, nameof(JaneClassroomService), "No courses found.");
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
                credPath = Path.Combine(credPath,
                    ".credentials/classroom.googleapis.com-dotnet-discord-bot-jane-lbs.json");

                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true)).Result;
                await _logger.LogAsync(LogSeverity.Info, nameof(JaneClassroomService),
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
