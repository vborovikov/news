use News;
go

-- v0.4.13

begin transaction;
go

-- Column added to store post status

alter table rss.Posts add
    Status varchar(50) not null constraint DF_Posts_Status default 'OK';
go

commit transaction;
go