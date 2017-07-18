using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace CodePlexIssueMigrator
{

    public class CodePlexIssue
    {
        public CodePlexIssue()
        {
            this.Comments = new List<CodeplexComment>();
        }

        public int Id { get; set; }
        public string Title { get; set; }
        public string DescriptionHtml { get; set; }
        public string Status { get; set; }
        public string Type { get; set; }
        public string Impact { get; set; }

        public List<CodeplexComment> Comments { get; private set; }

        public DateTimeOffset ReportedAtUtc { get; set; }

        public DateTimeOffset? ClosedAtUtc { get; set; }

        public string ReportedBy { get; set; }

        [XmlIgnore]
        public bool IsClosed
        {
            get { return this.ClosedAtUtc.HasValue; }
        }
    }
}