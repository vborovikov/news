use News;
go

-- v0.5.0

begin transaction;
go

alter table rss.Feeds drop
    column Scheduled;
go

commit transaction;
go