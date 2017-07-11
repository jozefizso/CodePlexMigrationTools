namespace DiscussionMigrator
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Web;

    using RestSharp;
    using Octokit;

    public static class Program
    {
        // CodePlex project
        private static string codePlexProject;

        // UserEcho API access token
        private static string userEchoAccessToken;

        // GitHub owner (organization or user)
        private static string gitHubOwner;

        // GitHub repository
        private static string gitHubRepository;

        private static string gitHubPassword;

        // Default UserEcho forum settings
        private static string Forum = "General";
        private static string FeedbackType = "Questions";
        private static string Category = null;
        private static List<string> Tags = new List<string> { "CodePlex" };
        private static bool CleanForum = false;

        private static HttpClient httpClient;
        private static GitHubClient client;

        static int Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Missing arguments: [CodeplexProject] [GitHubOwner] [GitHubRepository] [GitHubPassword]");
                return 1;
            }

            codePlexProject = args[0];

            gitHubOwner = args[1];
            gitHubRepository = args[2];
            gitHubPassword = args[3];

            httpClient = new HttpClient();

            Console.WriteLine("Migrating discussion from {0} to GitHub:", codePlexProject);
            
            var credentials = new Credentials(gitHubOwner, gitHubPassword);
            var connection = new Connection(new ProductHeaderValue("CodeplexDiscussionMigrator")) { Credentials = credentials };
            client = new GitHubClient(connection);

            MigrateDiscussions().Wait();

            Console.WriteLine();
            Console.WriteLine("Completed successfully.");

            return 0;
        }

        private static void ListNames(string title, JsonObject[] items)
        {
            if (items == null)
            {
                return;
            }

            Console.WriteLine(title);
            foreach (var item in items)
            {
                Console.WriteLine(item["name"]);
            }

            Console.WriteLine();
        }

        private static JsonObject GetByName(IEnumerable<JsonObject> types, string name)
        {
            return types != null ? types.FirstOrDefault(t => string.Equals(t["name"], name)) : null;
        }

        private static bool Failed(this JsonObject response)
        {
            return (string)response["status"] != "success";
        }


        private static Issue CreateIssue(string title, string body, List<string> labels) {
            var issue = new NewIssue(title) { Body = body };
            issue.Labels.Add("codeplex discussion");
            foreach (var label in labels) {
                if (!string.IsNullOrEmpty(label)) {
                    issue.Labels.Add(label);
                }
            }

            try {
                Random rnd = new System.Random();
                System.Threading.Thread.Sleep(rnd.Next(2, 10) * 1000);
                Issue newIssue = client.Issue.Create(gitHubOwner, gitHubRepository, issue).Result;
                return newIssue;
            } catch (System.Exception ex) {
                Console.WriteLine("ERROR: "+ ex.Message);
                return null;
            }
        }

        static async Task MigrateDiscussions()
        {
            var discussions = GetDiscussions().Reverse().ToArray();
            
            for (int i = 0; i < discussions.Length; i++)
            {
                var discussionId = discussions[i];

                CodePlexTopic discussion = null;
                for (int retry = 1; retry <= 5; retry++)
                {
                    try
                    {
                        discussion = await GetDiscussion(discussionId);
                        break;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                }

                if (discussion == null)
                {
                    Console.WriteLine("Could not get discussion from CodePlex, aborting migration.");
                    break;
                }

                Console.WriteLine("{0}/{1} #{2} '{3}'", i + 1, discussions.Length, discussion.Id, discussion.Title);
                var codePlexIssueUrl = string.Format("https://{0}.codeplex.com/discussions/{1}", codePlexProject, discussion.Id);

                var description = new StringBuilder();
                description.AppendFormat("<div><strong>This discussion was imported from <a href=\"{0}\" target=\"_blank\">{1}</a></strong></div>", codePlexIssueUrl, "CodePlex");
                foreach (var comment in discussion.Comments)
                {
                    description.AppendLine("<hr/>");
                    description.AppendLine("<div>");
                    description.AppendFormat(CultureInfo.InvariantCulture, "<p><strong><a href=\"https://www.codeplex.com/site/users/view/{0}\" target=\"_blank\">{0}</a></strong> wrote at {1:yyyy-MM-dd HH:mm}:</p>", comment.Author, comment.Time);
                    description.Append(comment.Content);
                    description.AppendLine("</div>");
                }

                for (int retry = 1; retry <= 5; retry++)
                {
                    try
                    {
                        var topic = CreateIssue(discussion.Title, description.ToString(), new List<string>()); // {"CodePlex Discussion"});
                        if (topic == null)
                        {
                            Console.WriteLine("  Could not create topic at GitHub.");
                            continue;
                        }

                        break;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("  " + e.Message);
                    }

                    Console.WriteLine("Retry {0}", retry + 1);
                }
            }
        }

        private static IEnumerable<int> GetDiscussions(int size = 100)
        {
            // Find the number of discussions
            var numberOfDiscussions = GetNumberOfDiscussions();

            // Calculate number of pages
            var pages = (int)Math.Ceiling((double)numberOfDiscussions / size);

            for (int page = 0; page < pages; page++)
            {
                var url = string.Format("https://{0}.codeplex.com/discussions?showUnansweredThreadsOnly=true&size={1}&page={2}", codePlexProject, size, page);
                var html = httpClient.GetStringAsync(url).Result;
                foreach (var id in GetMatches(html, "<a href=\"https://" + codePlexProject + ".codeplex.com/discussions/(\\d+)\">"))
                {
                    yield return int.Parse(id);
                }
            }
        }

        private static int GetNumberOfDiscussions()
        {
            var url = string.Format("https://{0}.codeplex.com/discussions?showUnansweredThreadsOnly=true", codePlexProject);
            var html = httpClient.GetStringAsync(url).Result;
            return int.Parse(GetMatch(html, "Selected\">(\\d+)</span> discussions"));
        }

        private static async Task<CodePlexTopic> GetDiscussion(int id)
        {
            var url = string.Format("https://{0}.codeplex.com/discussions/{1}", codePlexProject, id);
            var html = await httpClient.GetStringAsync(url);
            var title = GetMatch(html, "<h1 class=\"page_title WordWrapBreakWord\">(.*?)</h1>").Trim();
            var topic = new CodePlexTopic { Id = id, Title = DecodeHtml(title) };

            foreach (var postHtml in GetMatches(html, "<tr id=\"PostPanel\"(.*?)</tr>"))
            {
                var user = GetMatch(postHtml, "UserProfileLink.*?>(.*?)<");
                var content = GetMatch(postHtml, "<td id=\"PostContent.*?>(.*?)</td").Trim();
                var timeString = GetMatch(postHtml, "class=\"smartDate\" title=\"(.*?)\"");
                DateTime time;
                DateTime.TryParse(
                    timeString,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
                    out time);

                topic.Comments.Add(new CodeplexComment { Author = user, Content = content, Time = time });
            }

            return topic;
        }

        private static string DecodeHtml(string html)
        {
            var text = HttpUtility.HtmlDecode(html);
            return text.Trim();
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
