using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace CodeplexMigration.IssueMigrator.Templates
{
    public class CommentTemplate : TemplateBase
    {
        public CommentTemplate()
            : base("comment")
        {
        }

        public string UserAvatar { get; set; }

        protected override void OnFormatTemplate(StringBuilder sb)
        {
            base.OnFormatTemplate(sb);

            sb.Replace("${user_avatar}", this.UserAvatar);
        }
    }
}
