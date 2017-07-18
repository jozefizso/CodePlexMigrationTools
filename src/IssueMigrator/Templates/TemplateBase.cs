using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace CodeplexMigration.IssueMigrator.Templates
{
    public abstract class TemplateBase
    {
        protected TemplateBase(string templateName)
        {
            this.TemplateName = templateName;
        }

        public string TemplateName { get; private set; }


        public string CodeplexAvatar { get; set; }
        public string OriginalUserName { get; set; }
        public string OriginalUserUrl { get; set; }
        public string OriginalDateUtc { get; set; }
        public string OriginalDate { get; set; }
        public string OriginalUrl { get; set; }
        public string OriginalBody { get; set; }

        public string Format()
        {
            var template = GetTemplate(this.TemplateName);
            
            var sb = new StringBuilder(template);
            this.OnFormatTemplate(sb);
            return sb.ToString();
        }

        protected virtual void OnFormatTemplate(StringBuilder sb)
        {
            sb.Replace("${codeplex_avatar}", this.CodeplexAvatar);
            sb.Replace("${original_user_name}", this.OriginalUserName);
            sb.Replace("${original_user_url}", this.OriginalUserUrl);
            sb.Replace("${original_date_utc}", this.OriginalDateUtc);
            sb.Replace("${original_date}", this.OriginalDate);
            sb.Replace("${original_url}", this.OriginalUrl);
            sb.Replace("${original_body}", this.OriginalBody);
        }

        private static string GetTemplate(string templateName)
        {
            var assembly = Assembly.GetExecutingAssembly();

            using (var stream = assembly.GetManifestResourceStream($"CodeplexMigration.IssueMigrator.Templates.{templateName}.md.txt"))
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
