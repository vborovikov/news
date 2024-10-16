-- v0.5.0-beta.35

begin transaction;

alter table rss.Posts
    drop column Type,
    drop column Published;

commit transaction;
go