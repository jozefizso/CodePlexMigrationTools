using System.Diagnostics;
using System.IO;
using System.Web.UI.WebControls;
using CodeplexMigration.IssueMigrator.Codeplex;
using Newtonsoft.Json;

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
        private static string archivePath;

        // GitHub details
        private static string gitHubOrganization;
        private static string gitHubTeam;
        private static string gitHubAccessToken;

        private static GitHubClient client;

        public static async Task<int> Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Missing arguments: [CodePlexArchivePath] [GitHubOrganization] [GitHubTeam] [GitHubAccessToken]");
                return 1;
            }

            archivePath = args[0];

            gitHubOrganization = args[1];
            gitHubTeam = args[2];
            gitHubAccessToken = args[3];

            if (!CheckArchiveFolder(ref archivePath))
            {
                return 1;
            }

            var credentials = new Credentials(gitHubAccessToken);
            var connection = new Connection(new ProductHeaderValue("CodeplexIssueMigrator")) { Credentials = credentials };
            client = new GitHubClient(connection);
            Team team;

            try
            {
                var allTeams = await client.Organization.Team.GetAll(gitHubOrganization);
                team = allTeams.FirstOrDefault(t => t.Name == gitHubTeam);
            }
            catch (NotFoundException e)
            {
                Console.WriteLine("Target organization does not exist.");
                return 2;
            }

            if (team == null)
            {
                Console.WriteLine($"Cannot find team '{gitHubTeam}' in organization '{gitHubOrganization}'.");
                return 1;
            }

            Console.WriteLine($"Source: {archivePath}");
            Console.WriteLine($"Destination: {team.Url}");

            Console.WriteLine("Migrating discussions:");
            await MigrateIssuesFromCache(team.Id);

            Console.WriteLine("Completed successfully.");

            if (Debugger.IsAttached)
            {
                Console.ReadKey();
            }

            return 0;
        }

        static bool CheckArchiveFolder(ref string archivePath)
        {
            var discussionsJson = Path.Combine(archivePath, "discussions.json");

            if (!File.Exists(discussionsJson))
            {
                discussionsJson = Path.Combine(archivePath, "discussions", "discussions.json");

                if (File.Exists(discussionsJson))
                {
                    archivePath = Path.Combine(archivePath, "discussions");
                    return true;
                }

                Console.WriteLine($"Failed to find the 'discussions.json' file at path '{discussionsJson}'.");
                return false;
            }

            return true;
        }

        static async Task MigrateIssuesFromCache(int teamId)
        {
            var discussionsJsonFile = Path.Combine(archivePath, "discussions.json");
            var discussionsJson = File.ReadAllText(discussionsJsonFile);

            var all = JsonConvert.DeserializeObject<List<CodePlexDiscussionInfo>>(discussionsJson);
            foreach (var info in all)
            {
                Console.WriteLine($"  Title: {info.Title}");

                try
                {
                    var newDiscussion = new NewTeamDiscussion(info.Title, "(empty)");
                    var discussion = await client.Organization.TeamDiscussions.Create(teamId, newDiscussion);
                    Console.WriteLine($"  Discussion created. Number={discussion.Number}");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to create discussion. Exception={e.Message}");
                }
            }
        }
    }
}
