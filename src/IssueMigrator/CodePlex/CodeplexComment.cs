using System;

namespace CodePlexIssueMigrator
{
    public class CodeplexComment
    {
        public string Author { get; set; }

        public string BodyHtml { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
    }
}