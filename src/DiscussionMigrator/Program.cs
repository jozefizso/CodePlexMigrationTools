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

    public static class Program
    {
        // CodePlex project
        private static string codePlexProject;

        // UserEcho API access token
        private static string userEchoAccessToken;

        // Default UserEcho forum settings
        private static string Forum = "General";
        private static string FeedbackType = "Questions";
        private static string Category = null;
        private static List<string> Tags = new List<string> { "CodePlex" };
        private static bool CleanForum = false;

        private static HttpClient httpClient;
        private static UserEcho.Client client;

        static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Missing arguments: [CodeplexProject] [UserEchoAccessToken]");
                return 1;
            }

            codePlexProject = args[0];
            userEchoAccessToken = args[1];

            httpClient = new HttpClient();
            client = new UserEcho.Client(userEchoAccessToken);

            var forums = GetArray("forums");
            ListNames("Forums:", forums);

            var forumObject = GetByName(forums, Forum);
            if (forumObject == null)
            {
                Console.WriteLine("Forum not found: " + Forum);
                return 2;
            }

            var forumId = (long)forumObject["id"];

            var feedbackTypes = GetArray("forums/{0}/types", forumId);
            ListNames("Feedback types:", feedbackTypes);

            var feedbackTypeObject = GetByName(feedbackTypes, FeedbackType);
            if (feedbackTypeObject == null)
            {
                Console.WriteLine("Feedback type not found: " + FeedbackType);
                return 3;
            }

            var feedbackTypeId = (long)feedbackTypeObject["id"];

            var categories = GetArray("forums/{0}/categories", forumId);
            ListNames("Categories:", categories);

            long categoryId = 0;
            if (Category != null)
            {
                var categoryObject = GetByName(categories, Category);
                if (categoryObject == null)
                {
                    Console.WriteLine("Category not found: " + Category);
                    return 4;
                }

                categoryId = (long)categoryObject["id"];
            }

            var tags = GetArray("forums/{0}/tags", forumId);
            ListNames("Tags:", tags);
            var tagIds = new List<long>();
            foreach (var tag in Tags)
            {
                var tabObject = GetByName(tags, tag);
                if (tabObject != null)
                {
                    tagIds.Add((long)tabObject["id"]);
                }
                else
                {
                    Console.WriteLine("Tag not found: " + tag);
                }
            }

            if (CleanForum)
            {
                var topics = GetArray("forums/{0}/topics", forumId);
                foreach (var topic in topics)
                {
                    var topicId = (long)topic["id"];
                    Delete(topicId, "forums/{0}/topics", forumId);
                }
            }

            Console.WriteLine("Migrating discussion from {0} to UserEcho:", codePlexProject);
            MigrateDiscussions(forumId, feedbackTypeId, categoryId, tagIds).Wait();

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

        private static JsonObject[] GetArray(string format, params object[] args)
        {
            var path = string.Format(format, args);
            var response = (JsonObject)client.Get(path);
            if (response.Failed() || !response.Keys.Contains("data"))
            {
                return null;
            }

            return ((JsonArray)response["data"]).Cast<JsonObject>().ToArray();
        }

        private static bool Delete(long id, string format, params object[] args)
        {
            //// http://feedback.userecho.com/topic/489503-delete-topic-by-the-api/
            var path = string.Format(format, args);
            var response = (JsonObject)client.Delete(path, new { id });
            return !response.Failed();
        }

        private static JsonObject CreateTopic(long forumId, long typeId, string title, string content, long categoryId = 0, IList<long> tagIds = null)
        {
            object topic;
            if (tagIds != null && tagIds.Count > 0)
            {
                topic = new
                {
                    header = title,
                    description = content,
                    type = typeId,
                    category = categoryId,
                    tags = tagIds.ToArray()
                };
            }
            else
            {
                topic = new
                {
                    header = title,
                    description = content,
                    type = typeId,
                    category = categoryId,
                };
            }

            var response = (JsonObject)client.Post("forums/" + forumId + "/topics", topic);
            if (response.Failed())
            {
                return null;
            }

            return (JsonObject)response["data"];
        }

        static async Task MigrateDiscussions(long forumId, long typeId, long categoryId, IList<long> tagIds)
        {
            var discussions = GetDiscussions().Reverse().ToArray();
            var existingTopics = GetArray("forums/{0}/topics", forumId);

            for (int i = 0; i < discussions.Length; i++)
            {
                var discussionId = discussions[i];
                var isAlreadyMigrated = existingTopics != null && existingTopics.Any(
                    x =>
                    {
                        var d = (string)x["description"];
                        return d.Contains("codeplex.com/discussions/" + discussionId);
                    });
                if (isAlreadyMigrated)
                {
                    Console.WriteLine("{0}/{1} is already migrated", i + 1, discussions.Length);
                    continue;
                }

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
                var codePlexIssueUrl = string.Format("http://{0}.codeplex.com/discussions/{1}", codePlexProject, discussion.Id);

                var description = new StringBuilder();
                description.AppendFormat("<div><strong>This discussion was imported from <a href=\"{0}\" target=\"_blank\">{1}</a></strong></div>", codePlexIssueUrl, "CodePlex");
                foreach (var comment in discussion.Comments)
                {
                    description.AppendLine("<hr/>");
                    description.AppendLine("<div>");
                    description.AppendFormat(CultureInfo.InvariantCulture, "<p><strong><a href=\"http://www.codeplex.com/site/users/view/{0}\" target=\"_blank\">{0}</a></strong> wrote at {1:yyyy-MM-dd HH:mm}:</p>", comment.Author, comment.Time);
                    description.Append(comment.Content);
                    description.AppendLine("</div>");
                }

                for (int retry = 1; retry <= 5; retry++)
                {
                    try
                    {
                        var topic = CreateTopic(
                            forumId,
                            typeId,
                            discussion.Title,
                            description.ToString(),
                            categoryId,
                            tagIds);
                        if (topic == null)
                        {
                            Console.WriteLine("  Could not create topic at userecho.com.");
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
                var url = string.Format("http://{0}.codeplex.com/discussions?size={1}&page={2}", codePlexProject, size, page);
                var html = httpClient.GetStringAsync(url).Result;
                foreach (var id in GetMatches(html, "<a href=\"http://" + codePlexProject + ".codeplex.com/discussions/(\\d+)\">"))
                {
                    yield return int.Parse(id);
                }
            }
        }

        private static int GetNumberOfDiscussions()
        {
            var url = string.Format("https://{0}.codeplex.com/discussions", codePlexProject);
            var html = httpClient.GetStringAsync(url).Result;
            return int.Parse(GetMatch(html, "Selected\">(\\d+)</span> discussions"));
        }

        private static async Task<CodePlexTopic> GetDiscussion(int id)
        {
            var url = string.Format("http://{0}.codeplex.com/discussions/{1}", codePlexProject, id);
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
