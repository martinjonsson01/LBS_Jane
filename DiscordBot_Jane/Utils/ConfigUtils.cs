using System.Collections.Generic;
using System.Linq;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;

namespace DiscordBot_Jane.Core.Utils
{
    public static class ConfigUtils
    {
        public static List<SocketRole> CourseMentionRoles(string courseName, SocketGuild guild, IConfigurationRoot config)
        {
            var mentionRoles = new List<SocketRole>();
            var configCourseMentions = $"classroom_courses:{courseName}:RoleMentions:";
            foreach (var role in guild.Roles)
            {
                for (var i = 0; config[configCourseMentions + i] != null; i++)
                {
                    var mentionRole = config.GetValue(configCourseMentions + i, "everyone");
                    if (mentionRole == "everyone")
                    {
                        mentionRoles.Add(guild.EveryoneRole);
                        return mentionRoles;
                    }
                    if (role.Name == mentionRole)
                        mentionRoles.Add(role);
                }
            }
            return mentionRoles;
        }

        public static bool CourseIsBlackListed(string courseName, IConfigurationRoot config)
        {
            var configKey = "course_blacklist:";
            for (var i = 0; config[configKey + i] != null; i++)
            {
                var blackListedCourseName = config.GetValue(configKey + i, "null");
                if (blackListedCourseName == courseName)
                    return true;
            }
            return false;
        }

        public static bool ContainsValueAt(string configPath, string value, IConfigurationRoot config)
        {
            if (!configPath.EndsWith(":"))
                configPath = configPath + ":";
            for (var i = 0; config[configPath + i] != null; i++)
            {
                var blackListedCourseName = config.GetValue(configPath + i, "null");
                if (blackListedCourseName == value)
                    return true;
            }
            return false;
        }

        public static IEnumerable<string> GetListFrom(string configPath, IConfigurationRoot config)
        {
            var list = new List<string>();

            if (!configPath.EndsWith(":"))
                configPath = configPath + ":";

            for (var i = 0; config[configPath + i] != null; i++)
            {
                list.Add(config[configPath + i]);
            }

            return list;
        }
    }
}
