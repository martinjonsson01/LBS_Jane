using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot_Jane.Core.Models;
using DiscordBot_Jane.Core.Utils;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Classroom.v1.Data;
using Microsoft.Extensions.Configuration;

namespace DiscordBot_Jane.Core.Services
{
    public class ReminderService
    {
        private readonly DiscordSocketClient _discord;
        private readonly GoogleAuthenticateService _gAuth;
        private readonly LoggingService _logger;
        private readonly ChannelService _channelService;
        private readonly IConfigurationRoot _config;
        private readonly JaneClassroomService _classroom;

        public readonly CancellationTokenSource CancellationToken = new CancellationTokenSource();

        public ReminderService(
            DiscordSocketClient discord,
            GoogleAuthenticateService gAuth,
            LoggingService logger,
            ChannelService channelService,
            IConfigurationRoot config,
            JaneClassroomService classroom
            )
        {
            _discord = discord;
            _gAuth = gAuth;
            _logger = logger;
            _channelService = channelService;
            _config = config;
            _classroom = classroom;

            _gAuth.GoogleAuthenticated += StartCheckRemindersTask;
        }
        
        private async void StartCheckRemindersTask(object sender, UserCredential credential)
        {
            _gAuth.GoogleAuthenticated -= StartCheckRemindersTask;
            
            /* 
                * This loop does not end until application exits.
                * This is the order of operations:
                * 1. Check if any reminders are due.
                * 2. Sleep for a period of time.
                * 3. Repeat.
                */
            while (!CancellationToken.IsCancellationRequested)
            {
                // Check if any reminders are due.
                await CheckReminders();
                try
                {
                    double interval = _config.GetValue("reminder_interval_minutes", 0.5);
                    await Task.Delay(TimeSpan.FromMinutes(interval), CancellationToken.Token);
                }
                catch (TaskCanceledException e)
                {
                    await _logger.LogAsync(LogSeverity.Info, nameof(ReminderService), e.Message);
                    break;
                }
            }
        }

        private async Task CheckReminders()
        {
            if (_classroom.CourseWorks != null && _classroom.CourseWorks.Count > 0)
            {
                // Course Work reminders.
                foreach (var courseCourseWorks in _classroom.CourseWorks.Values)
                {
                    foreach (var courseWork in courseCourseWorks.Value)
                    {
                        if (CourseWorkDueSoon(courseWork))
                        {
                            var formattedCourseWork =
                                courseWork.Title
                                    .Replace("\n", " ")
                                    .Truncate(_config.GetValue("truncate_latest_messages", 25));
                            var dueHours = _config.GetValue("remind_within_hours", 24);

                            await _logger.LogAsync(LogSeverity.Info, nameof(ReminderService),
                                $"Course work {formattedCourseWork} is due within {dueHours} hours. Announcing it.");

                            foreach (var guild in _discord.Guilds)
                            {
                                // If guild isn't added to the classroom blacklist.
                                if (!ConfigUtils.ContainsValueAt("classroom_guilds_blacklist", guild.Name, _config))
                                {
                                    var workDueAnnouncement = CreateWorkDueAnnouncement(guild, courseWork);
                                    // Announce reminder.
                                    await ClassroomUtils.AnnounceNews(guild, workDueAnnouncement.embed,
                                        workDueAnnouncement.message, _channelService, _logger);
                                }
                            }
                        }
                    }
                }
            }
        }

        private (Embed embed, string message) CreateWorkDueAnnouncement(SocketGuild guild, CourseWork courseWork)
        {
            var course = ClassroomUtils.GetCourse(courseWork.CourseId, _classroom.Courses);

            DateTimeOffset dateTimeOffset = ClassroomUtils.GetDateTimeOffsetFromDateAndTimeOfDay(courseWork.DueDate, courseWork.DueTime);

            // Find the teacher who created this announcement.
            var teacherPhotoUrl = courseWork.GetTeacher(course, _classroom.CourseTeachers)?.Profile?.PhotoUrl ?? "https://cdn.discordapp.com/embed/avatars/0.png";

            // Create embed for new course work.
            var builder = new EmbedBuilder()
                .WithTitle(courseWork.Title ?? "Ingen titel")
                .WithDescription(courseWork.Description ?? "Ingen beskrivning")
                .WithUrl(courseWork.AlternateLink)
                .WithColor(new Color(54, 71, 79))
                .WithTimestamp(dateTimeOffset)
                .WithFooter(footer =>
                {
                    footer
                        .WithText("Inlämning")
                        .WithIconUrl(_config["urls:IconWhiteAnnouncement"]);
                })
                .WithThumbnailUrl(_config["urls:IconWhiteClock"])
                .WithAuthor(author =>
                {
                    var teacherImage = _config[$"classroom_courses:{course.Name}:TeacherImage"] ?? $"https:{teacherPhotoUrl}";
                    author
                        .WithName($"{course.Name} - {course.Section}")
                        .WithUrl(course.AlternateLink)
                        .WithIconUrl(teacherImage);
                });

            // Add materials as fields.
            if (courseWork.Materials != null)
                ClassroomUtils.AddMaterialsAsField(courseWork.Materials, builder);

            // Build embed.
            var embed = builder.Build();

            // Get mentions of all roles that should be mentioned.
            string announceMessage = ClassroomUtils.GetAnnounceMessageWithRolesMention(course, guild, _config, "NewReminder");

            return (embed, announceMessage);
        }

        private bool CourseWorkDueSoon(CourseWork courseWork)
        {
            if (courseWork.DueDate == null || courseWork.DueTime == null) return false;

            var dueDate = ClassroomUtils.GetDateTimeFromDateAndTimeOfDay(courseWork.DueDate, courseWork.DueTime);
            var now = DateTime.Now;
            var withinHours = _config.GetValue("remind_within_hours", 24);
            var range = new DateRange(dueDate.AddHours(-withinHours), dueDate);
            return range.Includes(now);
        }
    }
}
