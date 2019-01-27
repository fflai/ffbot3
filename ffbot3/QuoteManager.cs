using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FFBot2
{
    class QuoteManager
    {
        Dictionary<string, List<string>> QuoteCache = new Dictionary<string, List<string>>();

        Random rand = new Random();

        public async Task DisplayQuote (int number, IMessageChannel channel)
        {
            SocketGuild server = ((SocketGuildChannel)channel).Guild;

            var quote = FindQuote(server.Id.ToString(), number);

            if (quote != null)
            {
                await SendQuote(number, quote, channel);
            }
            else
            {
                await channel.SendMessageAsync("Could not find quote #" + number);
            }
        }

        public async Task DisplayFindQuote(string text, IMessageChannel channel)
        {
            SocketGuild server = ((SocketGuildChannel)channel).Guild;

            var res = SearchQuote(server.Id.ToString(), text);
            if (res < 0)
            {
                await channel.SendMessageAsync("Could not find quote for this");
            }
            else
            {
                await DisplayQuote(res, channel);
            }
        }
        public async Task DisplayAddQuote(string quote, IMessageChannel channel)
        {
            SocketGuild server = ((SocketGuildChannel)channel).Guild;

            var num = AddQuote(quote, server.Id.ToString());
            await channel.SendMessageAsync(":hash: Quote " + num + " added!");
        }

        public async Task DumpQuotes(IMessageChannel channel)
        {
            SocketGuild server = ((SocketGuildChannel)channel).Guild;
            string fileName = "quotes/" + server.Id + ".txt";
            await channel.SendFileAsync(fileName, "There you go!");
        }


        private async Task SendQuote(int number, string quote, IMessageChannel channel)
        {
            var msg = new Discord.EmbedBuilder();
            msg.Title = ":hash: Quote " + number;

            var res = "";
            foreach (var line in ParseQuote(quote))
            {
                if (line.Item1 != null)
                {
                    if (line.Item1.StartsWith("!"))
                    {
                        res += $"***{line.Item1.Substring(1).TrimStart()}** { line.Item2.TrimEnd() }*\r\n";
                    }
                    else
                    {
                        res += $"<**{line.Item1.Trim()}**> { line.Item2 } \r\n";
                    }
                }
                else
                {
                    res += $"{ line.Item2 }\r\n";
                }
            }

            msg.Description = res;
            msg.Color = Color.Red;

            await channel.SendMessageAsync("", embed: msg.Build());
        }
        private int SearchQuote (string server, string search)
        {
            search = search.ToLowerInvariant();
            var quotes = LoadQuotes(server);

            var words = search.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words)
            {
                quotes = quotes.Where(a => a.ToLowerInvariant().Contains(word)).ToList();
            }

            if (quotes.Count == 0)
                return -1;

            var quote = quotes[rand.Next(0, quotes.Count)];
            return GetQuoteId(quote);
        }

        private string FindQuote(string server, int number)
        {
            var quotes = LoadQuotes(server);

            var needle = $"[{number}] ";
            return quotes.SingleOrDefault(a => a.StartsWith(needle));
        }

        private int AddQuote(string quote, string server)
        {
            lock (this)
            {
                var quotes = LoadQuotes(server);
                var lastQuoteId = GetQuoteId(quotes.LastOrDefault() ?? "[0]");
                quotes.Add($"[{lastQuoteId + 1}] {quote.Trim()}");
                SaveQuotes(server);

                return lastQuoteId + 1;
            }
        }

        private List<string> LoadQuotes(string server)
        {
            lock (this)
            {
                if (QuoteCache.TryGetValue(server, out var res))
                    return res;

                string fileName = "quotes/" + server + ".txt";

                if (!File.Exists(fileName))
                    File.Create(fileName).Close();

                var read = File.ReadAllLines("quotes/" + server + ".txt");

                var quoteList = read.Where(a => !string.IsNullOrWhiteSpace(a));
                quoteList = read.Select(a => a.Replace("|||", "\n"));

                return QuoteCache[server] = new List<string>(quoteList);
            }
        }


        private void SaveQuotes(string server)
        {
            lock (this)
            {
                if (QuoteCache.TryGetValue(server, out var res))
                {
                    string fileName = "quotes/" + server + ".txt";

                    if (!File.Exists(fileName))
                        File.Create(fileName).Close();

                    File.WriteAllLines(fileName, res.Select(a => a.Replace("\r", "").Replace("\n", "|||")));
                }
            }
        }

        private IEnumerable<Tuple<string, string>> ParseQuote(string quote)
        {
            quote = quote.Substring(quote.IndexOf(']') + 1).Trim();

            foreach (var part in quote.Split('<'))
            {
                if (string.IsNullOrWhiteSpace(part))
                    continue;

                var thing = part.Split(new char[] { '>' }, 2);
                if (thing.Length == 2)
                {
                    yield return new Tuple<string, string>(thing[0], thing[1]);
                }
                else
                {
                    yield return new Tuple<string, string>(null, thing[0]);
                }
            }
        }

        private int GetQuoteId(string quote)
        {
            return int.Parse(quote.Substring(1, quote.IndexOf(']') - 1));
        }
    }
}
