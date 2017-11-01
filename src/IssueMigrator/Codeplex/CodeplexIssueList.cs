using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace CodeplexMigration.IssueMigrator.Codeplex
{
    public class CodeplexIssueList
    {
        [JsonProperty("List")]
        public List<CodeplexIssue> Issues { get; set; }


        [JsonProperty("TotalItemCount")]
        public int TotalItems { get; set; }
    }
}
