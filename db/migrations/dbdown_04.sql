use News;
go

-- v0.4.14

begin transaction;
go

alter table rss.Posts drop
    constraint DF_Posts_Status,
    --index IX_Posts_Status,
    column Status,
    column Error;
go

commit transaction;
go