using System;

namespace CodePlexIssueMigrator
{
    using System.Collections.Generic;

    public class CodePlexIssue
    {
        public CodePlexIssue()
        {
            this.Comments = new List<CodeplexComment>();
        }

        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public string Type { get; set; }
        public string Impact { get; set; }

        public List<CodeplexComment> Comments { get; private set; }

        public DateTimeOffset ReportedAtUtc { get; set; }

        public DateTimeOffset? ClosedAtUtc { get; set; }

        public string ReportedBy { get; set; }

        public bool IsClosed
        {
            get { return this.ClosedAtUtc.HasValue; }
        }
    }
}