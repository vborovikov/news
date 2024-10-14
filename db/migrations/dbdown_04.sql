use News;
go

-- v0.4.12

begin transaction;
go

alter view rss.AppPosts with schemabinding as
    select 
        up.UserId, p.FeedId, p.Id as PostId, up.IsRead, up.IsFavorite,
        p.Link, p.Slug, p.Published, p.Title, p.Author, 
        isnull(p.SafeDescription, p.Description) as Description, 
        isnull(p.SafeContent, p.Content) as Content
    from rss.Posts p
    left outer join rss.UserPosts up on up.PostId = p.Id;
go

alter table rss.Posts drop
    constraint CK_Posts_LocalContent,
    constraint CK_Posts_LocalContentSource,
    column LocalDescription,
    column LocalContent,
    column LocalContentSource;
go

commit transaction;
go