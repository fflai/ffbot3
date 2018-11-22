using System;
namespace FFBot2
{
    public class Author
    {
        public string PenName;
        public int ID;
        public string Url;
    }

    public class Story
    {
        public string ID;
        public Author Author;
        public string Title;
        public string Description;
        public int NumWords;
        public int NumChapters;
        public int NumFavorites;
        public int NumReviews;
        public bool IsComplete;
        public string Rating;
        public string Language;
        public int NumFollows;
        public DateTime Published;
        public DateTime? Updated;

        public FicNetwork Network;
        public string Url;
    }

    public enum FicNetwork
    {
        AO3,
        FFN
    }
}
