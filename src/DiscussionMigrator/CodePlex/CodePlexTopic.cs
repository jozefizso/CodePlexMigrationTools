namespace DiscussionMigrator
{
    using System.Collections.Generic;

    public class CodePlexTopic
    {
        public CodePlexTopic()
        {
            this.Comments = new List<CodeplexComment>();
        }

        public int Id { get; set; }

        public string Title { get; set; }

        public List<CodeplexComment> Comments { get; private set; }
    }
}