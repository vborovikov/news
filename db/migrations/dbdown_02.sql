use News;
go

begin transaction;
go

drop view rss.AppPosts;
go

drop view rss.AppFeeds;
go

drop table rss.UserPosts;
go

drop table rss.UserFeeds;
go

drop table rss.UserChannels;
go

drop table rss.Posts;
go

drop table rss.Feeds;
go

commit;
go

drop schema rss;
go
