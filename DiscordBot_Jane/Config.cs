using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;

namespace DiscordBot_Jane
{
    public static class Config
    {
        public static List<SocketRole> CourseMentionRoles(string courseName, SocketGuild guild, IConfigurationRoot config)
        {
            var mentionRoles = new List<SocketRole>();
            var configCourseMentions = config.GetValue<IEnumerable<string>>($"classroom_courses:{courseName}:RoleMentions", new []{"everyone"});
            var courseMentions = configCourseMentions as IList<string> ?? configCourseMentions.ToList();
            foreach (var role in guild.Roles)
            {
                foreach (var mentionRole in courseMentions)
                {
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
    }
}
