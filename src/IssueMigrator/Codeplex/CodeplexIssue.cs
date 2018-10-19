using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace CodeplexMigration.IssueMigrator.Codeplex
{
    public class CodeplexIssue
    {
        public int Id { get; set; }

        [JsonProperty("Summary")]
        public string Title { get; set; }

        [JsonProperty("Description")]
        public string Description { get; set; }

        public string ProjectName { get; set; }

        public CodeplexIssueStatus Status { get; set; }

        public CodeplexIssueType Type { get; set; }

        public CodeplexIssuePriority Priority { get; set; }

        [JsonProperty("ReportedDate")]
        public DateTimeOffset ReportedAt { get; set; }

        [JsonProperty("ClosedDate")]
        public DateTimeOffset? ClosedAt { get; set; }

        public string ClosedComment { get; set; }

        [JsonIgnore]
        public bool IsClosed
        {
            get { return this.ClosedAt.HasValue; }
        }
    }

    public class CodeplexIssuePriority
    {
        public string Name { get; set; }
        public int Severity { get; set; }
        public int Id { get; set; }
    }

    public class CodeplexIssueStatus
    {
        public string Name { get; set; }
        public int Id { get; set; }
    }

    public class CodeplexIssueType
    {
        public string Name { get; set; }
        public int Id { get; set; }
    }
}