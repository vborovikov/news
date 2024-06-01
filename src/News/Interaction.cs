namespace News;

using Dodkin.Dispatch;

public record UpdateFeedCommand(Guid FeedId) : Command { }

public record UpdatePostCommand(Guid PostId) : Command { }

public record LocalizeFeedsCommand : Command { }