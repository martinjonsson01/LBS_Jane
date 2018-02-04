using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot_Jane
{
    public static class Config
    {
        public static readonly string DiscordToken = "NDA5Njk4NjIwNTk0NDU0NTI4.DViZKw._a-X4VeC_4bC2RU4H238csPo51c";
        public static readonly string Trigger = "Jane ";
        public static readonly string ClassroomNoCourseWork = "Inga uppgifter i kursen.";
        public static readonly string ClassroomNoCourseAnnouncements = "Inga nyheter i kursen."; 

        public static readonly int BanPruneDays = 7;
        public static readonly double ClassroomCallIntervalMinutes = 1.0;
    }
}
