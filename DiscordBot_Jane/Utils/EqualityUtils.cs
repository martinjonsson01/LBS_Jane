using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Apis.Classroom.v1.Data;

namespace DiscordBot_Jane.Utils
{
    public static class EqualityUtils
    {
        public static bool SameCourseWorkAs(this CourseWork thisCourseWork, CourseWork otherCourseWork)
        {
            if (thisCourseWork.CourseId != null && !thisCourseWork.CourseId.Equals(otherCourseWork.CourseId))
                return false;
            if (thisCourseWork.Title != null && !thisCourseWork.Title.Equals(otherCourseWork.Title))
                return false;
            if (thisCourseWork.Description != null && !thisCourseWork.Description.Equals(otherCourseWork.Description))
                return false;
            if (thisCourseWork.CreatorUserId != null && !thisCourseWork.CreatorUserId.Equals(otherCourseWork.CreatorUserId))
                return false;
            if (thisCourseWork.AlternateLink != null && !thisCourseWork.AlternateLink.Equals(otherCourseWork.AlternateLink))
                return false;

            return true;
        }

        public static bool SameAnnouncementAs(this Announcement thisAnnouncement, Announcement otherAnnouncement)
        {
            if (thisAnnouncement.CourseId != null && !thisAnnouncement.CourseId.Equals(otherAnnouncement.CourseId))
                return false;
            if (thisAnnouncement.Text != null && !thisAnnouncement.Text.Equals(otherAnnouncement.Text))
                return false;
            if (thisAnnouncement.CreatorUserId != null && !thisAnnouncement.CreatorUserId.Equals(otherAnnouncement.CreatorUserId))
                return false;
            if (thisAnnouncement.AlternateLink != null && !thisAnnouncement.AlternateLink.Equals(otherAnnouncement.AlternateLink))
                return false;

            return true;
        }
    }
}
