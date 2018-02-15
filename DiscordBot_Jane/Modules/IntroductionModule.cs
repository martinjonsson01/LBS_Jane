using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace DiscordBot_Jane.Core.Modules
{
    [Name("Introduktion")]
    public class IntroductionModule : ModuleBase<SocketCommandContext>
    {
        [Command("introducera dig själv"), Alias("introducera dig", "introducera")]
        public async Task IntroduceAsync()
        {
            await SendIntroduction();
        }

        [Command("introducera dig själv"), Alias("introducera dig", "introducera")]
        public async Task IntroduceAsync([Remainder]string remainder)
        {
            await SendIntroduction();
        }

        private async Task SendIntroduction()
        {
            var builder = new EmbedBuilder()
                .WithTitle("Källkod")
                .WithDescription("Jag är en discord-bot gjord för att påminna och updatera er om vad som händer på [Google Classroom](https://classroom.google.com). Ifall du behöver hjälp med något kommando så kan du fråga mig genom att skriva \"Jane hjälp\" i någon av text-kanalerna. ")
                .WithUrl("https://github.com/martinjonsson01/LBS_Jane")
                .WithColor(new Color(0xDE0190))
                .WithTimestamp(new DateTimeOffset(new DateTime(2018, 2, 4)))
                .WithFooter(footer =>
                {
                    footer
                        .WithText("Skapad av Martin")
                        .WithIconUrl("https://cdn.discordapp.com/avatars/153217095625211905/b20316e13a9d43e0ea1294208b9db6ef.png?size=32");
                })
                .WithThumbnailUrl("https://cdn.discordapp.com/avatars/409698620594454528/e6fcb735d4163612c92f5c66d7d2f66a.png?size=128")
                .WithImageUrl("https://lh3.googleusercontent.com/oYTH3eeyvxdQCYUfNGGBb0dVyGxpaBZMrL9go8FeYz85_nrvf6YceLfreJnOIRpHtCAJoXD3NbfOSDIUdlk1qE-96cbeSYrOE5cfexWDkI84BeRmm_iyUIoJzJX2BSQ8z-DreA9qn5QwXqAiSaxr-9OPyLofmz0CMsSIuIoqpsrkIlONavriKiJAm0eWJlDO-KCwttDSvnKOxqeRmbMg1AqjFti3i0nbb4ErBoOJUAR88uiWxD_GGNL47f88heUtGCj0UKmccxEoTAtaO8oZGMtc9x8uGt8FdORg-xzcT2zaXw9-3NrFlPmvnqNJchsTtazYb5sSz3WN762wyY_yJf0_5y-AOhROmuIxu4AHYxZNRMRxFkMbrVkKdN7Sex571Uuir-jhkSBQeIO-VyfjkUvftpg_jc9ZkY3JVXJAiS2tceIyPvKmAhUb53cp0XNEIf7W2SsjMqN7g4F7flbHQnZq9BvKnqGmMPX5CPk3Pmq_25FE4FvUyB22eXGSgLChZa29dQ1Bpk2aBJosYWQ5AkmrG23oqjT8K3Vn9exCGQ6Rrap9A_g9eMkIEqDDX-ldzGUA4kiCzgx81Ch3LW3v_JXh4C1W2TC3kgsHCIXtm9HFvinbnAPOuBr2Xzz0wcGg3j48BBuX-2qwRmdbN9ZgEnEz6z6HIU3y=w720-h273-no")
                .WithAuthor(author =>
                {
                    author
                        .WithName("Jane")
                        .WithIconUrl("https://cdn.discordapp.com/avatars/409698620594454528/e6fcb735d4163612c92f5c66d7d2f66a.png?size=32");
                })
                .AddField("Vad jag kan göra", "- Notifiera om nya meddelanden och uppgifter från classroom\n- Påminna om uppgifter som ska lämnas in inom 24 timmar")
                .AddField("Vad jag kommer att kunna göra", "- Påminna om mattevideos\n- Påminna om idrottslektioner\n- Svara på frågor angående när uppgifter ska lämnas in\n- Kompilera allt som ska lämnas in till ett enda dokument/kalender för att ge användare en överblick över t.ex. den kommande veckan.\n- Påminna om uppkommande prov inom vissa ämnen (t.ex. Matte eller Samhäll)");
            var embed = builder.Build();

            await ReplyAsync("", false, embed);
        }
    }
}
