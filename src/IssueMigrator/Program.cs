using System.Threading;

namespace CodePlexIssueMigrator
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net.Http;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Web;

    using Octokit;

    public class Program
    {
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

            var credentials = new Credentials(gitHubAccessToken);
            var connection = new Connection(new ProductHeaderValue("CodeplexIssueMigrator")) { Credentials = credentials };
            client = new GitHubClient(connection);
            Console.WriteLine("Source: {0}.codeplex.com", codePlexProject);
            Console.WriteLine("Destination: github.com/{0}/{1}", gitHubOwner, gitHubRepository);
            Console.WriteLine("Migrating issues:");
            MigrateIssues().Wait();

            Console.WriteLine();
            Console.WriteLine("Completed successfully.");
            return 0;
        }

        static async Task MigrateIssues()
        {
            var issues = GetIssues();
            foreach (var issue in issues)
            {
                var codePlexIssueUrl = string.Format("https://{0}.codeplex.com/workitem/{1}", codePlexProject, issue.Id);
                var description = new StringBuilder();
                description.AppendFormat("**This issue was imported from [CodePlex]({0})**", codePlexIssueUrl);
                description.AppendLine();
                description.AppendLine();
                description.AppendFormat(CultureInfo.InvariantCulture, "**[{0}](https://www.codeplex.com/site/users/view/{0})** wrote {1:yyyy-MM-dd} at {1:HH:mm}\r\n", issue.ReportedBy, issue.Time);
                description.Append(issue.Description);
                foreach (var comment in issue.Comments)
                {
                    description.AppendLine();
                    description.AppendLine();
                    description.AppendFormat(CultureInfo.InvariantCulture, "**[{0}](https://www.codeplex.com/site/users/view/{0})** wrote {1:yyyy-MM-dd} at {1:HH:mm}\r\n", comment.Author, comment.Time);
                    description.Append(comment.Content);
                    // await CreateComment(gitHubIssue.Number, comment.Content);
                }

                var labels = new List<string>();

                switch (issue.Type)
                {
                    case "Feature":
                        labels.Add("enhancement");
                        break;
                    case "Issue":
                        labels.Add("bug");
                        break;
                }
                // if (issue.Impact == "Low" || issue.Impact == "Medium" || issue.Impact == "High")
                //    labels.Add(issue.Impact);
                var gitHubIssue = await CreateIssue(issue.Title, description.ToString().Trim(), labels);

                if (issue.IsClosed())
                {
                    await CloseIssue(gitHubIssue);
                }

                Thread.Sleep(_rnd.Next(800, 1500));
            }
        }

        static IEnumerable<CodePlexIssue> GetIssues(int size = 100)
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
                    var codeplexIssue = GetIssue(id).Result;
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

        static async Task<CodePlexIssue> GetIssue(int number)
        {
            var url = string.Format("https://{0}.codeplex.com/workitem/{1}", codePlexProject, number);
            var html = await httpClient.GetStringAsync(url);

            var description = GetMatch(html, "descriptionContent\">(.*?)</div>");
            var reportedBy = GetMatch(html, "ReportedByLink.*?>(.*?)</a>");

            var reportedTimeString = GetMatch(html, "ReportedOnDateTime.*?title=\"(.*?)\"");
            DateTime reportedTime;
            DateTime.TryParse(
                reportedTimeString,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
                out reportedTime);

            var issue = new CodePlexIssue { Description = HtmlToMarkdown(description), ReportedBy = reportedBy, Time = reportedTime };

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
                issue.Comments.Add(new CodeplexComment { Content = HtmlToMarkdown(content), Author = author, Time = time });
            }

            return issue;
        }

        private static string HtmlToMarkdown(string html)
        {
            var text = HttpUtility.HtmlDecode(html);
            text = text.Replace("<br>", "\r\n");
            return text.Trim();
        }

        private static async Task<Issue> CreateIssue(string title, string body, List<string> labels)
        {
            var issue = new NewIssue(title) { Body = body };
            issue.Labels.Add("CodePlex");
            foreach (var label in labels)
            {
                if (!string.IsNullOrEmpty(label))
                {
                    issue.Labels.Add(label);
                }
            }

            return await client.Issue.Create(gitHubOwner, gitHubRepository, issue);
        }

        private static async Task CreateComment(int number, string comment)
        {
            await client.Issue.Comment.Create(gitHubOwner, gitHubRepository, number, comment);
        }

        private static async Task CloseIssue(Issue issue)
        {
            var issueUpdate = new IssueUpdate { State = ItemState.Closed };
            foreach (var label in issue.Labels)
            {
                issueUpdate.Labels.Add(label.Name);
            }

            await client.Issue.Update(gitHubOwner, gitHubRepository, issue.Number, issueUpdate);
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
            return titleMatch.Groups[1].Value;
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
