using System;
using System.Diagnostics.Contracts;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json.Linq;

namespace FFBot2
{
	public class Search
	{
		public class SearchResult
		{
			string Network;
			string FicId;
		}

		public static async Task<FicLink> SearchFor(string fic)
		{
			if (fic.Length < 4)
				return null;

			try
			{
				var res = await MakeRequest(fic);

				for (int i = 0; i < 10; i++)
				{
					var searchResult = ResultAt(res, i);

					var url = await GetFinalRedirect(searchResult["url"].Value<string>());

					var link = FicLink.From(url);

					if (link != null)
						return link;
				}

				return null;
			}
			catch (Exception e)
			{
				Console.WriteLine("Error when search for " + fic);
				Console.WriteLine(e.ToString());
				return null;
			}
		}

		static JToken ResultAt(JObject search, int index)
		{
			if (search["webPages"]?["value"]?[index] == null)
				return null;

			return search["webPages"]["value"][index];
		}

		async static Task<JObject> MakeRequest(string query)
		{
            using (var wc = new WebClient())
            {
                var queryString = HttpUtility.ParseQueryString(string.Empty);

                // Request headers
                wc.Headers.Add("Ocp-Apim-Subscription-Key", "<hidden for git>");

                // Request parameters
                queryString["q"] = query;
                queryString["count"] = "10";
                queryString["offset"] = "0";
                queryString["mkt"] = "en-us";
                queryString["safesearch"] = "Off";
                var uri = "https://api.cognitive.microsoft.com/bing/v5.0/search?" + queryString;

                var res = await wc.DownloadStringTaskAsync(uri);
                return JObject.Parse(res);
            }
		}

		async static Task<string> GetFinalRedirect(string url)
		{
			if (string.IsNullOrWhiteSpace(url))
				return url;

			int maxRedirCount = 8;  // prevent infinite loops
			string newUrl = url;
			do
			{
				HttpWebRequest req = null;
				HttpWebResponse resp = null;
				try
				{
					req = (HttpWebRequest)WebRequest.Create(url);
					req.Method = "HEAD";
					req.AllowAutoRedirect = false;
					resp = (HttpWebResponse)(await req.GetResponseAsync());
					switch (resp.StatusCode)
					{
						case HttpStatusCode.OK:
							return newUrl;
						case HttpStatusCode.Redirect:
						case HttpStatusCode.MovedPermanently:
						case HttpStatusCode.RedirectKeepVerb:
						case HttpStatusCode.RedirectMethod:
							newUrl = resp.Headers["Location"];
							if (newUrl == null)
								return url;

							if (newUrl.IndexOf("://", System.StringComparison.Ordinal) == -1)
							{
								// Doesn't have a URL Schema, meaning it's a relative or absolute URL
								Uri u = new Uri(new Uri(url), newUrl);
								newUrl = u.ToString();
							}
							break;
						default:
							return newUrl;
					}
					url = newUrl;
				}
				catch (WebException)
				{
					// Return the last known good URL
					return newUrl;
				}
				catch (Exception ex)
				{
					return null;
				}
				finally
				{
					if (resp != null)
						resp.Close();
				}
			} while (maxRedirCount-- > 0);

			return newUrl;
		}

	}

}
