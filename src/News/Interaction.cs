namespace News;

using Dodkin.Dispatch;

public record UpdateFeedCommand(Guid FeedId) : Command { }

public record LocalizeFeedsCommand : Command { }