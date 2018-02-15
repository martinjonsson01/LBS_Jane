using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using DiscordBot_Jane.Core.Services;
using Google.Apis.Classroom.v1;
using Google.Apis.Classroom.v1.Data;
using Google.Apis.Requests;
using Microsoft.Extensions.Configuration;

namespace DiscordBot_Jane.Core.Utils
{
    public static class ClassroomUtils
    {
        /// <summary>
        /// Gets a list of teachers for a course.
        /// </summary>
        /// <param name="service">The classroom service used to connect to classroom.</param>
        /// <param name="courseId">The id of the course to get teachers for.</param>
        /// <returns></returns>
        public static async Task<IList<Teacher>> GetTeachers(ClassroomService service, string courseId)
        {
            // Define request parameters.
            var teachersRequest = service.Courses.Teachers.List(courseId);

            // Execute request.
            ListTeachersResponse response = await teachersRequest.ExecuteAsync();

            return response.Teachers;
        }

        /// <summary>
        /// Gets lists of teachers for each course in the specified course list.
        /// </summary>
        /// <param name="service">The classroom service used to connect to classroom.</param>
        /// <param name="courses">The list of courses to get lists of teachers for.</param>
        /// <param name="callback">A callback that will be called for every course and list of course teachers.</param>
        public static async Task GetTeachersForCourses(ClassroomService service,
            IEnumerable<Course> courses, Action<Course, ListTeachersResponse, RequestError> callback)
        {
            // Instantiate batch request.
            BatchRequest batchRequest = new BatchRequest(service);

            foreach (var course in courses)
            {
                // Queue request for course and pass in callback.
                GetTeachers(service, course.Id, batchRequest, (content, error, index, message) =>
                {
                    callback?.Invoke(course, content, error);
                });
            }

            // Execute request.
            await batchRequest.ExecuteAsync();
        }

        /// <summary>
        /// Get teacher who created this <see cref="CourseWork"/>.
        /// </summary>
        /// <param name="courseWork">The <see cref="CourseWork"/> to get the teacher from.</param>
        /// <param name="course">The <see cref="Course"/> to get the teacher from.</param>
        /// <param name="courseTeachers">The dictionary of courses and teachers to pull data from.</param>
        /// <returns>The teacher who created this <see cref="CourseWork"/>.</returns>
        public static Teacher GetTeacher(this CourseWork courseWork, Course course, Dictionary<string, IList<Teacher>> courseTeachers)
        {
            if (courseTeachers.ContainsKey(course.Name))
            {
                foreach (var teacher in courseTeachers[course.Name])
                {
                    if (teacher.UserId == courseWork.CreatorUserId)
                        return teacher;
                }
                return courseTeachers[course.Name][0];
            }
            return null;
        }

        /// <summary>
        /// Get teacher who created this <see cref="Announcement"/>.
        /// </summary>
        /// <param name="announcement">The <see cref="Announcement"/> to get the teacher from.</param>
        /// <param name="course">The <see cref="Course"/> to get the teacher from.</param>
        /// <param name="courseTeachers">The dictionary of courses and teachers to pull data from.</param>
        /// <returns>The teacher who created this <see cref="Announcement"/>.</returns>
        public static Teacher GetTeacher(this Announcement announcement, Course course, Dictionary<string, IList<Teacher>> courseTeachers)
        {
            if (courseTeachers.ContainsKey(course.Name))
            {
                foreach (var teacher in courseTeachers[course.Name])
                {
                    if (teacher.UserId == announcement.CreatorUserId)
                        return teacher;
                }
                return courseTeachers[course.Name][0];
            }
            return null;
        }

        private static void GetTeachers(ClassroomService service, string courseId,
            BatchRequest batchRequest, BatchRequest.OnResponse<ListTeachersResponse> callback)
        {
            // Define request parameters.
            var teachersRequest = service.Courses.Teachers.List(courseId);

            // Queue request.
            batchRequest.Queue(teachersRequest, callback);
        }

        public static async Task AnnounceNews(SocketGuild guild, Embed embed, string announceMessage, ChannelService channelService, LoggingService logger)
        {
            if (channelService.NewsChannels.TryGetValue(guild.Id, out var channel))
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
                    await logger.LogAsync(LogSeverity.Error, nameof(JaneClassroomService), $"News channel is null for guild {guild.Name}");
                }
            }
            else if (channelService.NewsChannelsRest.TryGetValue(guild.Id, out var restChannel))
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
                    await logger.LogAsync(LogSeverity.Error, nameof(JaneClassroomService), $"Rest news channel is null for guild {guild.Name}");
                }
            }
            else
            {
                await logger.LogAsync(LogSeverity.Error, nameof(JaneClassroomService), $"No news channel assigned for guild {guild.Name}");
            }
        }

        public static string GetAnnounceMessageWithRolesMention(Course course, SocketGuild guild, IConfigurationRoot config, string configMessage = "NewAnnouncement")
        {
            var announceMessage = $"{config[$"messages:{configMessage}"]} från {course.Name}";
            foreach (var role in ConfigUtils.CourseMentionRoles(course.Name, guild, config))
            {
                if (role.Id == guild.Id)
                    announceMessage += " @everyone";
                else
                    announceMessage += $" {role.Mention}";
            }
            return announceMessage;
        }

        public static Course GetCourse(string id, IEnumerable<Course> courses)
        {
            foreach (var course in courses)
            {
                if (course.Id == id)
                    return course;
            }
            return null;
        }

        /// <summary>
        /// Turns a <see cref="Date"/> object and a <see cref="TimeOfDay"/> object into a <see cref="DateTimeOffset"/> object.
        /// </summary>
        /// <param name="date">The date.</param>
        /// <param name="time">The time of day.</param>
        /// <returns>A <see cref="DateTimeOffset"/> consisting of the provided <see cref="Date"/> and <see cref="TimeOfDay"/>.</returns>
        public static DateTimeOffset GetDateTimeOffsetFromDateAndTimeOfDay(Date date, TimeOfDay time)
        {
            // Generate a DateTimeOffset based on the date and timeofday.
            return new DateTimeOffset(GetDateTimeFromDateAndTimeOfDay(date, time));
        }

        public static DateTime GetDateTimeFromDateAndTimeOfDay(Date date, TimeOfDay time)
        {
            var now = DateTime.Now;
            // Generate a DateTime based on the date and timeofday.
            var year = date?.Year ?? now.Year;
            var month = date?.Month ?? now.Month;
            var day = date?.Day ?? now.Day;
            var hours = time?.Hours ?? now.Hour;
            var minutes = time?.Minutes ?? now.Minute;
            var seconds = time?.Seconds ?? now.Second;
            return new DateTime(year, month, day, hours, minutes, seconds);
        }

        /// <summary>
        /// Adds every <see cref="Material"/> in <see cref="materials"/> to the provided <see cref="EmbedBuilder"/>.
        /// </summary>
        /// <param name="materials">The list of <see cref="Material"/>s to add to the <see cref="builder"/>.</param>
        /// <param name="builder">The <see cref="EmbedBuilder"/> to add each <see cref="Material"/> to.</param>
        public static void AddMaterialsAsField(IEnumerable<Material> materials, EmbedBuilder builder)
        {
            var materialsString = "";
            foreach (var material in materials)
            {
                var materialInfo = GetMaterialInfo(material);
                materialsString += $"[{materialInfo.materialTitle}]({materialInfo.materialLink})\n";
            }
            builder.AddField("Material:", materialsString);
        }

        /// <summary>
        /// Sends a new embed message for each <see cref="Material"/> in the provided list of <see cref="materials"/>.
        /// </summary>
        /// <param name="materials">The list of <see cref="Material"/>s to send messages for.</param>
        /// <param name="channel">The channel in which to send the embed messages for each <see cref="Material"/>.</param>
        /// <returns></returns>
        public static async Task LinkMaterialsSeperately(IList<Material> materials, SocketTextChannel channel)
        {
            if (materials != null)
            {
                await channel.SendMessageAsync("**Material:**");
                foreach (var material in materials)
                {
                    var materialInfo = ClassroomUtils.GetMaterialInfo(material);

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

        /// <summary>
        /// Gets information from a <see cref="Material"/> object and returns them in string format inside of a <see cref="Tuple"/>.
        /// </summary>
        /// <param name="material">The <see cref="Material"/> to get information from.</param>
        /// <returns>A <see cref="Tuple{string, string, string}"/> containing various information about the material.</returns>
        public static (string materialTitle, string materialLink, string materialThumbnail) GetMaterialInfo(Material material)
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
