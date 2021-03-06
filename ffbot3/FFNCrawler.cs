﻿using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FFBot2
{
    public static class FFNCrawler
    {
        static Regex userRegex = new Regex("\\/u\\/(\\d+)\\/.+");
        static Regex favsRegex = new Regex("Favs: ((\\d|,)+) -");
        static Regex followsRegex = new Regex("Follows: ((\\d|,)+) -");
        static Regex ratingRegex = new Regex("Rated: ([^-]+) -");
        static Regex languageRegex = new Regex("Rated: [^-]+ - (\\w+) - ");
        static Regex wordsRegex = new Regex("Words: ((\\d|,)+) - ");
        static Regex chaptersRegex = new Regex("Chapters: (\\d+)");
        static Regex reviewsRegex = new Regex("Reviews: ((\\d|,)+) - ");
        static Regex idRegex = new Regex("id: ((\\d|,)+)");
        static Regex publishedRegex = new Regex("Published: ([^-]+) - ");
        static Regex updatedRegex = new Regex("Updated: ([^-]+) - ");


        public static async Task<Story> CrawlStory(string id)
        {
            string url = MainClass.Config.FanFictionUrl + "s/" + id + "/1/";
            string profileHtml = null;

            HttpClientHandler handler = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            using (var wc = new HttpClient(handler))
            {
                wc.DefaultRequestHeaders.Add("user-agent", "vlaaibot (contact: julianfanfic@gmail.com, vlaai on ao3 / ffn, or https://discord.gg/EZSVG7H0)");
                profileHtml = await wc.GetStringAsync(url);
            }

            // profileHtml = await wc.DownloadDataTaskAsync(url);
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(profileHtml);

            var story = ExtractStory(doc.GetElementbyId("profile_top"));

            return story.Item1;

        }

        static Tuple<Story, Author> ExtractStory(HtmlAgilityPack.HtmlNode ele)
        {
            var authorEle = ele.ChildNodes.SingleOrDefault(a => a.Name == "a" && userRegex.IsMatch(a.Attributes["href"].Value));

            int id = int.Parse(userRegex.Match(authorEle.Attributes["href"].Value).Groups[1].Value);
            string penname = authorEle.InnerText;

            var author = new Author()
            {
                ID = id,
                PenName = penname,
                Url = $"https://www.fanfiction.net/u/{id}",
            };

            var subdiv = ele.ChildNodes.Single(a => a.Name == "div");
            var description = subdiv.ChildNodes[0].InnerText;


            // We're removing all commas since they are annoyingly in numbers
            var statusLine = ele.ChildNodes.Single(a => a.Name == "span" && a.Attributes["class"]?.Value == "xgray xcontrast_txt").InnerText;

            string rating = ratingRegex.Match(statusLine).Groups[1].Value;
            string language = languageRegex.Match(statusLine).Groups[1].Value;

            Func<Regex, int> getInt = (Regex regex) => regex.IsMatch(statusLine) ? int.Parse(regex.Match(statusLine).Groups[1].Value.Replace(",", "")) : 0;

            var story = new Story();
            story.ID = idRegex.Match(statusLine).Groups[1].Value;
            story.Author = author;
            story.Title = ele.ChildNodes.Single(a => a.Name == "b" && a.Attributes["class"]?.Value == "xcontrast_txt").InnerText;
            story.Description = description;
            story.NumWords = getInt(wordsRegex);
            story.NumChapters = Math.Max(getInt(chaptersRegex), 1);
            story.NumFavorites = getInt(favsRegex);
            story.NumReviews = getInt(reviewsRegex);
            story.IsComplete = statusLine.Contains("Status: Complete");
            story.Rating = rating;
            story.Language = language;
            story.NumFollows = getInt(followsRegex);
            story.Published = ParseFFNDate(publishedRegex.Match(statusLine).Groups[1].Value);
            story.Network = FicNetwork.FFN;
            story.Url = "https://www.fanfiction.net/s/" + story.ID + "/1/";

            story.Updated = null;
            if (updatedRegex.IsMatch(statusLine))
                story.Updated = ParseFFNDate(updatedRegex.Match(statusLine).Groups[1].Value);

            return new Tuple<Story, Author>(story, author);
        }

        static DateTime ParseFFNDate(string text)
        {
            if(DateTime.TryParse(text, CultureInfo.GetCultureInfo("en-US"), DateTimeStyles.None, out var dt))
                return dt;

            return DateTime.Now;
        }
    }
}
