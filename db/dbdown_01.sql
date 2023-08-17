use News;
go

begin transaction;
go

drop table rss.UserPosts;
go

drop table rss.UserFeeds;
go

drop table rss.Posts;
go

drop table rss.Feeds;
go

commit;
go

drop schema rss;
go