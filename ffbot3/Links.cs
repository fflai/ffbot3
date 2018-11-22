using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace FFBot2
{
    public static class Links
    {
        public static readonly Dictionary<FicNetwork, Regex> Expressions = new Dictionary<FicNetwork, Regex>()
        {
            { FicNetwork.FFN, new Regex("fanfiction.net\\/s\\/(\\d+)") },
            { FicNetwork.AO3, new Regex("archiveofourown.org/works/(\\d+)") },
        };
    }

    public class FicLink
    {
        public FicNetwork Network { get; set; }
        public string ID { get; set; }

        public static FicLink From(string url)
        {
            foreach (var network in Links.Expressions.Keys)
            {
                var regex = Links.Expressions[network];
                if (regex.IsMatch(url))
                {
                    return new FicLink
                    {
                        Network = network,
                        ID = regex.Match(url).Groups[1].Value,
                    };
                }
            }

            return null;
        }
    }
}
