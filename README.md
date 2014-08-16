A small suite of tools that should help migrating data from CodePlex.
Use at your own risk. The web scraping from the CodePlex site is probably very brittle, and may need to be updated.

### IssueMigrator

Migrates issues from a CodePlex project to GitHub.

Syntax: IssueMigrator.exe [CodePlexProject] [GitHubOwner] [GitHubRepository] [GitHubAccessToken] 
Example: IssueMigrator.exe OxyPlot oxyplot oxyplot c2bdeb09f9acb52c154fb21ab6a3239452ad615e

The GitHub repository should be created before executing the migration command.

### DiscussionMigrator

Migrates a CodePlex discussion forum to UserEcho. 

Syntax: `DiscussionMigrator.exe [CodePlexProject] [UserEchoAccessToken]`
Example: `DiscussionMigrator.exe OxyPlot cc0cb52b4645b90efe42e94d025dc21343bf0ac9`

Note: 
- you need an upgraded plan to get access to the API access token.
- the discussions will be added to the "General" forum (hard coded)
- the feedback type will be set to "Questions" (hard coded)
- a "CodePlex" tag will be added to each discussion (hard coded)

### How to migrate a Mercurial repository from CodePlex to GitHub

Simly use the import repository functionality in GitHub