namespace News.Service.Data;

using System;

record DbFeed
{
    public Guid Id { get; init; }
    public string Source { get; init; }
}

record DbPost
{
    public Guid Id { get; init; }
    public string ExternalId { get; init; }
    public DateTimeOffset Published { get; init; }
}
