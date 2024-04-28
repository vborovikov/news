use News;
go

-- v0.5.0

begin transaction;
go

alter table rss.Feeds add
    Scheduled datetimeoffset null; -- feed update next time
go

commit transaction;
go