# CodePlex Archive Migration

> A collection of .NET tools that should help migrating data from CodePlex Archive.


## IssueMigrator

Migrates issues from an archive of a CodePlex project to GitHub.

```bat
IssueMigrator.exe [CodePlexArchivePath] [GitHubOwner] [GitHubRepository] [GitHubAccessToken]
IssueMigrator.exe "C:\dev\oxyplot" oxyplot oxyplot c2bdeb09f9acb52c154fb21ab6a3239452ad615e
```

**Note:** The GitHub repository should be created before executing the migration command.


## DiscussionMigrator

Migrates discussions from an archive of a CodePlex project to GitHub.

```bat
DiscussionMigrator.exe [CodePlexArchivePath] [GitHubOwner] [GitHubRepository] [GitHubPassword]
DiscussionMigrator.exe "C:\dev\outlookgooglecalendarsync" phw198 OutlookGoogleCalendarSync yourGitPassword
```

**Note:**

- A `codeplex discussion` tag will be added to each issue (hard coded).
- Only unanswered discussions will be migrated.


## License

[MIT](LICENSE)

Copyright © 2014 Oystein Bjorke
Copyright © 2017-2018 Jozef Izso
