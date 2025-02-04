namespace News.Service;

public static class EventIds
{
    public static readonly EventId ServiceStarted = new(100, nameof(ServiceStarted));
    public static readonly EventId ServiceStopped = new(101, nameof(ServiceStopped));
    public static readonly EventId SchedulingStarted = new(102, nameof(SchedulingStarted));
    public static readonly EventId SchedulingStopped = new(103, nameof(SchedulingStopped));
    public static readonly EventId SchedulingFailed = new(104, nameof(SchedulingFailed));

    public static readonly EventId FeedUpdateInitiated = new(110, nameof(FeedUpdateInitiated));
    public static readonly EventId FeedUpdateCompleted = new(111, nameof(FeedUpdateCompleted));
    public static readonly EventId FeedUpdateSkipped = new(112, nameof(FeedUpdateSkipped));
    public static readonly EventId FeedUpdateFailed = new(113, nameof(FeedUpdateFailed));
    public static readonly EventId FeedUpdateScheduled = new(114, nameof(FeedUpdateScheduled));
    public static readonly EventId FeedUpdateNotScheduled = new(115, nameof(FeedUpdateNotScheduled));

    public static readonly EventId PostUpdateInitiated = new(120, nameof(PostUpdateInitiated));
    public static readonly EventId PostUpdateCompleted = new(121, nameof(PostUpdateCompleted));
    public static readonly EventId PostUpdateFailed = new(122, nameof(PostUpdateFailed));
}