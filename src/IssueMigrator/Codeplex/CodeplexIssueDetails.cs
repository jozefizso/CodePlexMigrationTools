using System;
using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;

namespace CodeplexMigration.IssueMigrator.Codeplex
{
    [DebuggerDisplay("{Issue.Id}: {Issue.Title}")]
    public class CodeplexIssueDetails
    {
        [JsonProperty("WorkItem")]
        public CodeplexIssue Issue { get; set; }

        public IEnumerable<CodeplexComment> Comments { get; set; }
    }
}
