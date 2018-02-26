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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Color = Discord.Color;

namespace DiscordBot_Jane.Core.Services
{
    public class JaneClassroomService
    {
        public readonly List<Course> Courses = new List<Course>();
        public readonly Dictionary<string, IList<Teacher>> CourseTeachers = new Dictionary<string, IList<Teacher>>();
        public readonly Dictionary<string, KeyValuePair<Course, List<CourseWork>>> CourseWorks = new Dictionary<string, KeyValuePair<Course, List<CourseWork>>>();
        public readonly Dictionary<string, KeyValuePair<Course, List<Announcement>>> Announcements = new Dictionary<string, KeyValuePair<Course, List<Announcement>>>();

        private readonly DiscordSocketClient _discord;
        private readonly GoogleAuthenticateService _gAuth;
        private readonly CommandService _commands;
        private readonly LoggingService _logger;
        private readonly IServiceProvider _provider;
        private readonly ChannelService _channelService;
        private readonly IConfigurationRoot _config;
        private readonly Random _random;

        private const string ApplicationName = "LBS Jane Discord Bot";

        public readonly CancellationTokenSource CancellationToken = new CancellationTokenSource();

        public JaneClassroomService(
            DiscordSocketClient discord,
            GoogleAuthenticateService gAuth,
            CommandService commands,
            LoggingService logger,
            IServiceProvider provider,
            ChannelService channelService,
            IConfigurationRoot config,
            Random random)
        {
            _discord = discord;
            _gAuth = gAuth;
            _commands = commands;
            _logger = logger;
            _provider = provider;
            _channelService = channelService;
            _config = config;
            _random = random;

            _gAuth.GoogleAuthenticated += StartPollDataFromClassroomTask;
        }

        private async void StartPollDataFromClassroomTask(object sender, UserCredential credential)
        {
            _gAuth.GoogleAuthenticated -= StartPollDataFromClassroomTask;

            // Create Classroom API service.
            using (var service = new ClassroomService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            }))
            {
                /* 
                 * This loop does not end until application exits.
                 * This is the order of operations:
                 * 1. Get new data from classroom.
                 * 2. Check if anything new has been added to CourseWorks or Announcements and handle any new things.
                 * 3. Sleep for a period of time.
                 * 4. Repeat.
                 */
                var firstLoop = true;
                while (!CancellationToken.IsCancellationRequested)
                {
                    var oldCourseWorks = CourseWorks.ToDictionary(entry => entry.Key,
                                                                  entry => entry.Value);
                    var oldAnnouncements = Announcements.ToDictionary(entry => entry.Key,
                                                                      entry => entry.Value);

                    // Time how long fetching data takes.
                    var stopWatch = new Stopwatch();
                    await _logger.LogAsync(LogSeverity.Info, nameof(JaneClassroomService),
                        "Fetching data from Classroom...").ConfigureAwait(false);
                    stopWatch.Start();
                    // Fetch new data from classroom.
                    await GetDataFromClassroomTask(service).ConfigureAwait(false);
                    // Stop timer and log how long fetching data from Classroom took.
                    stopWatch.Stop();
                    await _logger.LogAsync(LogSeverity.Info, nameof(JaneClassroomService),
                        $"Fetching data from Classroom completed! Took {stopWatch.ElapsedMilliseconds} ms").ConfigureAwait(false);

                    /*if (Program.InDebugMode)
                    {
                        if (oldAnnouncements.Count > 0)
                        {
                            // TODO: Remove this, this is for testing.
                            foreach (var course in Courses)
                            {
                                var temp = Announcements[course.Name].Value[0];
                                Announcements[course.Name].Value.Add(new Announcement
                                {
                                    AlternateLink = temp.AlternateLink,
                                    Text = "THIS IS A TEST#" + _random.Next(999, 10000),
                                    Id = "123"
                                });
                            }
                        }
                    }*/

                    // Time how long checking for changes takes.
                    stopWatch.Reset();
                    await _logger.LogAsync(LogSeverity.Info, nameof(JaneClassroomService),
                        "Checking for changes in data...").ConfigureAwait(false);
                    stopWatch.Start();
                    if (oldCourseWorks.Count > 0)
                    {
                        foreach (var pair in CourseWorks.Except(oldCourseWorks,
                            new CourseWorkDictionaryEqualityComparer()))
                        {
                            if (pair.Key == null)
                            {
                                await _logger.LogAsync(LogSeverity.Error, nameof(JaneClassroomService),
                                    "CourseWork change detected in null course.").ConfigureAwait(false);
                                return;
                            }
                            await _logger.LogAsync(LogSeverity.Info, nameof(JaneClassroomService),
                                $"CourseWork change detected in course {pair.Key}").ConfigureAwait(false);
                            foreach (var newCourseWork in pair.Value.Value.Except(oldCourseWorks[pair.Key].Value,
                                new CourseWorkEqualityComparer()))
                            {
                                // Check if course work was updated or is completely new.
                                bool updated = oldCourseWorks[pair.Key].Value.Any(x => x.Id == newCourseWork.Id);

                                // Handle in every guild.
                                foreach (var guild in _discord.Guilds)
                                {
                                    // If guild isn't added to the classroom blacklist.
                                    if (!ConfigUtils.ContainsValueAt("classroom_guilds_blacklist", guild.Name, _config))
                                    {
                                        await HandleNewCourseWork(pair.Value.Key, newCourseWork, updated, guild)
                                            .ConfigureAwait(false);
                                    }
                                }
                            }
                        }
                    }
                    if (oldAnnouncements.Count > 0)
                    {
                        foreach (var pair in Announcements.Except(oldAnnouncements,
                            new CourseAnnouncementDictionaryEqualityComparer()))
                        {
                            if (pair.Key == null)
                            {
                                await _logger.LogAsync(LogSeverity.Error, nameof(JaneClassroomService),
                                    "Announcement change detected in null course.").ConfigureAwait(false);
                                return;
                            }
                            await _logger.LogAsync(LogSeverity.Info, nameof(JaneClassroomService),
                                $"Announcement change detected in course {pair.Key}").ConfigureAwait(false);
                            foreach (var newAnnouncement in pair.Value.Value.Except(oldAnnouncements[pair.Key].Value,
                                new CourseAnnouncementEqualityComparer()))
                            {
                                // Check if announcement was updated or is completely new.
                                bool updated = oldAnnouncements[pair.Key].Value.Any(x => x.Id == newAnnouncement.Id);

                                // Handle in every guild.
                                foreach (var guild in _discord.Guilds)
                                {
                                    // If guild isn't added to the classroom blacklist.
                                    if (!ConfigUtils.ContainsValueAt("classroom_guilds_blacklist", guild.Name, _config))
                                    {
                                        await HandleNewCourseAnnouncement(pair.Value.Key, newAnnouncement, updated, guild)
                                            .ConfigureAwait(false);
                                    }
                                }
                            }
                        }
                    }
                    // Stop timer and print how long checking for changes in data took.
                    stopWatch.Stop();
                    await _logger.LogAsync(LogSeverity.Info, nameof(JaneClassroomService),
                        $"Checking for changes in data completed! Took {stopWatch.ElapsedMilliseconds} ms").ConfigureAwait(false);

                    // Only get teachers data for courses if this is the first loop.
                    if (firstLoop)
                    {
                        firstLoop = false;
                        // Get teachers data for courses. (This does not need to be constantly updated since it changes rarely.)
                        await ClassroomUtils.GetTeachersForCourses(service, Courses, async (course, response, error) =>
                        {
                            if (error != null)
                                await _logger.LogAsync(LogSeverity.Error, nameof(ClassroomUtils), error.Message).ConfigureAwait(false);
                            else
                                CourseTeachers.Add(course.Name, response.Teachers);
                        }).ConfigureAwait(false);
                    }

                    // Wait for a period of time before repeating.
                    try
                    {
                        double interval = _config.GetValue("classroom_interval_minutes", 1.0);
                        await Task.Delay(TimeSpan.FromMinutes(interval), CancellationToken.Token).ConfigureAwait(false);
                    }
                    catch (TaskCanceledException e)
                    {
                        await _logger.LogAsync(LogSeverity.Info, nameof(JaneClassroomService), e.Message).ConfigureAwait(false);
                        break;
                    }
                }
            }
        }

        private async Task HandleNewCourseWork(Course course, CourseWork newCourseWork, bool updated, SocketGuild guild)
        {
            // Don't announce new course work for blacklisted courses.
            if (ConfigUtils.CourseIsBlackListed(course.Name, _config)) return;

            var formattedCourseWork =
                newCourseWork.Title
                .Replace("\n", " ")
                .Truncate(_config.GetValue("truncate_latest_messages", 25));
            await _logger.LogAsync(LogSeverity.Info, nameof(JaneClassroomService),
                $"Announcing course work change for \"{course.Name}\": \"{formattedCourseWork}\" in guild \"{guild.Name}\"").ConfigureAwait(false);

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
                    var teacherImage = _config[$"classroom_courses:{course.Name}:TeacherImage"] ?? teacherPhotoUrl;
                    if (!teacherImage.StartsWith("https:")) teacherImage = $"https:{teacherImage}";
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
            string announceMessage = ClassroomUtils.GetAnnounceMessageWithRolesMention(course, guild, _config, updated ? "UpdatedCourseWork" : "NewCourseWork");

            // Announce new course work.
            await ClassroomUtils.AnnounceNews(guild, embed, announceMessage, _channelService, _logger).ConfigureAwait(false);

            // Link all course work materials.
            //await LinkMaterialsNew(newCourseWork.Materials, guild.GetTextChannel(397542059596316673)).ConfigureAwait(false);
        }

        private async Task HandleNewCourseAnnouncement(Course course, Announcement newAnnouncement, bool updated, SocketGuild guild)
        {
            // Don't announce new course announcements for blacklisted courses.
            if (ConfigUtils.CourseIsBlackListed(course.Name, _config)) return;

            var formattedAnnouncement =
                newAnnouncement.Text
                .Replace("\n", " ")
                .Truncate(_config.GetValue("truncate_latest_messages", 25));
            await _logger.LogAsync(LogSeverity.Info, nameof(JaneClassroomService),
                $"New announcement for \"{course.Name}\": \"{formattedAnnouncement}\" in guild \"{guild.Name}\"").ConfigureAwait(false);

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
                    var teacherImage = _config[$"classroom_courses:{course.Name}:TeacherImage"] ?? teacherPhotoUrl;
                    if (!teacherImage.StartsWith("https:")) teacherImage = $"https:{teacherImage}";
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
            string announceMessage = ClassroomUtils.GetAnnounceMessageWithRolesMention(course, guild, _config, updated ? "UpdatedAnnouncement" : "NewAnnouncement");

            // Announce new course announcement.
            await ClassroomUtils.AnnounceNews(guild, embed, announceMessage, _channelService, _logger).ConfigureAwait(false);

            // Link all announcement materials.
            //await LinkMaterialsNew(newAnnouncement.Materials, guild.GetTextChannel(397542059596316673)).ConfigureAwait(false);
        }

        private async Task GetDataFromClassroomTask(ClassroomService service)
        {
            // Define request parameters.
            var coursesRequest = service.Courses.List();
            coursesRequest.PageSize = 20;

            // Prepare batch request.
            var batchCourseInfoRequest = new BatchRequest(service);

            try
            {
                // List courses.
                ListCoursesResponse response = await coursesRequest.ExecuteAsync(CancellationToken.Token).ConfigureAwait(false);

                if (response.Courses != null && response.Courses.Count > 0)
                {
                    foreach (var course in response.Courses)
                    {
                        // Add course to dictionaries.
                        if (!CourseWorks.ContainsKey(course.Name))
                            CourseWorks.Add(course.Name, new KeyValuePair<Course, List<CourseWork>>(course, new List<CourseWork>()));
                        if (!Announcements.ContainsKey(course.Name))
                            Announcements.Add(course.Name, new KeyValuePair<Course, List<Announcement>>(course, new List<Announcement>()));
                        if (!Courses.Contains(course, new CourseEqualityComparer()))
                            Courses.Add(course);

                        // Add course work request for this course to batch request.
                        batchCourseInfoRequest.Queue<ListCourseWorkResponse>(
                            service.Courses.CourseWork.List(course.Id),
                            async (courseWorkResponse, error, i, message) =>
                            {
                                // Make sure there are no errors.
                                if (error != null)
                                {
                                    await _logger.LogAsync(LogSeverity.Error, nameof(ClassroomUtils), error.Message).ConfigureAwait(false);
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
                                        $"[{course.Name}] Latest work: \"{latestCourseWork}\"").ConfigureAwait(false);
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
                                    await _logger.LogAsync(LogSeverity.Error, nameof(ClassroomUtils), error.Message).ConfigureAwait(false);
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
                                        $"[{course.Name}] Latest announcement: \"{latestAnnouncement}\"").ConfigureAwait(false);
                                }
                            });
                    }
                    // Execute batch request.
                    await batchCourseInfoRequest.ExecuteAsync(CancellationToken.Token).ConfigureAwait(false);
                }
                else
                {
                    await _logger.LogAsync(LogSeverity.Error, nameof(JaneClassroomService), "No courses found.").ConfigureAwait(false);
                }
            }
            catch (TaskCanceledException e)
            {
                await _logger.LogAsync(LogSeverity.Info, nameof(JaneClassroomService), e.Message).ConfigureAwait(false);
            }
        }

    }
}
