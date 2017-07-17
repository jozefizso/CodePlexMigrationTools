using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace IssueMigrator
{
    public class CommentTemplate
    {
        public string UserAvatar { get; set; }
        public string OriginalUserName { get; set; }
        public string OriginalUserUrl { get; set; }
        public string OriginalDateUtc { get; set; }
        public string OriginalDate { get; set; }
        public string OriginalBody { get; set; }

        public string Format()
        {
            var template = GetCommentTemplate();
            
            var sb = new StringBuilder(template);
            sb.Replace("${user_avatar}", this.UserAvatar);
            sb.Replace("${original_user_name}", this.OriginalUserName);
            sb.Replace("${original_user_url}", this.OriginalUserUrl);
            sb.Replace("${original_date_utc}", this.OriginalDateUtc);
            sb.Replace("${original_date}", this.OriginalDate);
            sb.Replace("${original_body}", this.OriginalBody);

            return sb.ToString();
        }

        private static string GetCommentTemplate()
        {
            return GetTemplate("comment");
        }

        private static string GetTemplate(string templateName)
        {
            var assembly = Assembly.GetExecutingAssembly();

            using (var stream = assembly.GetManifestResourceStream($"IssueMigrator.Templates.{templateName}.md.txt"))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException($"Teamplate with name '{templateName}.md.txt' does not exists as embedded resource in assembly.");
                }

                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }
    }
}
