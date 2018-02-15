using System.Collections.Generic;
using System.Linq;
using Google.Apis.Classroom.v1.Data;

namespace DiscordBot_Jane.Core.Equality_Comparers
{
    public class CourseAnnouncementEqualityComparer : IEqualityComparer<Announcement>
    {
        public bool Equals(Announcement x, Announcement y)
        {
            if (x == null && y != null)
                return false;
            if (x != null && y == null)
                return false;
            if (x == null && y == null)
                return true;
            if (x.CourseId != y.CourseId)
                return false;
            if (x.Text != y.Text)
                return false;
            if (x.CreatorUserId != y.CreatorUserId)
                return false;
            if (x.AlternateLink != y.AlternateLink)
                return false;

            return true;
        }

        public int GetHashCode(Announcement obj)
        {
            unchecked // Overflow is fine, just wrap
            {
                int hash = 17;
                hash = hash * 23 + (obj.CourseId?.GetHashCode() ?? 0);
                hash = hash * 23 + (obj.Text?.GetHashCode() ?? 0);
                hash = hash * 23 + (obj.CreatorUserId?.GetHashCode() ?? 0);
                hash = hash * 23 + (obj.AlternateLink?.GetHashCode() ?? 0);
                return hash;
            }
        }
    }

    public class CourseAnnouncementDictionaryEqualityComparer : IEqualityComparer<KeyValuePair<string, KeyValuePair<Course, List<Announcement>>>>
    {
        public bool Equals(KeyValuePair<string, KeyValuePair<Course, List<Announcement>>> x, KeyValuePair<string, KeyValuePair<Course, List<Announcement>>> y)
        {
            if (!x.Key.Equals(y.Key))
                return false;
            if (!x.Value.Key.Id.Equals(y.Value.Key.Id))
                return false;
            if (!x.Value.Value.SequenceEqual(y.Value.Value, new CourseAnnouncementEqualityComparer()))
                return false;

            return true;
        }

        public int GetHashCode(KeyValuePair<string, KeyValuePair<Course, List<Announcement>>> obj)
        {
            unchecked // Overflow is fine, just wrap
            {
                int hash = 17;
                hash = hash * 29 + obj.Key.GetHashCode();
                hash = hash * 29 + obj.Value.Key.Id.GetHashCode();
                foreach (var announcement in obj.Value.Value)
                {
                    hash = hash * 11 + (announcement.CourseId?.GetHashCode() ?? 0);
                    hash = hash * 11 + (announcement.Text?.GetHashCode() ?? 0);
                    hash = hash * 11 + (announcement.CreatorUserId?.GetHashCode() ?? 0);
                    hash = hash * 11 + (announcement.AlternateLink?.GetHashCode() ?? 0);
                }
                return hash;
            }
        }
    }
}
