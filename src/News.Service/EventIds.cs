namespace News.Service;

public static class EventIds
{
    public static readonly EventId ServiceStarted = new(1, nameof(ServiceStarted));
    public static readonly EventId ServiceStopped = new(2, nameof(ServiceStopped));
    public static readonly EventId SchedulingStarted = new(3, nameof(SchedulingStarted));
    public static readonly EventId SchedulingStopped = new(4, nameof(SchedulingStopped));
    public static readonly EventId SchedulingFailed = new(5, nameof(SchedulingFailed));
    public static readonly EventId FeedUpdateStarted = new(10, nameof(FeedUpdateStarted));
    public static readonly EventId FeedUpdateCompleted = new(11, nameof(FeedUpdateCompleted));
    public static readonly EventId FeedUpdateSkipped = new(11, nameof(FeedUpdateSkipped));
    public static readonly EventId FeedUpdateFailed = new(12, nameof(FeedUpdateFailed));
    public static readonly EventId FeedUpdateScheduled = new(13, nameof(FeedUpdateScheduled));
    public static readonly EventId FeedUpdateNotScheduled = new(14, nameof(FeedUpdateNotScheduled));
}