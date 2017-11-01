using System;
using Newtonsoft.Json;

namespace CodeplexMigration.IssueMigrator.Codeplex
{
    public class CodeplexComment
    {
        [JsonProperty("PostedBy")]
        public string Author { get; set; }

        [JsonProperty("Message")]
        public string BodyHtml { get; set; }

        [JsonProperty("PostedDate")]
        public DateTime CreatedAt { get; set; }
    }
}