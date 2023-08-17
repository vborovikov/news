use News;
go

-- create RSS schema

create schema rss authorization dbo;
go

-- create RSS tables

begin transaction;
go

create table rss.Feeds (
    Id uniqueidentifier not null primary key clustered,
    Title nvarchar(100) not null check (Title != N''),
    Description nvarchar(250) null,
    Source nvarchar(850) not null unique check (Source != N''),
    Link nvarchar(850) not null unique check (Link != N'')
);
go

create table rss.Posts (
    Id uniqueidentifier not null primary key,
    FeedId uniqueidentifier not null foreign key references rss.Feeds(Id) on delete cascade,
    Link nvarchar(850) not null unique check (Link != N'') index IX_Posts_Link unique nonclustered,
    Published datetimeoffset not null default sysdatetimeoffset(),
    Title nvarchar(100) not null check (Title != N''),
    Content nvarchar(max) not null check (Content != N''),
    index IXC_Posts (FeedId, Id) clustered
);
go

create table rss.UserFeeds (
    UserId uniqueidentifier not null foreign key references asp.Users(Id) on delete cascade,
    FeedId uniqueidentifier not null foreign key references rss.Feeds(Id) on delete cascade,
    Title nvarchar(100) null,
    constraint PK_UserFeeds primary key (UserId, FeedId),
    index IXC_UserFeeds (UserId, FeedId) clustered
);
go

create table rss.UserPosts (
    UserId uniqueidentifier not null foreign key references asp.Users(Id) on delete cascade,
    PostId uniqueidentifier not null foreign key references rss.Posts(Id) on delete cascade,
    IsRead bit not null default 0,
    IsFavorite bit not null default 0,
    constraint PK_UserPosts primary key (UserId, PostId),
    index IXC_UserPosts (UserId, PostId) clustered
);
go

commit;
go
