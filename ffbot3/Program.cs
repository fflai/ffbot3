using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Discord.WebSocket;

namespace FFBot2
{
    class MainClass
    {
        static RecCounter RecCounter = new RecCounter("recs.json", "reclog.csv");

        static Discord.IDiscordClient DiscordClient;
        public static Config Config { get; set; }

        static ConcurrentDictionary<ulong, Discord.IUserMessage> SentMessages = new ConcurrentDictionary<ulong, Discord.IUserMessage>();

        static QuoteManager Quotes = new QuoteManager();

        public static void Main(string[] args)
        {
            if (!Directory.Exists("quotes"))
            {
                Console.WriteLine("Warning: Creating quotes directory.");
                Directory.CreateDirectory("quotes");
            }

            var configStr = File.ReadAllText(args.Length > 0 ? args[0] : "config.json");
            Config = Newtonsoft.Json.JsonConvert.DeserializeObject<Config>(configStr);

            var discord = new DiscordSocketClient();
            DiscordClient = discord;
            discord.Disconnected += (e) =>
            {
                Console.WriteLine("Disconnected?: " + e.ToString());
                Environment.Exit(0);
                return Task.FromResult("lel");
            };
            discord.MessageReceived += (e) => Task.Run(() => ProcessMessage(e));
            discord.MessageDeleted += (e, u) => Task.Run(() => DeletedMessage(e, u));
            discord.MessageUpdated += (e, m, u) => Task.Run(() => UpdatedMessage(e, m, u));


            var loginTask = discord.LoginAsync(Discord.TokenType.Bot, File.ReadAllLines("token.txt")[0]);
            loginTask.Wait();
            Console.WriteLine("Logged in!");
            var startTask = Task.Run(discord.StartAsync);
            startTask.Wait();
            Console.WriteLine("Started.");


            Thread.Sleep(int.MaxValue);
        }

        static async Task ProcessMessage(Discord.IMessage message)
        {
            if (message.Author.Id == DiscordClient.CurrentUser.Id)
                return;

            var server = Config.Servers.SingleOrDefault(a => a.ChannelIds.Contains(message.Channel.Id));

            if (server == null)
                return;

            if (server.AllowQuotes && await HandleQuote(message))
                return;

            var res = await GetResponse(message.Content, message.Author.Discriminator);
            if (res != null)
            {
                var msg = await message.Channel.SendMessageAsync("", false, res);
                SentMessages.TryAdd(message.Id, msg);
            }
        }

        static async Task DeletedMessage(Discord.Cacheable<Discord.IMessage, ulong> message, ISocketMessageChannel channel)
        {
            if (SentMessages.TryGetValue(message.Id, out var toDelete))
            {
                SentMessages.TryRemove(message.Id, out _);
                await toDelete.DeleteAsync();
            }
        }

        static async Task UpdatedMessage(Discord.Cacheable<Discord.IMessage, ulong> message, SocketMessage updatedMessage, ISocketMessageChannel channel)
        {
            if (SentMessages.TryGetValue(message.Id, out var oldMessage))
            {
                var res = await GetResponse(updatedMessage.Content, updatedMessage.Author.Discriminator);
                await oldMessage.ModifyAsync((props) => props.Embed = res);
            }
            else
            {
                await ProcessMessage(updatedMessage);
            }
        }

        static async Task<bool> HandleQuote(Discord.IMessage msg)
        {
            try
            {
                if (!msg.Content.StartsWith("!quote "))
                    return false;

                var msgSplit = msg.Content.Split(' ');
                if (msgSplit.Length < 2)
                    return true;

                string verb = msgSplit[1];

                switch (verb)
                {
                    case "show":
                        if (int.TryParse(TextAfter(msg.Content, "show").Trim(), out var quoteNum))
                        {
                            await Quotes.DisplayQuote(quoteNum, msg.Channel);
                        }
                        break;
                    case "find":
                        var text = TextAfter(msg.Content, "find").Trim();
                        if (text.Length > 0)
                        {
                            await Quotes.DisplayFindQuote(text, msg.Channel);
                        }
                        break;
                    case "add":
                        var toAdd = TextAfter(msg.Content, "add").Trim();
                        if (toAdd.Length > 0)
                        {
                            await Quotes.DisplayAddQuote(toAdd, msg.Channel);
                        }
                        break;
                    case "dump":
                        await Quotes.DumpQuotes(msg.Channel);
                        break;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error handling quote: {0}", e);
            }

            return true;
        }

        private async static Task<Discord.Embed> GetResponse(string message, string user)
        {
            var networkRegex = new Regex(@"`([^`]+)`|!l\(([^)]+)\)|link\(([^)]+)\)", RegexOptions.IgnoreCase);
            var ffnRegex = new Regex(@"linkffn\(([^)]+)\)", RegexOptions.IgnoreCase);
            var ao3Regex = new Regex(@"linkao3\(([^)]+)\)", RegexOptions.IgnoreCase);

            var linkRegex = new Regex(@"https?:\/\/[^\s]+", RegexOptions.IgnoreCase);

            // First, let's look at directly linked stories:
            var ficLink = FicLink.From(message);
            if (ficLink != null)
                return await GetStory(ficLink, true, user);

            // Now, let's look for linkffn(search) and the others.
            if (networkRegex.IsMatch(message))
            {
                var match = GetValue(networkRegex.Match(message));
                var fic = await Search.SearchFor(match + " fanfiction");

                if (fic != null)
                    return await GetStory(fic, false, user);
            }

            if (ffnRegex.IsMatch(message))
            {
                var match = GetValue(ffnRegex.Match(message));

                int tmp;
                if (int.TryParse(match, out tmp))
                {
                    return await GetStory(new FicLink
                    {
                        ID = match,
                        Network = FicNetwork.FFN
                    }, false, user);
                }

                var fic = await Search.SearchFor(match + " site:fanfiction.net");

                if (fic != null)
                    return await GetStory(fic, false, user);
            }

            if (ao3Regex.IsMatch(message))
            {
                var match = GetValue(ao3Regex.Match(message));

                int tmp;
                if (int.TryParse(match, out tmp))
                {
                    return await GetStory(new FicLink
                    {
                        ID = match,
                        Network = FicNetwork.AO3
                    }, false, user);
                }

                var fic = await Search.SearchFor(match + " site:archiveofourown.org");

                if (fic != null)
                    return await GetStory(fic, false, user);
            }

            return null;
        }


        static string GetValue(Match match)
        {
            for (int i = 1; i < match.Groups.Count; i++)
                if (!string.IsNullOrEmpty(match.Groups[i].Value))
                    return match.Groups[i].Value;

            return match.Value;
        }

        private static async Task<Discord.Embed> GetStory(FicLink link, bool alreadyLinked, string forUser)
        {
            try
            {
                Story story;

                if (link.Network == FicNetwork.FFN)
                    story = await FFNCrawler.CrawlStory(link.ID);
                else if (link.Network == FicNetwork.AO3)
                    story = await AO3Crawler.CrawlStory(link.ID);
                else
                    return null;

                string statusLine = "Published " + story.Published.ToShortDateString();
                if (story.Updated != null)
                    statusLine += ", Updated " + ((DateTime)story.Updated).ToShortDateString();

                statusLine += "  -  " + story.NumReviews + " Reviews, " + story.NumFavorites + " Favorites";

                string numWords = story.NumWords.ToString("N0", CultureInfo.InvariantCulture).Replace(",", "'");


                var numRecs = RecCounter.GetNumRecommendations(story);

                if (forUser != null)
                    numRecs = RecCounter.Recommend(story, forUser);

                var desc = story.Description;

                List<string> descriptionLines = new List<string>();

                while (desc.Length > 100)
                {
                    var lastSpaceRight = desc.LastIndexOf(' ', 100);
                    if (lastSpaceRight == -1)
                    {
                        lastSpaceRight = desc.IndexOf(' ');
                        if (lastSpaceRight == -1)
                        {
                            descriptionLines.Add(desc);
                            break;
                        }
                    }
                    descriptionLines.Add(desc.Substring(0, lastSpaceRight));
                    desc = desc.Substring(lastSpaceRight + 1);
                }

                if (desc.Length > 0)
                {
                    descriptionLines.Add(desc);
                }

                string description = Clean(story.Description);
                var updated = story.Updated ?? story.Published;

                var builder = new Discord.EmbedBuilder();
                builder.WithAuthor(story.Author.PenName, url: story.Author.Url);
                builder.Title = story.Title;
                builder.Url = story.Url;
                builder.Description = story.Description;

                var book = "";
                if (story.IsComplete || DateTime.Now.Subtract(updated).TotalDays < 14)
                    book = ":green_book:";
                else if (DateTime.Now.Subtract(updated).TotalDays < 180)
                    book = ":orange_book:";
                else
                    book = ":closed_book:";

                var updateString = AgoString(updated) + (story.IsComplete ? " - Complete!" : "");

                builder.AddInlineField(book + " Last Updated", updateString);

                builder.AddInlineField(":book: Length", $"{FormatBigNumber(story.NumWords)} words in {story.NumChapters} chapters");

                return builder.Build();
            }
            catch (Exception e)
            {
                Console.WriteLine("Error when crawling story " + link.ID + " at " + link.Network);
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                return null;
            }
        }

        private static string AgoString(DateTime dateTime)
        {
            var el = DateTime.Now - dateTime;

            if (el.TotalDays <= 1)
                return "recently";

            if (el.TotalDays <= 7)
                return "about " + Math.Floor(el.TotalDays + 1) + " days ago";

            if (el.TotalDays <= 28)
                return "about " + Math.Floor((el.TotalDays / 7) + 1) + " weeks ago";


            if (el.TotalDays <= 365.25)
                return "about " + Math.Floor((el.TotalDays / 30) + 1) + " months ago";

            return "about " + Math.Floor((el.TotalDays / (365.25)) + 1) + " years ago";
        }

        private static string TextAfter(string haystack, string needle)
        {
            var pos = haystack.IndexOf(needle);
            return haystack.Substring(pos + needle.Length);
        }

        private static string Clean(string inString)
        {
            return inString
                .Replace("\n", "   ")
                .Replace("\\", "\\\\")
                .Replace("*", "\\*")
                .Replace("_", "\\_")
                ;
        }

        private static string FormatBigNumber(int bigNumber)
        {
            string res = "";

            while (bigNumber > 0)
            {
                if (bigNumber < 1000)
                {
                    res = bigNumber + res;
                    break;
                }

                var segment = "'" + (bigNumber % 1000).ToString().PadLeft(3, '0');
                res = segment + res;

                bigNumber /= 1000;
            }

            return res;
        }
    }
}
