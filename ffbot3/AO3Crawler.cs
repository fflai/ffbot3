using System;
using System.Net;
using System.Linq;
using HtmlAgilityPack;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO;
using System.Text;

namespace FFBot2
{
    public class AO3Crawler
    {
        public async static Task<Story> CrawlStory(string id)
        {
            string url = "https://archiveofourown.org/works/" + id + "?view_adult=true";

            CookieContainer container = new CookieContainer();

            HttpWebRequest request = GetNewRequest(url, container);
            HttpWebResponse response = (HttpWebResponse)(await request.GetResponseAsync());
            while (response.StatusCode == HttpStatusCode.Found)
            {
                response.Close();
                request = GetNewRequest(response.Headers["Location"], container);
                response = (HttpWebResponse)(await request.GetResponseAsync());
            }

            var resStream = response.GetResponseStream();

            string resHtml;
            using (var ms = new MemoryStream())
            {
                await resStream.CopyToAsync(ms);
                resHtml = Encoding.UTF8.GetString(ms.ToArray());
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(resHtml);
            resStream.Close();
            response.Close();

            var story = ExtractStory(doc, id);

            return story;
        }

        static HttpWebRequest GetNewRequest(string targetUrl, CookieContainer container)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(targetUrl);
            request.CookieContainer = container;
            request.AllowAutoRedirect = false;
            return request;
        }

        public static Story ExtractStory(HtmlDocument doc, string id)
        {
            Story story = new Story();

            story.ID = id;

            story.Title = NodeString(doc, "h2", "title heading");


            var authorEle = FindNode(doc, "h3", "byline heading");
            var authorUrl = "https://archiveofourown.org" + authorEle.Descendants("a").First().GetAttributeValue("href", "");

            story.Author = new Author
            {
                ID = 0,
                PenName = authorEle.InnerText,
                Url = authorUrl,
            };
            story.Description = doc.DocumentNode
                .Descendants("blockquote")
                .FirstOrDefault(a => a.ParentNode.Attributes["class"]?.Value == "summary module")?
                .InnerText?
                .Trim();
            story.Rating = NodeString(doc, "dd", "rating tags");
            story.NumWords = NodeInt(doc, "dd", "words");
            story.NumFavorites = NodeInt(doc, "dd", "kudos");
            story.NumFollows = NodeInt(doc, "dd", "bookmarks");
            story.Language = NodeString(doc, "dd", "language");
            story.NumReviews = NodeInt(doc, "dd", "comments");

            story.Published = DateTime.Parse(NodeString(doc, "dd", "published"));

            var updatedString = NodeString(doc, "dd", "status");
            if (updatedString != null)
                story.Updated = DateTime.Parse(updatedString);

            var chapters = NodeString(doc, "dd", "chapters").Split('/');
            story.NumChapters = int.Parse(chapters[0]);
            story.IsComplete = chapters[0] == chapters[1];


            story.Network = FicNetwork.AO3;
            story.Url = "https://archiveofourown.org/works/" + story.ID;

            return story;
        }

        static HtmlNode FindNode(HtmlDocument doc, string tag, string className)
        {
            return doc.DocumentNode.Descendants(tag).SingleOrDefault(a => ClassName(a) == className);
        }

        static string NodeString(HtmlDocument doc, string tag, string className)
        {
            var node = FindNode(doc, tag, className);

            if (node == null)
                return null;

            return node.InnerText.Trim();
        }

        static int NodeInt(HtmlDocument doc, string tag, string className)
        {
            var content = NodeString(doc, tag, className);

            if (content == null)
                return 0;

            return int.Parse(content.Replace(",", ""));
        }



        private static string ClassName(HtmlNode node)
        {
            return node.Attributes["class"]?.Value ?? "";
        }
    }
}
