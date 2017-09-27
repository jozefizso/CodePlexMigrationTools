using System;

namespace CodeplexMigration.IssueMigrator.Codeplex
{
    public class CodeplexComment
    {
        public string Author { get; set; }

        public string BodyHtml { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}