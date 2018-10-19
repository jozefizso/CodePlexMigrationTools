using System;
using Newtonsoft.Json;

namespace CodeplexMigration.IssueMigrator.Codeplex
{
    /// <summary>
    ///
    /// </summary>
    /// <remarks>
    /// The author field is not present for comments exported from CodePlex Archive.
    /// </remarks>
    public class CodeplexComment
    {
        [JsonProperty("Message")]
        public string BodyHtml { get; set; }

        [JsonProperty("PostedDate")]
        public DateTimeOffset CreatedAt { get; set; }
    }
}