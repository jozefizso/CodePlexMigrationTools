using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Serialization;
using CodeplexMigration.IssueMigrator.Codeplex;
using Octokit;

namespace CodeplexMigration.IssueMigrator
{
    public class Program
    {
        private const string GitHubAvatar_CodeplexAvatar_User = "https://avatars.githubusercontent.com/u/30236365?s=192";

        // CodePlex project
        private static string codePlexProject;

        // GitHub owner (organization or user)
        private static string gitHubOwner;

        // GitHub repository
        private static string gitHubRepository;

        // GitHub API access token
        private static string gitHubAccessToken;

        private static GitHubClient client;
        private static HttpClient httpClient;

        private static readonly Random _rnd = new Random();

        static int Main(string[] args)
        {
            if (args.Length < 4)
            {
                Console.WriteLine("Missing arguments: [CodeplexProject] [GitHubOwner] [GitHubRepository] [GitHubAccessToken]");
                return 1;
            }

            codePlexProject = args[0];
            gitHubOwner = args[1];
            gitHubRepository = args[2];
            gitHubAccessToken = args[3];

            httpClient = new HttpClient();
            CacheIssues(codePlexProject);

            //var credentials = new Credentials(gitHubAccessToken);
            //var connection = new Connection(new ProductHeaderValue("CodeplexIssueMigrator")) { Credentials = credentials };
            //client = new GitHubClient(connection);
            //Console.WriteLine("Source: {0}.codeplex.com", codePlexProject);
            //Console.WriteLine("Destination: github.com/{0}/{1}", gitHubOwner, gitHubRepository);
            //Console.WriteLine("Migrating issues:");
            //MigrateIssues().Wait();

            Console.WriteLine();
            Console.WriteLine("Completed successfully.");
            return 0;
        }

        static void CacheIssues(string codeplexProject)
        {
            if (!Directory.Exists(".cache"))
            {
                Directory.CreateDirectory(".cache");
            }

            var issues = GetIssues();
            foreach (var issue in issues)
            {
                var xmlSerializer = new XmlSerializer(typeof(CodeplexIssue));

                using (var xmlWriter = new CodeplexXmlWriter($@".cache\{codeplexProject}_{issue.Id}.xml", Encoding.UTF8))
                {
                    xmlWriter.Formatting = Formatting.Indented;
                    xmlWriter.IndentChar = ' ';
                    xmlWriter.Indentation = 2;

                    xmlSerializer.Serialize(xmlWriter, issue);
                }

                using (var xmlReader = new XmlTextReader($@".cache\{codeplexProject}_{issue.Id}.xml"))
                {
                    xmlReader.DtdProcessing = DtdProcessing.Prohibit;
                    xmlReader.WhitespaceHandling = WhitespaceHandling.All;

                    var issue2 = xmlSerializer.Deserialize(xmlReader) as CodeplexIssue;

                    Debug.Assert(issue2 != null, "Issue cannot be deserialized.");
                    Debug.Assert(issue.DescriptionHtml == issue2.DescriptionHtml);
                }
            }
        }

        static async Task MigrateIssues()
        {
            var issues = GetIssues();
            foreach (var issue in issues)
            {
                var import = new NewIssueImport(issue.Title);

                var issueTemplate = new IssueTemplate();

                var codePlexIssueUrl = $"https://{codePlexProject}.codeplex.com/workitem/{issue.Id}";

                issueTemplate.CodeplexAvatar = GitHubAvatar_CodeplexAvatar_User;
                issueTemplate.OriginalUrl = codePlexIssueUrl;
                issueTemplate.OriginalUserName = issue.ReportedBy;
                issueTemplate.OriginalUserUrl = $"https://www.codeplex.com/site/users/view/{issue.ReportedBy}";
                issueTemplate.OriginalDate = issue.ReportedAt.ToString("R");
                issueTemplate.OriginalDateUtc = issue.ReportedAt.ToString("s");
                issueTemplate.OriginalBody = issue.DescriptionHtml;

                var issueBody = issueTemplate.Format();
                import.Issue.Body = issueBody.Trim();

                foreach (var comment in issue.Comments)
                {
                    var commentTemplate = new CommentTemplate();
                    commentTemplate.UserAvatar = $"https://github.com/identicons/{comment.Author}.png";
                    commentTemplate.OriginalUserName = comment.Author; 
                    commentTemplate.OriginalUserUrl = $"https://www.codeplex.com/site/users/view/{comment.Author}";
                    commentTemplate.OriginalDate = comment.CreatedAt.ToString("R");
                    commentTemplate.OriginalDateUtc = comment.CreatedAt.ToString("s");
                    commentTemplate.OriginalBody = comment.BodyHtml;

                    var commentBody = commentTemplate.Format();
                    var newComment = new NewIssueImportComment(commentBody);
                    newComment.CreatedAt = comment.CreatedAt.UtcDateTime;
                    import.Comments.Add(newComment);
                }

                import.Issue.Labels.Add("CodePlex");
                switch (issue.Type)
                {
                    case "Feature":
                        import.Issue.Labels.Add("enhancement");
                        break;
                    case "Issue":
                        import.Issue.Labels.Add("bug");
                        break;
                }
                // if (issue.Impact == "Low" || issue.Impact == "Medium" || issue.Impact == "High")
                //    labels.Add(issue.Impact);

                import.Issue.CreatedAt = issue.ReportedAt.UtcDateTime;
                if (issue.ClosedAt.HasValue)
                {
                    import.Issue.ClosedAt = issue.ClosedAt.Value.UtcDateTime;
                    import.Issue.Closed = true;
                }

                var gitHubIssue = await StartIssueImport(import);
                Console.WriteLine($"  Issue import task '{gitHubIssue.Id}': {gitHubIssue.Url}");

                Thread.Sleep(_rnd.Next(800, 1500));
            }
        }

        static IEnumerable<CodeplexIssue> GetIssues(int size = 100)
        {
            // Find the number of items
            var numberOfItems = GetNumberOfItems();

            Console.WriteLine("Found {0} items", numberOfItems);

            // Calculate number of pages
            var pages = (int)Math.Ceiling((double)numberOfItems / size);

            for (int page = 0; page < pages; page++)
            {
                var url = string.Format("https://{0}.codeplex.com/workitem/list/advanced?keyword=&status=All&type=All&priority=All&release=All&assignedTo=All&component=All&sortField=Id&sortDirection=Ascending&size={1}&page={2}", codePlexProject, size, page);
                var html = httpClient.GetStringAsync(url).Result;
                foreach (var issue in GetMatches(html, "<tr id=\"row_checkbox_\\d+\" class=\"CheckboxRow\">(.*?)</tr>"))
                {
                    var id = int.Parse(GetMatch(issue, "<td class=\"ID\">(\\d+?)</td>"));
                    var status = GetMatch(issue, "<td class=\"Status\">(.+?)</td>");
                    var type = GetMatch(issue, "<td class=\"Type\">(.+?)</td>");
                    var impact = GetMatch(issue, "<td class=\"Severity\">(.+?)</td>");
                    var title = GetMatch(issue, "<a id=\"TitleLink.*>(.*?)</a>");
                    Console.WriteLine("{0} ({1}) : {2}", id, status, title);
                    var codeplexIssue = GetIssue(id, status).Result;
                    codeplexIssue.Id = id;
                    codeplexIssue.Title = HtmlToMarkdown(title);
                    codeplexIssue.Status = status;
                    codeplexIssue.Type = type;
                    codeplexIssue.Impact = impact;
                    yield return codeplexIssue;
                }
            }
        }

        private static int GetNumberOfItems()
        {
            var url = string.Format("https://{0}.codeplex.com/workitem/list/advanced?keyword=&status=All&type=All&priority=All&release=All&assignedTo=All&component=All&sortField=LastUpdatedDate&sortDirection=Descending&page=0&reasonClosed=All",codePlexProject);
            var html = httpClient.GetStringAsync(url).Result;
            return int.Parse(GetMatch(html, "Selected\">(\\d+)</span> items"));
        }

        static async Task<CodeplexIssue> GetIssue(int number, string status)
        {
            var url = string.Format("https://{0}.codeplex.com/workitem/{1}", codePlexProject, number);
            var html = await httpClient.GetStringAsync(url);

            var description = GetMatch(html, "descriptionContent\">(.*?)</div>");
            var reportedBy = GetMatch(html, "ReportedByLink.*?>(.*?)</a>");

            var reportedTimeString = GetMatch(html, "ReportedOnDateTime.*?title=\"(.*?)\"");
            DateTimeOffset reportedTime;
            DateTimeOffset.TryParse(
                reportedTimeString,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal,
                out reportedTime);

            var issue = new CodeplexIssue { DescriptionHtml = HtmlToMarkdown(description), ReportedBy = reportedBy, ReportedAt = reportedTime.ToUniversalTime() };
            
            for (int i = 0; ; i++)
            {
                var commentMatch = Regex.Match(html, @"CommentContainer" + i + "\">.*", RegexOptions.Multiline | RegexOptions.Singleline);
                if (!commentMatch.Success)
                {
                    break;
                }

                var commentHtml = commentMatch.Value;
                var author = GetMatch(commentHtml, "class=\"author\".*?>(.*?)</a>");

                var timeString = GetMatch(commentHtml, "class=\"smartDate\" title=\"(.*?)\"");
                DateTime time;
                DateTime.TryParse(
                    timeString,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
                    out time);

                var content = GetMatch(commentHtml, "markDownOutput \">(.*?)</div>");
                issue.Comments.Add(new CodeplexComment { BodyHtml = HtmlToMarkdown(content), Author = author, CreatedAt = time });
            }

            if (status == "Closed")
            {
                var closedTimeString = GetMatch(html, "ClosedOnDateTime.*?title=\"(.*?)\"");
                DateTimeOffset closedTime;
                if (DateTimeOffset.TryParse(closedTimeString, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal, out closedTime))
                {
                    issue.ClosedAt = closedTime;
                }
                
                var closedCommentMatch = Regex.Match(html, "<div id=\"ClosedDiv\".*", RegexOptions.Multiline | RegexOptions.Singleline);
                if (closedCommentMatch.Success)
                {
                    var closedCommentHtml = closedCommentMatch.Value;
                    var author = GetMatch(closedCommentHtml, "id=\"ClosedByLink\".*?>(.*?)</a>");

                    var timeString = GetMatch(closedCommentHtml, "class=\"smartDate\" title=\"(.*?)\"");
                    DateTime time;
                    DateTime.TryParse(
                        timeString,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
                        out time);

                    var content = GetMatch(closedCommentHtml, "markDownOutput \">(.*?)</div>");
                    content = HtmlToMarkdown(content);

                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        issue.Comments.Add(new CodeplexComment { BodyHtml = content, Author = author, CreatedAt = time });
                    }
                }
            }

            return issue;
        }

        private static string HtmlToMarkdown(string html)
        {
            var text = HttpUtility.HtmlDecode(html);
            text = text.Replace("<br>", "\r\n");
            return text.Trim();
        }

        private static async Task<IssueImport> StartIssueImport(NewIssueImport issue)
        {
            return await client.IssueImport.StartImport(gitHubOwner, gitHubRepository, issue);
        }

        private static async Task CreateComment(int number, string comment)
        {
            await client.Issue.Comment.Create(gitHubOwner, gitHubRepository, number, comment);
        }

        /// <summary>
        /// Gets the value of the first group by matching the specified string with the specified regular expression.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <param name="expression">Regular expression with one group.</param>
        /// <returns>The value of the first group.</returns>
        private static string GetMatch(string input, string expression)
        {
            var titleMatch = Regex.Match(input, expression, RegexOptions.Multiline | RegexOptions.Singleline);
            if (titleMatch.Groups.Count >= 2)
            {
                return titleMatch.Groups[1].Value;
            }

            return null;
        }

        /// <summary>
        /// Gets the value of the first group of the matches of the specified regular expression.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <param name="expression">Regular expression with a group that should be captured.</param>
        /// <returns>A sequence of values from the first group of the matches.</returns>
        private static IEnumerable<string> GetMatches(string input, string expression)
        {
            foreach (Match match in Regex.Matches(input, expression, RegexOptions.Multiline | RegexOptions.Singleline))
            {
                yield return match.Groups[1].Value;
            }
        }
    }
}
