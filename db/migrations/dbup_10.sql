-- v0.5.0-beta.35

begin transaction;

alter table rss.Feeds add
    Type char(3) null,
    Published datetimeoffset null;

commit transaction;
go