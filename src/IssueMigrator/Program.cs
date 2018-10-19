using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CodeplexMigration.IssueMigrator.Codeplex;
using CodeplexMigration.IssueMigrator.Templates;
using Newtonsoft.Json;
using Octokit;

namespace CodeplexMigration.IssueMigrator
{
    public class Program
    {
        // link to a profile with CodePlex logo set as an avatar
        private const string GitHubAvatar_CodeplexAvatar_User = "https://avatars.githubusercontent.com/u/30236365?s=96";
        private const string GitHubAvatar_CodeplexGuestAvatar_User = "https://avatars.githubusercontent.com/u/34607183?s=96";

        // CodePlex project
        private static string archivePath;

        // GitHub owner (organization or user)
        private static string gitHubOwner;

        // GitHub repository
        private static string gitHubRepository;

        // GitHub API access token
        private static string gitHubAccessToken;

        private static GitHubClient client;

        public static async Task<int> Main(string[] args)
        {
            if (args.Length < 4)
            {
                Console.WriteLine("Missing arguments: [CodePlexArchivePath] [GitHubOwner] [GitHubRepository] [GitHubAccessToken]");
                return 1;
            }

            archivePath = args[0];
            gitHubOwner = args[1];
            gitHubRepository = args[2];
            gitHubAccessToken = args[3];

            if (!CheckArchiveFolder(ref archivePath))
            {
                return 1;
            }

            Console.WriteLine("Source: {0}", archivePath);
            Console.WriteLine("Destination: github.com/{0}/{1}", gitHubOwner, gitHubRepository);

            var credentials = new Credentials(gitHubAccessToken);
            var connection = new Connection(new ProductHeaderValue("CodeplexIssueMigrator")) { Credentials = credentials };
            client = new GitHubClient(connection);

            try
            {
                var repository = await client.Repository.Get(gitHubOwner, gitHubRepository);
            }
            catch (NotFoundException e)
            {
                Console.WriteLine("Target repository does not exists. Will create new.");
                var newRepo = new NewRepository(gitHubRepository);
                await client.Repository.Create(gitHubOwner, newRepo);
                Console.WriteLine("Created new GitHub repository at: github.com/{0}/{1}", gitHubOwner, gitHubRepository);
            }

            Console.WriteLine("Migrating issues:");
            await MigrateIssuesFromCache();

            Console.WriteLine();
            Console.WriteLine("Completed successfully.");

            if (Debugger.IsAttached)
            {
                Console.ReadKey();
            }

            return 0;
        }

        static bool CheckArchiveFolder(ref string archivePath)
        {
            var issuesJson = Path.Combine(archivePath, "issues.json");

            if (!File.Exists(issuesJson))
            {
                issuesJson = Path.Combine(archivePath, "issues", "issues.json");

                if (File.Exists(issuesJson))
                {
                    archivePath = Path.Combine(archivePath, "issues");
                    return true;
                }

                Console.WriteLine($"Failed to find the 'issues.json' file at path '{issuesJson}'.");
                return false;
            }

            return true;
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

                var codePlexIssueUrl = $"https://{issue.ProjectName}.codeplex.com/workitem/{issue.Id}";

                issueTemplate.CodeplexAvatar = GitHubAvatar_CodeplexAvatar_User;
                issueTemplate.OriginalUrl = codePlexIssueUrl;
                //issueTemplate.OriginalUserName = issue.ReportedBy;
                //issueTemplate.OriginalUserUrl = $"https://www.codeplex.com/site/users/view/{issue.ReportedBy}";
                issueTemplate.OriginalDate = issue.ReportedAt.ToString("R");
                issueTemplate.OriginalDateUtc = issue.ReportedAt.ToString("s");
                issueTemplate.OriginalBody = issue.Description;

                var issueBody = issueTemplate.Format();
                import.Issue.Body = issueBody.Trim();

                foreach (var comment in issueDetails.Comments)
                {
                    if (String.IsNullOrWhiteSpace(comment.BodyHtml))
                    {
                        continue;
                    }

                    var commentTemplate = new CommentTemplate();
                    commentTemplate.UserAvatar = GitHubAvatar_CodeplexGuestAvatar_User;
                    commentTemplate.OriginalDate = comment.CreatedAt.ToString("R");
                    commentTemplate.OriginalDateUtc = comment.CreatedAt.ToString("s");
                    commentTemplate.OriginalBody = comment.BodyHtml;

                    var commentBody = commentTemplate.Format();
                    var newComment = new NewIssueImportComment(commentBody);
                    newComment.CreatedAt = comment.CreatedAt.UtcDateTime;
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

                import.Issue.CreatedAt = issue.ReportedAt.UtcDateTime;
                if (issue.ClosedAt.HasValue)
                {
                    if (!String.IsNullOrEmpty(issue.ClosedComment))
                    {
                        var commentTemplate = new CloseCommentTemplate();
                        commentTemplate.UserAvatar = $"https://avatars.githubusercontent.com/u/34607183?s=96";
                        commentTemplate.OriginalDate = issue.ClosedAt.Value.ToString("R");
                        commentTemplate.OriginalDateUtc = issue.ClosedAt.Value.ToString("s");
                        commentTemplate.OriginalBody = issue.ClosedComment;

                        var closeCommentBody = commentTemplate.Format();
                        var closeComment = new NewIssueImportComment(closeCommentBody);
                        closeComment.CreatedAt = issue.ClosedAt.Value.UtcDateTime;
                        import.Comments.Add(closeComment);
                    }

                    import.Issue.ClosedAt = issue.ClosedAt.Value.UtcDateTime;
                    import.Issue.Closed = true;
                }

                var gitHubIssue = await StartIssueImport(import);
                Console.WriteLine($"  Issue {issue.Id} is imported with task '{gitHubIssue.Id}': {gitHubIssue.Url}");
            }
        }

        static IEnumerable<CodeplexIssueDetails> GetIssuesFromCache()
        {
            var issuesJsonFile = Path.Combine(archivePath, "issues.json");
            var issuesJson = File.ReadAllText(issuesJsonFile);

            var allIssues = JsonConvert.DeserializeObject<List<CodeplexIssueInfo>>(issuesJson);
            var cache = new List<CodeplexIssueDetails>(allIssues.Count);
            foreach (var issueInfo in allIssues)
            {
                var issueIdString = issueInfo.Id.ToString(CultureInfo.InvariantCulture);
                var sourceFile = Path.Combine(archivePath, issueIdString, issueIdString + ".json");

                try
                {
                    var json = File.ReadAllText(sourceFile);
                    var workitem = JsonConvert.DeserializeObject<CodeplexIssueDetails>(json);

                    cache.Add(workitem);
                }
                catch (JsonException e)
                {
                    Console.WriteLine($"Failed to convert JSON data in file '{sourceFile}'. Exception={e.Message}");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to read file '{sourceFile}'. Exception={e.Message}");
                }
            }

            return cache;
        }

        private static async Task<IssueImport> StartIssueImport(NewIssueImport issue)
        {
            return await client.IssueImport.StartImport(gitHubOwner, gitHubRepository, issue);
        }
    }
}
