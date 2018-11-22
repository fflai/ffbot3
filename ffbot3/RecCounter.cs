using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Newtonsoft.Json;

namespace FFBot2
{
    public class RecCounter
    {
        string FileName { get; set; }
        string RecLogFileName { get; set; }

        Dictionary<string, int> Recommendations = new Dictionary<string, int>();

        public RecCounter(string file, string recLog)
        {
            FileName = file;
            RecLogFileName = recLog;
            Load();
        }

        public int Recommend(Story story, string recommender)
        {
            lock (Recommendations)
            {
                string id = story.Network.ToString() + "!" + story.ID;

                if (!Recommendations.ContainsKey(id))
                    Recommendations[id] = 0;

                var res = ++Recommendations[id];
                Save();
                File.AppendAllText(RecLogFileName, DateTime.UtcNow.ToString("o") + ";" + id + ";" + recommender + Environment.NewLine);

                return res;
            }
        }

        public int GetNumRecommendations(Story story)
        {
            string id = story.Network.ToString() + "!" + story.ID;

            if (!Recommendations.ContainsKey(id))
                return 0;

            return Recommendations[id];
        }

        void Save()
        {
            File.WriteAllText(FileName, JsonConvert.SerializeObject(Recommendations));
        }

        void Load()
        {
            if (File.Exists(FileName))
            {
                Recommendations = JsonConvert.DeserializeObject<Dictionary<string, int>>(File.ReadAllText(FileName));
            }
        }
    }
}
