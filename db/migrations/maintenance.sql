use News;
go

begin transaction;
go

-- v0.4.13-beta.6
-- drop unused index
drop index if exists 
    IX_Posts_Link on rss.Posts;
go

commit transaction;
go