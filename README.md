A collection of .NET tools that should help migrating data from CodePlex.
Use at your own risk. The web scraping from the CodePlex site is probably very brittle, and may need to be updated.

### IssueMigrator

Migrates issues from a CodePlex project to GitHub.

```
IssueMigrator.exe [CodePlexProject] [GitHubOwner] [GitHubRepository] [GitHubAccessToken]
IssueMigrator.exe oxyplot oxyplot oxyplot c2bdeb09f9acb52c154fb21ab6a3239452ad615e
```

Note:
- The GitHub repository should be created before executing the migration command.

### DiscussionMigrator

Migrates a CodePlex discussion forum to UserEcho. 

```
DiscussionMigrator.exe [CodePlexProject] [UserEchoAccessToken]
DiscussionMigrator.exe oxyplot cc0cb52b4645b90efe42e94d025dc21343bf0ac9
```

Note: 
- You need an upgraded plan to get access to the API access token.
- The discussions will be added to the "General" forum (hard coded)
- The feedback type will be set to "Questions" (hard coded)
- A "CodePlex" tag will be added to each discussion (hard coded). This tag must be added to the UserEcho forum.

### How to migrate a Mercurial repository from CodePlex to GitHub

Simly use the import repository functionality in GitHub.

Add `.gitattributes`, `.gitignore` and remember to [normalize the line endings](https://help.github.com/articles/dealing-with-line-endings)!
