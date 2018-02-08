using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Google.Apis.Classroom.v1;
using Google.Apis.Classroom.v1.Data;
using Google.Apis.Requests;

namespace DiscordBot_Jane.Utils
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
    }
}
