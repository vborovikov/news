﻿namespace News.Service.Data;

using System;
using CodeHollow.FeedReader;
using CodeHollow.FeedReader.Feeds;

record DbFeed
{
    public Guid Id { get; init; }
    public string Source { get; init; }
}

record FeedItemWrapper
{
    private readonly FeedItem item;

    public FeedItemWrapper(FeedItem item)
    {
        this.item = item;
    }

    public string Id => this.item.Id ?? this.item.PublishingDateString;

    public string Link => this.item.Link ?? this.item.Id;

    public string Published => this.item.PublishingDateString ?? (this.item.SpecificItem as AtomFeedItem)?.UpdatedDateString!;

    public string Title =>
        string.IsNullOrWhiteSpace(this.item.Title) ? this.Link :
        this.item.Title.Length >= 1000 ? this.item.Title[..999] :
        this.item.Title;

    public string? Description => this.item.Content is not null ? this.item.Description : null;

    public string Author => this.item.Author;

    public string Content => this.item.Content ?? this.item.Description ?? this.Link;
}
