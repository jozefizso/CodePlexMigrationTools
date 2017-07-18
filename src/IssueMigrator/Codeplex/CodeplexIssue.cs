using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace CodeplexMigration.IssueMigrator.Codeplex
{
    public class CodeplexIssue
    {
        public CodeplexIssue()
        {
            this.Comments = new List<CodeplexComment>();
        }

        public int Id { get; set; }

        public string Title { get; set; }

        public string DescriptionHtml { get; set; }

        public string Status { get; set; }

        public string Type { get; set; }

        public string Impact { get; set; }

        public ICollection<CodeplexComment> Comments { get; }

        public DateTimeOffset ReportedAt { get; set; }

        public DateTimeOffset? ClosedAt { get; set; }

        public string ReportedBy { get; set; }

        [XmlIgnore]
        public bool IsClosed
        {
            get { return this.ClosedAt.HasValue; }
        }
    }
}