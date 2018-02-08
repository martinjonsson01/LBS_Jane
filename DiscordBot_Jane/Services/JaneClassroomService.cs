using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot_Jane.Utils;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Classroom.v1;
using Google.Apis.Classroom.v1.Data;
using Google.Apis.Requests;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Google.Protobuf.WellKnownTypes;
using Color = Discord.Color;

namespace DiscordBot_Jane.Services
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
            ChannelService channelService)
        {
            _discord = discord;
            _commands = commands;
            _logger = logger;
            _provider = provider;
            _channelService = channelService;
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

                    // TODO: Remove this, this is for testing.
                    oldAnnouncements[oldAnnouncements.Keys.ElementAt(3)].Value.RemoveAt(2);

                    // No need to get data from classroom if this is the first  
                    // loop since it was already called before entering the loop.
                    if (firstLoop)
                        firstLoop = false;
                    else
                        await GetDataFromClassroomTask(service);
                    
                    if (oldCourseWorks.Keys.Count > 0)
                    {
                        foreach (var pair in CourseWorks.Except(oldCourseWorks,
                            new CourseWorkDictionaryEqualityComparer()))
                        {
                            await _logger.LogAsync(LogSeverity.Debug, nameof(ClassroomService), $"CourseWork change detected in course {pair.Key}");
                            foreach (var newCourseWork in pair.Value.Value.Except(oldCourseWorks[pair.Key].Value, new CourseWorkEqualityComparer()))
                            {
                                // Handle in every guild.
                                foreach (var guild in _discord.Guilds)
                                {
                                    await HandleNewCourseWork(pair.Value.Key, newCourseWork, guild);
                                }
                            }
                        }
                    }
                    if (oldAnnouncements.Keys.Count > 0)
                    {
                        foreach (var pair in Announcements.Except(oldAnnouncements,
                            new CourseAnnouncementDictionaryEqualityComparer()))
                        {
                            await _logger.LogAsync(LogSeverity.Debug, nameof(ClassroomService), $"Announcement change detected in course {pair.Key}");
                            foreach (var newAnnouncement in pair.Value.Value.Except(oldAnnouncements[pair.Key].Value, new CourseAnnouncementEqualityComparer()))
                            {
                                // Handle in every guild.
                                foreach (var guild in _discord.Guilds)
                                {
                                    await HandleNewCourseAnnouncement(pair.Value.Key, newAnnouncement, guild);
                                }
                            }
                        }
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
        }

        private async Task HandleNewCourseWork(Course course, CourseWork newCourseWork, SocketGuild guild)
        {
            await _logger.LogAsync(LogSeverity.Debug, nameof(ClassroomService),
                $"Announcing new course work for {course.Name}: {newCourseWork.Title.Substring(0, Math.Min(25, newCourseWork.Title.Length))} in guild {guild.Name}");
            DateTimeOffset dateTimeOffset = GetDateTimeOffsetFromDateAndTimeOfDay(newCourseWork.DueDate, newCourseWork.DueTime);

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
                        .WithIconUrl(Config.IconWhiteClock);
                })
                .WithThumbnailUrl(Config.IconWhiteTask)
                .WithAuthor(author =>
                {
                    author
                        .WithName($"{course.Name} - {course.Section}")
                        .WithUrl(course.AlternateLink)
                        .WithIconUrl(
                            Config.CourseTeacherImageDictionary.ContainsKey(course.Name)
                            ? Config.CourseTeacherImageDictionary[course.Name] // If it contains key.
                            : $"https:{teacherPhotoUrl}"); // If it doesn't.
                });

            // Add materials as fields.
            if (newCourseWork.Materials != null)
                AddMaterialsAsField(newCourseWork.Materials, builder);

            // Build embed.
            var embed = builder.Build();

            // Get mentions of all roles that should be mentioned.
            string announceMessage = GetAnnounceMessageWithRolesMention(course);

            // Announce new course work.
            await AnnounceNews(guild, embed, announceMessage);

            // Link all course work materials.
            //await LinkMaterialsNew(newCourseWork.Materials, guild.GetTextChannel(397542059596316673));
        }

        private async Task AnnounceNews(SocketGuild guild, Embed embed, string announceMessage)
        {
            if (_channelService.NewsChannels.TryGetValue(guild.Id, out var channel))
            {
                if (channel != null)
                {
                    // Announce new course work along with embed.
                    await channel.SendMessageAsync(
                        announceMessage,
                        embed: embed);
                }
                else
                {
                    await _logger.LogAsync(LogSeverity.Error, nameof(ClassroomService), $"News channel is null for guild {guild.Name}");
                }
            }
            else if (_channelService.NewsChannelsRest.TryGetValue(guild.Id, out var restChannel))
            {
                if (restChannel != null)
                {
                    // Announce new course work along with embed.
                    await restChannel.SendMessageAsync(
                        announceMessage,
                        embed: embed);
                }
                else
                {
                    await _logger.LogAsync(LogSeverity.Error, nameof(ClassroomService), $"Rest news channel is null for guild {guild.Name}");
                }
            }
            else
            {
                await _logger.LogAsync(LogSeverity.Error, nameof(ClassroomService), $"No news channel assigned for guild {guild.Name}");
            }
        }

        private async Task HandleNewCourseAnnouncement(Course course, Announcement newAnnouncement, SocketGuild guild)
        {
            await _logger.LogAsync(LogSeverity.Debug, nameof(ClassroomService),
                $"New announcement for {course.Name}: {newAnnouncement.Text.Substring(0, Math.Min(25, newAnnouncement.Text.Length))}");

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
                        .WithIconUrl(Config.IconWhiteClock);
                })
                .WithThumbnailUrl(Config.IconWhiteAnnouncement)
                .WithAuthor(author =>
                {
                    author
                        .WithName($"{course.Name} - {course.Section}")
                        .WithUrl(course.AlternateLink)
                        .WithIconUrl(
                            Config.CourseTeacherImageDictionary.ContainsKey(course.Name)
                            ? Config.CourseTeacherImageDictionary[course.Name] // If it contains key.
                            : $"https:{teacherPhotoUrl}"); // If it doesn't.
                });

            // Add materials as fields.
            if (newAnnouncement.Materials != null)
                AddMaterialsAsField(newAnnouncement.Materials, builder);

            // Build embed.
            var embed = builder.Build();

            // Get mentions of all roles that should be mentioned.
            string announceMessage = GetAnnounceMessageWithRolesMention(course);

            // Announce new course announcement.
            await AnnounceNews(guild, embed, announceMessage);

            // Link all announcement materials.
            //await LinkMaterialsNew(newAnnouncement.Materials, guild.GetTextChannel(397542059596316673));
        }

        private string GetAnnounceMessageWithRolesMention(Course course)
        {
            var announceMessage = $"{Config.NewAnnouncementMessage} från {course.Name}";
            foreach (var role in Config.CourseMentionRoles(course.Name, _discord.GetGuild(Config.GuildId)))
            {
                if (role.Id == _discord.GetGuild(Config.GuildId).Id)
                    announceMessage += " @everyone";
                else
                    announceMessage += $" {role.Mention}";
            }
            return announceMessage;
        }

        private async Task LinkMaterialsNew(IList<Material> materials, SocketTextChannel channel)
        {
            if (materials != null)
            {
                await channel.SendMessageAsync("**Material:**");
                foreach (var material in materials)
                {
                    var materialInfo = GetMaterialInfo(material);

                    var materialBuilder = new EmbedBuilder()
                        .WithTitle(materialInfo.materialTitle)
                        .WithUrl(materialInfo.materialLink)
                        .WithColor(new Color(0x386CC2))
                        .WithImageUrl(materialInfo.materialThumbnail);
                    var materialEmbed = materialBuilder.Build();

                    await channel.SendMessageAsync(
                        "",
                        embed: materialEmbed);
                }
            }
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

            await _logger.LogAsync(LogSeverity.Debug, nameof(ClassroomService), "Courses:");
            if (response.Courses != null && response.Courses.Count > 0)
            {
                foreach (var course in response.Courses)
                {
                    await _logger.LogAsync(LogSeverity.Debug, nameof(ClassroomService),
                        $"{course.Name} ({course.Id})");

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
                                var firstCourseWorkTitle = Config.ClassroomNoCourseWork;
                                if (courseWorkResponse.CourseWork?[0] != null)
                                    firstCourseWorkTitle = courseWorkResponse.CourseWork[0].Title;

                                await _logger.LogAsync(LogSeverity.Info, nameof(ClassroomService),
                                    $"[{course.Name}] Latest work: {firstCourseWorkTitle}");
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
                                var firstCourseWorkTitle = Config.ClassroomNoCourseAnnouncements;
                                if (courseAnnouncementsResponse.Announcements?[0] != null)
                                    firstCourseWorkTitle =
                                        courseAnnouncementsResponse.Announcements[0].Text;

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
                credPath = Path.Combine(credPath,
                    ".credentials/classroom.googleapis.com-dotnet-discord-bot-jane-lbs.json");

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

        private static DateTimeOffset GetDateTimeOffsetFromDateAndTimeOfDay(Date date, TimeOfDay time)
        {
            // Generate a DateTimeOffset based on the due date of the course work.
            var year = date?.Year ?? DateTime.Now.Year;
            var month = date?.Month ?? DateTime.Now.Month;
            var day = date?.Day ?? DateTime.Now.Day;
            var hours = time?.Hours ?? DateTime.Now.Hour;
            var minutes = time?.Minutes ?? DateTime.Now.Minute;
            var seconds = time?.Seconds ?? DateTime.Now.Second;
            return new DateTimeOffset(new DateTime(year, month, day, hours, minutes, seconds));
        }

        private static void AddMaterialsAsField(IEnumerable<Material> materials, EmbedBuilder builder)
        {
            foreach (var material in materials)
            {
                var materialInfo = GetMaterialInfo(material);
                builder.AddField("Material:",
                    $"[{materialInfo.materialTitle}]({materialInfo.materialLink})");
            }
        }

        private static (string materialTitle, string materialLink, string materialThumbnail) GetMaterialInfo(Material material)
        {
            // Get different material title depending on material type.
            var materialTitle = "null";
            var materialLink = "null";
            var materialThumbnail = "null";
            if (material.DriveFile?.DriveFile != null)
            {
                materialTitle = material.DriveFile?.DriveFile.Title;
                materialLink = material.DriveFile?.DriveFile.AlternateLink;
                materialThumbnail = material.DriveFile?.DriveFile?.ThumbnailUrl;
            }
            else if (material.Form != null)
            {
                materialTitle = material.Form.Title;
                materialLink = material.Form?.FormUrl;
                materialThumbnail = material.Form?.ThumbnailUrl;
            }
            else if (material.YoutubeVideo != null)
            {
                materialTitle = material.YoutubeVideo.Title;
                materialLink = material.YoutubeVideo?.AlternateLink;
                materialThumbnail = material.YoutubeVideo?.ThumbnailUrl;
            }
            else if (material.Link != null)
            {
                materialTitle = material.Link.Title;
                materialLink = material.Link?.Url;
                materialThumbnail = material.Link?.ThumbnailUrl;
            }
            return (materialTitle, materialLink, materialThumbnail);
        }

    }
}
