using System;
using System.Diagnostics;

namespace CodeplexMigration.IssueMigrator.Codeplex
{
    [DebuggerDisplay("Id={Id}, Title={Title}")]
    public class CodeplexIssueInfo
    {
        public int Id { get; set; }

        public string Title { get; set; }
    }
}