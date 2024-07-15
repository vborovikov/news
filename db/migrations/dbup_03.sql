use News;
go

-- v0.4.12

begin transaction;
go

-- Columns added to store downloaded content and images (images are stored externally)

alter table rss.Posts add
	LocalDescription nvarchar(max) null,
    LocalContentSource nvarchar(max) null constraint CK_Posts_LocalContentSource check (LocalContentSource != N''),
    LocalContent nvarchar(max) null constraint CK_Posts_LocalContent check (LocalContent != N'');
go

alter view rss.AppPosts with schemabinding as
    select 
        up.UserId, p.FeedId, p.Id as PostId, up.IsRead, up.IsFavorite,
        p.Link, p.Slug, p.Published, p.Title, p.Author,
        coalesce(p.SafeDescription, p.LocalDescription, p.Description) as Description, 
        coalesce(p.SafeContent, p.LocalContent, p.Content) as Content
    from rss.Posts p
    left outer join rss.UserPosts up on up.PostId = p.Id;
go

commit transaction;
go