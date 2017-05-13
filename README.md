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

Migrates a CodePlex discussion forum to GitHub. 

```
DiscussionMigrator.exe [CodePlexProject] [GitHubOwner] [GitHubRepository] [GitHubPassword]
DiscussionMigrator.exe outlookgooglecalendarsync phw198 OutlookGoogleCalendarSync yourGitPassword
```

Note: 
- A "codeplex discussion" tag will be added to each issue (hard coded).
- Only unanswered discussions will be migrated.

### How to migrate a Mercurial repository from CodePlex to GitHub

Simply use the import repository functionality in GitHub.

Add `.gitattributes`, `.gitignore` and remember to [normalize the line endings](https://help.github.com/articles/dealing-with-line-endings)!
