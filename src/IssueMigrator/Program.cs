using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Serialization;
using CodeplexMigration.IssueMigrator.Codeplex;
using CodeplexMigration.IssueMigrator.Templates;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Octokit;
using Formatting = System.Xml.Formatting;

namespace CodeplexMigration.IssueMigrator
{
    public class Program
    {
        private const string GitHubAvatar_CodeplexAvatar_User = "https://avatars.githubusercontent.com/u/30236365?s=192";
        private const string Codeplex_ListIssuesTemplate = "https://{0}.codeplex.com/project/api/issues?start={1}&showClosed={2}";
        private const string Codeplex_IssueDetailsTemplate = "https://{0}.codeplex.com/project/api/issues/{1}";

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

        public static async Task<int> Main(string[] args)
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
            //await CacheIssues(codePlexProject);

            var credentials = new Credentials(gitHubAccessToken);
            var connection = new Connection(new ProductHeaderValue("CodeplexIssueMigrator")) { Credentials = credentials };
            client = new GitHubClient(connection);

            try
            {
                await ResetTestRepository(client);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return 1;
            }

            Console.WriteLine("Source: {0}.codeplex.com", codePlexProject);
            Console.WriteLine("Destination: github.com/{0}/{1}", gitHubOwner, gitHubRepository);
            Console.WriteLine("Migrating issues:");
            //await MigrateIssues();
            await MigrateIssuesFromCache();

            Console.WriteLine();
            Console.WriteLine("Completed successfully.");

            if (Debugger.IsAttached)
            {
                Console.ReadKey();
            }

            return 0;
        }

        private static async Task ResetTestRepository(GitHubClient client)
        {
            var repo = await client.Repository.Get(gitHubOwner, gitHubRepository);
            if (repo != null)
            {
                await client.Repository.Delete(repo.Id);
            }

            var newRepo = new NewRepository(gitHubRepository);
            var repo2 = await client.Repository.Create(gitHubOwner, newRepo);

            Console.WriteLine($"Repository {gitHubOwner}/{gitHubRepository} was reset. New id={repo2.Id}");
        }

        static async Task CacheIssues(string codeplexProject)
        {
            if (!Directory.Exists(".cache"))
            {
                Directory.CreateDirectory(".cache");
            }

            var issues = await GetIssues();
            foreach (var issue in issues)
            {
                var url = string.Format(Codeplex_IssueDetailsTemplate, codeplexProject, issue.Id);
                var json = await httpClient.GetStringAsync(url);
                var filename = $@".cache\{codeplexProject}_{issue.Id}.json";

                File.WriteAllText(filename, json, Encoding.UTF8);
                Console.WriteLine($"  Issue {issue.Id} cached to path '{filename}'.");
            }
        }

        static async Task MigrateIssuesFromCache()
        {
            var issues = GetIssuesFromCache();
            await MigrateIssues(issues);
        }

        static async Task MigrateIssues(IEnumerable<CodeplexIssueDetails> details)
        {
            foreach (var issueDetails in details)
            {
                var issue = issueDetails.Issue;
                var import = new NewIssueImport(issue.Title);

                var issueTemplate = new IssueTemplate();

                var codePlexIssueUrl = $"https://{codePlexProject}.codeplex.com/workitem/{issue.Id}";

                issueTemplate.CodeplexAvatar = GitHubAvatar_CodeplexAvatar_User;
                issueTemplate.OriginalUrl = codePlexIssueUrl;
                issueTemplate.OriginalUserName = issue.ReportedBy;
                issueTemplate.OriginalUserUrl = $"https://www.codeplex.com/site/users/view/{issue.ReportedBy}";
                issueTemplate.OriginalDate = issue.ReportedAt.ToString("R");
                issueTemplate.OriginalDateUtc = issue.ReportedAt.ToString("s");
                issueTemplate.OriginalBody = HtmlToMarkdown(issue.DescriptionHtml);

                var issueBody = issueTemplate.Format();
                import.Issue.Body = issueBody.Trim();

                foreach (var comment in issueDetails.Comments)
                {
                    var commentTemplate = new CommentTemplate();
                    commentTemplate.UserAvatar = $"https://github.com/identicons/{comment.Author}.png";
                    commentTemplate.OriginalUserName = comment.Author;
                    commentTemplate.OriginalUserUrl = $"https://www.codeplex.com/site/users/view/{comment.Author}";
                    commentTemplate.OriginalDate = comment.CreatedAt.ToString("R");
                    commentTemplate.OriginalDateUtc = comment.CreatedAt.ToString("s");
                    commentTemplate.OriginalBody = HtmlToMarkdown(comment.BodyHtml);

                    var commentBody = commentTemplate.Format();
                    var newComment = new NewIssueImportComment(commentBody);
                    newComment.CreatedAt = comment.CreatedAt;
                    import.Comments.Add(newComment);
                }

                import.Issue.Labels.Add("CodePlex");
                switch (issue.Type?.Name)
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

                import.Issue.CreatedAt = issue.ReportedAt;
                if (issue.ClosedAt.HasValue)
                {
                    import.Issue.ClosedAt = issue.ClosedAt.Value;
                    import.Issue.Closed = true;
                }

                var gitHubIssue = await StartIssueImport(import);
                Console.WriteLine($"  Issue {issue.Id} is imported with task '{gitHubIssue.Id}': {gitHubIssue.Url}");
            }
        }

        static IEnumerable<CodeplexIssueDetails> GetIssuesFromCache()
        {
            var files = Directory.EnumerateFiles(".cache", "*.json").ToList();
            var issues = new List<CodeplexIssueDetails>(files.Count);

            foreach (var file in files)
            {
                var json = File.ReadAllText(file);
                try
                {
                    var workitem = JsonConvert.DeserializeObject<CodeplexIssueDetails>(json);
                    issues.Add(workitem);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }

            return issues;
        }

        static async Task<IEnumerable<CodeplexIssueInfo>> GetIssues()
        {
            // Find the number of items
            var url = string.Format(Codeplex_ListIssuesTemplate, codePlexProject, 0, true);
            var issueList = await DownloadIssueList(url);
            var numberOfItems = issueList.TotalItems;

            Console.WriteLine("Found {0} items", numberOfItems);

            var issues = new List<CodeplexIssueInfo>(numberOfItems);
            issues.AddRange(issueList.Issues);

            var retrieviedItems = issueList.Issues.Count;
            while (retrieviedItems < numberOfItems)
            {
                url = string.Format(Codeplex_ListIssuesTemplate, codePlexProject, retrieviedItems, true);
                issueList = await DownloadIssueList(url);
                retrieviedItems += issueList.Issues.Count;

                issues.AddRange(issueList.Issues);
            }

            return issues;
        }

        private static async Task<CodeplexIssueList> DownloadIssueList(string url)
        {
            string json = await httpClient.GetStringAsync(url);
            return JsonConvert.DeserializeObject<CodeplexIssueList>(json);
        }

        private static string HtmlToMarkdown(string html)
        {
            var text = HttpUtility.HtmlDecode(html);
            if (text == null)
            {
                return "";
            }

            text = text.Replace("<br>", "");
            text = text.Replace("<br/>", "");
            text = text.Replace("<br />", "");
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
