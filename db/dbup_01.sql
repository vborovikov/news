use News;
go

-- RSS schema

create schema rss authorization dbo;
go

-- RSS tables

begin transaction;
go

create table rss.Feeds (
    Id uniqueidentifier not null primary key clustered default newid(),
    Title nvarchar(1000) not null check (Title != N''),
    Description nvarchar(2000) null,
    Source nvarchar(850) not null unique check (Source != N''),
    Link nvarchar(850) not null unique check (Link != N''),
    Updated datetimeoffset not null default sysdatetimeoffset(),
    Status varchar(50) not null default 'OK',
    Error nvarchar(2000) null
);
go

create table rss.Posts (
    Id uniqueidentifier not null primary key default newid(),
    FeedId uniqueidentifier not null foreign key references rss.Feeds(Id) on delete cascade,
    ExternalId varchar(850) not null check (ExternalId != N''),
    Link nvarchar(850) not null check (Link != N''),
    Slug varchar(100) not null check (Slug != '') index IX_Posts_Slug nonclustered,
    Published datetimeoffset not null default sysdatetimeoffset(),
    Title nvarchar(1000) not null check (Title != N''),
    Description nvarchar(max) null,
    Content nvarchar(max) not null check (Content != N''),
    Author nvarchar(100) null,
    index IXC_Posts clustered (FeedId, Id),
    index IX_Posts_ExternalId unique nonclustered (ExternalId, FeedId)
);
go

create table rss.UserChannels (
    Id uniqueidentifier not null primary key default newid(),
    UserId uniqueidentifier not null foreign key references asp.Users(Id) on delete cascade,
    Name nvarchar(100) not null check (Name != N''),
    Slug varchar(100) not null check (Slug != ''),
    index IXC_UserChannels clustered (UserId, Id),
    index IX_UserChannels_Name unique nonclustered (Name, UserId),
    index IX_UserChannels_Slug unique nonclustered (Slug, UserId)
);
go

create table rss.UserFeeds (
    UserId uniqueidentifier not null foreign key references asp.Users(Id),
    ChannelId uniqueidentifier not null foreign key references rss.UserChannels(Id) on delete cascade,
    FeedId uniqueidentifier not null foreign key references rss.Feeds(Id) on delete cascade,
    Slug varchar(100) not null check (Slug != ''),
    Title nvarchar(200) null,
    constraint PK_UserFeeds primary key (UserId, FeedId),
    index IXC_UserFeeds clustered (UserId, ChannelId, FeedId),
    index IX_UserFeeds_Slug unique nonclustered (Slug, UserId),
    index IX_UserFeeds_Title nonclustered (Title, UserId)
);
go

create table rss.UserPosts (
    UserId uniqueidentifier not null foreign key references asp.Users(Id) on delete cascade,
    FeedId uniqueidentifier not null foreign key references rss.Feeds(Id),
    PostId uniqueidentifier not null foreign key references rss.Posts(Id) on delete cascade,
    IsRead bit not null default 0,
    IsFavorite bit not null default 0,
    constraint PK_UserPosts primary key (UserId, PostId),
    index IXC_UserPosts clustered (UserId, FeedId, PostId)
);
go

create view rss.AppFeeds with schemabinding as
    select uf.UserId, uf.ChannelId, uf.FeedId, f.Source,
        isnull(uf.Title, f.Title) as Title, uf.Slug, 
        f.Description, f.Link, f.Updated, f.Error
    from rss.UserFeeds uf
    inner join rss.Feeds f on f.Id = uf.FeedId;
go

create view rss.AppPosts with schemabinding as
    select 
        up.UserId, p.FeedId, p.Id as PostId, up.IsRead, up.IsFavorite,
        p.Link, p.Slug, p.Published, p.Title, p.Description, p.Content, p.Author
    from rss.Posts p
    left outer join rss.UserPosts up on up.PostId = p.Id;
go

commit transaction;
go
