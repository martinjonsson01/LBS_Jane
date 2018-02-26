using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot_Jane.Core.Services;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace DiscordBot_Jane.Core.Modules
{
    [Name("Memes")]
    public class MemeModule : ModuleBase<SocketCommandContext>
    {
        private readonly IConfigurationRoot _config;
        private readonly LoggingService _logger;
        private readonly Random _random;
        private readonly CommandHandler _commandHandler;

        public MemeModule(IConfigurationRoot config, LoggingService logger, Random random, CommandHandler commandHandler)
        {
            _config = config;
            _logger = logger;
            _random = random;
            _commandHandler = commandHandler;
        }

        #region Meme

        [Command("meme"), Alias("dank meme", "maymay", "hit me up with a dank meme")]
        [Summary("Svarar med en meme direktplockad från meme-fälten i Kazakstan")]
        [RequireUserPermission(GuildPermission.SendMessages)]
        public async Task SendMeme()
        {
            dynamic embed = await GetMemeEmbed();

            await ReplyAsync("", false, embed);
        }

        [Command("meme"), Alias("dank meme", "maymay")]
        [Summary("Svarar med en meme direktplockad från meme-fälten i Kazakstan")]
        [RequireUserPermission(GuildPermission.SendMessages)]
        public async Task SendMeme([Remainder] string rest)
        {
            dynamic embed = await GetMemeEmbed();

            await ReplyAsync("", false, embed);
        }

        private async Task<dynamic> GetMemeEmbed()
        {
            // Only get a new JSON object every hour or if it is null.
            if (_commandHandler.LastCacheUpdate.AddHours(1) <= DateTime.Now || _commandHandler.MemeJsonCache == null)
            _commandHandler.MemeJsonCache =
                            await GetJsonObject("https://www.reddit.com/r/dankmemes/top.json?sort=top&t=day&limit=500");

            dynamic allPosts = _commandHandler.MemeJsonCache.data.children;

            var posts = new List<dynamic>();

            // Only get image posts.
            if (allPosts != null)
            {
                foreach (var allPost in allPosts)
                {
                    if (allPost.data.post_hint == "image")
                        posts.Add(allPost);
                }
            }
            
            // If MemeIndex doesn't contain current guild, or if MemeIndex is over max.
            if (!_commandHandler.MemeIndex.ContainsKey(Context.Guild.Name) ||
                _commandHandler.MemeIndex[Context.Guild.Name] >= posts.Count)
            {
                // Reset index.
                _commandHandler.MemeIndex.Add(Context.Guild.Name, 1);
                // Reset cache as well.
                _commandHandler.MemeJsonCache = null;
            }

            var post = posts[_commandHandler.MemeIndex[Context.Guild.Name]];
            _commandHandler.MemeIndex[Context.Guild.Name]++;

            string title = post.data.title ?? "null";
            string url = post.data.url ?? "null";

            var builder = new EmbedBuilder()
                .WithTitle(title)
                .WithDescription($"[{url}]({url})")
                .WithUrl(url)
                .WithColor(new Color((byte)_random.Next(0, 255), (byte)_random.Next(0, 255), (byte)_random.Next(0, 255)))
                .WithImageUrl(url);
            var embed = builder.Build();
            return embed;
        }

        private async Task<dynamic> GetJsonObject(string uri)
        {
            using (var wc = new WebClient())
            {
                var jsonString = await wc.DownloadStringTaskAsync(new Uri(uri));
                _commandHandler.LastCacheUpdate = DateTime.Now;
                return JsonConvert.DeserializeObject(jsonString);
            }
        }

        #endregion Meme

        #region Ratewaifu

        [Command("rate waifu"), Alias("waifu rate", "ratewaifu", "rate wife", "rate")]
        [Summary("Rate-ar din waifu på en skala 1-10.")]
        [RequireUserPermission(GuildPermission.SendMessages)]
        public async Task RateWaifu()
        {
            await ReplyAsync($"Du är en {_random.Next(0, 11)}/10 waifu 😄");
        }

        [Command("rate waifu"), Alias("waifu rate", "ratewaifu", "rate wife", "rate")]
        [Summary("Rate-ar din waifu på en skala 1-10.")]
        [RequireUserPermission(GuildPermission.SendMessages)]
        public async Task RateWaifu([Remainder] SocketGuildUser user)
        {
            if (user == Context.User)
                await ReplyAsync($"Du är en {_random.Next(0, 11)}/10 waifu 😄");
            else
                await ReplyAsync($"{user.Username} är en {_random.Next(0, 11)}/10 waifu 😄");
        }

        [Command("rate waifu"), Alias("waifu rate", "ratewaifu", "rate wife", "rate")]
        [Summary("Rate-ar din waifu på en skala 1-10.")]
        [RequireUserPermission(GuildPermission.SendMessages)]
        public async Task RateWaifu(string waifu)
        {
            if (waifu == "me" || waifu == "jag")
                await ReplyAsync($"Du är en {_random.Next(0, 11)}/10 waifu 😄");
            else
                await ReplyAsync($"{waifu} är en {_random.Next(0, 11)}/10 waifu 😄");
        }

        #endregion Ratewaifu
    }
}
