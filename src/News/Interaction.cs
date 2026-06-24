namespace News;

using Dodkin.Dispatch;

public record UpdateFeedCommand(Guid FeedId) : Command
{ 
    public bool CycleAccessMethods { get; init; }
}

public record UpdatePostCommand(Guid PostId) : Command { }

public record LocalizeFeedsCommand : Command { }

public record SlugifyFeedQuery(string FeedUrl) : Query<string>;