using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Apis.Classroom.v1.Data;

namespace DiscordBot_Jane.Core.Equality_Comparers
{
    public class CourseEqualityComparer : IEqualityComparer<Course>
    {
        public bool Equals(Course x, Course y)
        {
            if (x == null && y != null)
                return false;
            if (x != null && y == null)
                return false;
            if (x == null && y == null)
                return true;
            if (x.Name != y.Name)
                return false;
            if (x.Description != y.Description)
                return false;
            if (x.EnrollmentCode != y.EnrollmentCode)
                return false;
            if (x.AlternateLink != y.AlternateLink)
                return false;
            if (x.CourseGroupEmail != y.CourseGroupEmail)
                return false;
            if (x.TeacherGroupEmail != y.TeacherGroupEmail)
                return false;

            return true;
        }

        public int GetHashCode(Course obj)
        {
            unchecked // Overflow is fine, just wrap
            {
                int hash = 31;
                hash = hash * 71 + (obj.Id?.GetHashCode() ?? 0);
                hash = hash * 71 + (obj.Name?.GetHashCode() ?? 0);
                hash = hash * 71 + (obj.Description?.GetHashCode() ?? 0);
                hash = hash * 71 + (obj.EnrollmentCode?.GetHashCode() ?? 0);
                hash = hash * 71 + (obj.AlternateLink?.GetHashCode() ?? 0);
                hash = hash * 71 + (obj.CourseGroupEmail?.GetHashCode() ?? 0);
                hash = hash * 71 + (obj.TeacherGroupEmail?.GetHashCode() ?? 0);
                return hash;
            }
        }
    }
}
