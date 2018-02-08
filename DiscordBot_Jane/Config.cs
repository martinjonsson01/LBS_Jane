using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;

namespace DiscordBot_Jane
{
    public static class Config
    {
        public const string DiscordToken = "NDA5Njk4NjIwNTk0NDU0NTI4.DViZKw._a-X4VeC_4bC2RU4H238csPo51c";
        public const string Trigger = "Jane ";
        public const string ClassroomNoCourseWork = "Inga uppgifter i kursen.";
        public const string ClassroomNoCourseAnnouncements = "Inga nyheter i kursen.";
        public const string NewCourseWorkMessage = "Ny uppgift, @everyone";
        public static readonly string NewAnnouncementMessage = "Nytt meddelande,";
        
        public const string IconWhiteClock = "https://i.imgur.com/hTxeEtB.png";
        public const string IconWhiteTask = "https://i.imgur.com/MMasd8h.png";
        public static readonly string IconWhiteAnnouncement = "https://i.imgur.com/bZaDLaA.png";
        // If this dictionary does not contain a course, the Classroom profile url for teacher who is posting new object will be used.
        public static readonly Dictionary<string, string> CourseTeacherImageDictionary = new Dictionary<string, string>
        {
            {"Ma1c/2c TE17", "https://lh3.googleusercontent.com/bzcv2Y63QyR13nVopCZMOMjoxQsrh26KcgnfRhB4bvg4b0OG4n-ovwKFnMjkqLzmb_Qf3vR_b87zlX0zt-yUomsiiHye8dSkkJ6GfiLvW4Vk0RQkfgkz3AaJgwWacqfhidm_HwNnl0h2JH76lWjuvjIrlp00RCafMDOD_oVgm1EjtAD50dzN2M9VNlUXKg2PJiJyuttGuE2ggT3ATgix22l1nE7s6ydlQnzKe7PMLCrgjWjLmuMnmd2phwmvBzVFUUrg6wQNQS6rzXpUiIu-hWVWtstNk-v0OIVjd91etyin36DhKXij8WASMzk-juRXvMpOf6sp4TtSkuTVyfwox6Jzdq54OSbIh0SHrUrr9gMb3o2Tt56xtFXXLvucJsgOEPyF1080qa4xbMOoFD6JUCxmkLlbP01mTqcrjeUvZouPXlWPqoj9m6URVM-NwrJqvsePwuUnXsbvI0eud2cff3yIVAwoiiymByj9UkOu6zM0SQFTGNWc_oqW-DV0ChvBDSLHBlFDPrU4sJSsD63ExrehPBWkYNxYs0mrH4MYxwCpivIizQIWt-X0PqhxGR_9ahzj_H5bRqbDluVKY1mvddjsdaw7HjtBDTrNjhzeh9_CWpP4HXGA-P_1sXyGEGG61gqRCbfP3PqByV9Nymq0eKxOgS_P98uA=s350-no"},
            {"Svenska 1 17B3", "https://lh6.googleusercontent.com/-PBlWlKsQlxA/AAAAAAAAAAI/AAAAAAAAAEQ/CyXUnFIK0u0/photo.jpg?sz=32"},
            {"Idrott och hälsa 1", "https://www.lbs.se/live-cdn/2018/01/johan-rosenstromer.jpg"},
            {"Elevråd", "https://lh5.googleusercontent.com/-KRvQCTtfWS8/AAAAAAAAAAI/AAAAAAAAAA4/id_BLn7HF4A/photo.jpg?sz=32"}
        };

        public const long GuildId = 397542058811719680;
        public const long SpelRoleId = 410868452547624976;
        public const long SystemRoleId = 410868455944749081;
        public static List<SocketRole> CourseMentionRoles(string courseName, SocketGuild guild)
        {
            var mentionRoles = new List<SocketRole>();
            SocketRole spelRole = null;
            SocketRole systemRole = null;
            foreach (var role in guild.Roles)
            {
                if (role.Id == SpelRoleId)
                    spelRole = role;
                if (role.Id == SystemRoleId)
                    systemRole = role;
            }
            switch (courseName)
            {
                case "Mentorsgrupp":
                    if (systemRole != null)
                        mentionRoles.Add(systemRole);
                    return mentionRoles;
                case "Digitalt Skapande":
                    if (systemRole != null)
                        mentionRoles.Add(systemRole);
                    return mentionRoles;
                case "Gränssnittsdesign":
                    if (systemRole != null)
                        mentionRoles.Add(systemRole);
                    return mentionRoles;
                case "Ma1c/2c TE17":
                    mentionRoles.Add(guild.EveryoneRole);
                    return mentionRoles;
                case "Teknik 1":
                    mentionRoles.Add(guild.EveryoneRole);
                    return mentionRoles;
                case "Samhällskunskap 1b":
                    mentionRoles.Add(guild.EveryoneRole);
                    return mentionRoles;
                case "Engelska 5":
                    mentionRoles.Add(guild.EveryoneRole);
                    return mentionRoles;
                case "Svenska 1 17B3":
                    mentionRoles.Add(guild.EveryoneRole);
                    return mentionRoles;
                case "Idrott och hälsa 1":
                    mentionRoles.Add(guild.EveryoneRole);
                    return mentionRoles;
                case "IT-Support":
                    mentionRoles.Add(guild.EveryoneRole);
                    return mentionRoles;
                case "Elevråd":
                    mentionRoles.Add(guild.EveryoneRole);
                    return mentionRoles;
                default:
                    mentionRoles.Add(guild.EveryoneRole);
                    return mentionRoles;
            }
        }

        public const int BanPruneDays = 7;
        public const double ClassroomCallIntervalMinutes = 1.0;
    }
}
