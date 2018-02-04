using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace DiscordBot_Jane.Modules
{
    [Name("Hjälp")]
    public class HelpModule : ModuleBase<SocketCommandContext>
    {
        private readonly CommandService _service;

        public HelpModule(CommandService service)
        {
            _service = service;
        }

        [Command("help"), Alias("hjälp")]
        public async Task HelpAsync()
        {
            // Delete the "help" command message from the user.
            if (!Context.IsPrivate)
                await Context.Message.DeleteAsync();

            var builder = new EmbedBuilder()
            {
                Color = new Color(114, 137, 218),
                Description = "Detta är vad jag kan göra:"
            };

            foreach (var module in _service.Modules)
            {
                string description = null;
                foreach (var cmd in module.Commands)
                {
                    var result = await cmd.CheckPreconditionsAsync(Context);
                    if (result.IsSuccess)
                        description += $"{Config.Trigger} {cmd.Aliases.First()}\n";
                }

                if (!string.IsNullOrWhiteSpace(description))
                {
                    builder.AddField(x =>
                    {
                        x.Name = module.Name;
                        x.Value = description;
                        x.IsInline = false;
                    });
                }
            }

            //await ReplyAsync("", false, builder.Build());
            await Context.User.SendMessageAsync("", false, builder.Build());
        }

        [Command("help"), Alias("hjälp")]
        public async Task HelpAsync(string command)
        {
            // Delete the "help" command message from the user.
            if (!Context.IsPrivate)
                await Context.Message.DeleteAsync();

            var result = _service.Search(Context, command);

            if (!result.IsSuccess)
            {
                //await ReplyAsync($"Sorry, I couldn't find a command like **{command}**.");
                await Context.User.SendMessageAsync($"Verkar som att jag inte kunde hitta något kommando som liknar **{command}**.");
                return;
            }

            var builder = new EmbedBuilder()
            {
                Color = new Color(114, 137, 218),
                Description = $"Här är några kommandon som liknar **{command}**:"
            };

            foreach (var match in result.Commands)
            {
                var cmd = match.Command;

                builder.AddField(x =>
                {
                    x.Name = string.Join(", ", cmd.Aliases);
                    x.Value = $"Parametrar: {string.Join(", ", cmd.Parameters.Select(p => p.Name))}\n" +
                              $"Sammanfattning: {cmd.Summary}";
                    x.IsInline = false;
                });
            }

            //await ReplyAsync("", false, builder.Build());
            await Context.User.SendMessageAsync("", false, builder.Build());
        }
    }
}
