using System.Collections.Generic;
using System.Linq;
using Google.Apis.Classroom.v1.Data;

namespace DiscordBot_Jane.Core.Equality_Comparers
{
    public class CourseWorkEqualityComparer : IEqualityComparer<CourseWork>
    {
        public bool Equals(CourseWork x, CourseWork y)
        {
            if (x == null && y != null)
                return false;
            if (x != null && y == null)
                return false;
            if (x == null && y == null)
                return true;
            if (x.CourseId != y.CourseId)
                return false;
            if (x.Title != y.Title)
                return false;
            if (x.Description != y.Description)
                return false;
            if (x.CreatorUserId != y.CreatorUserId)
                return false;
            if (x.AlternateLink != y.AlternateLink)
                return false;

            return true;
        }

        public int GetHashCode(CourseWork obj)
        {
            unchecked // Overflow is fine, just wrap
            {
                int hash = 17;
                hash = hash * 23 + (obj.CourseId?.GetHashCode() ?? 0);
                hash = hash * 23 + (obj.Title?.GetHashCode() ?? 0);
                hash = hash * 23 + (obj.Description?.GetHashCode() ?? 0);
                hash = hash * 23 + (obj.CreatorUserId?.GetHashCode() ?? 0);
                hash = hash * 23 + (obj.AlternateLink?.GetHashCode() ?? 0);
                return hash;
            }
        }
    }

    public class CourseWorkDictionaryEqualityComparer : IEqualityComparer<KeyValuePair<string, KeyValuePair<Course, List<CourseWork>>>>
    {
        public bool Equals(KeyValuePair<string, KeyValuePair<Course, List<CourseWork>>> x, KeyValuePair<string, KeyValuePair<Course, List<CourseWork>>> y)
        {
            if (!x.Key.Equals(y.Key))
                return false;
            if (!x.Value.Key.Id.Equals(y.Value.Key.Id))
                return false;
            if (!x.Value.Value.SequenceEqual(y.Value.Value, new CourseWorkEqualityComparer()))
                return false;

            return true;
        }

        public int GetHashCode(KeyValuePair<string, KeyValuePair<Course, List<CourseWork>>> obj)
        {
            unchecked // Overflow is fine, just wrap
            {
                int hash = 17;
                hash = hash * 29 + obj.Key.GetHashCode();
                hash = hash * 29 + obj.Value.Key.Id.GetHashCode();
                foreach (var courseWork in obj.Value.Value)
                {
                    hash = hash * 23 + (courseWork.CourseId?.GetHashCode() ?? 0);
                    hash = hash * 23 + (courseWork.Title?.GetHashCode() ?? 0);
                    hash = hash * 23 + (courseWork.Description?.GetHashCode() ?? 0);
                    hash = hash * 23 + (courseWork.CreatorUserId?.GetHashCode() ?? 0);
                    hash = hash * 23 + (courseWork.AlternateLink?.GetHashCode() ?? 0);
                }
                return hash;
            }
        }
    }
}
