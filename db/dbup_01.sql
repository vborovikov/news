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
    Link nvarchar(850) not null unique check (Link != N''),
    Updated datetimeoffset not null default sysdatetimeoffset(),
);
go

create table rss.Posts (
    Id uniqueidentifier not null primary key,
    FeedId uniqueidentifier not null foreign key references rss.Feeds(Id) on delete cascade,
    Link nvarchar(850) not null check (Link != N'') index IX_Posts_Link unique nonclustered,
    Published datetimeoffset not null default sysdatetimeoffset(),
    Title nvarchar(100) not null check (Title != N''),
    Content nvarchar(max) not null check (Content != N''),
    index IXC_Posts clustered (FeedId, Id)
);
go

create table rss.Channels (
    Id uniqueidentifier not null primary key,
    Name nvarchar(100) not null check (Name != N'') index IX_Channels_Name nonclustered,
    Slug varchar(100) not null check (Slug != '') index IXC_Channels_Slug clustered
);
go

create table rss.UserChannels (
    UserId uniqueidentifier not null foreign key references asp.Users(Id) on delete cascade,
    ChannelId uniqueidentifier not null foreign key references rss.Channels(Id) on delete cascade,
    Name nvarchar(100) not null check (Name != N''),
    Slug varchar(100) not null check (Slug != ''),
    constraint PK_UserChannels primary key (UserId, ChannelId),
    index IXC_UserChannels clustered (UserId, ChannelId),
    index IX_UserChannels_Name unique nonclustered (Name, UserId),
    index IX_UserChannels_Slug unique nonclustered (Slug, UserId)
);
go

create table rss.UserFeeds (
    UserId uniqueidentifier not null foreign key references asp.Users(Id) on delete cascade,
    FeedId uniqueidentifier not null foreign key references rss.Feeds(Id) on delete cascade,
    ChannelId uniqueidentifier not null foreign key references rss.Channels(Id) on delete cascade,
    Slug varchar(100) not null check (Slug != ''),
    Title nvarchar(100) null,
    constraint PK_UserFeeds primary key (UserId, FeedId),
    index IXC_UserFeeds clustered (UserId, FeedId),
    index IX_UserFeeds_Slug unique nonclustered (Slug, UserId),
    index IX_UserFeeds_Title unique nonclustered (Title, UserId)
);
go

create table rss.UserPosts (
    UserId uniqueidentifier not null foreign key references asp.Users(Id) on delete cascade,
    PostId uniqueidentifier not null foreign key references rss.Posts(Id) on delete cascade,
    IsRead bit not null default 0,
    IsFavorite bit not null default 0,
    constraint PK_UserPosts primary key (UserId, PostId),
    index IXC_UserPosts clustered (UserId, PostId)
);
go

commit;
go
