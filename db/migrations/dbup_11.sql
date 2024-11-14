begin transaction;

create table rss.Schedule
(
    Id int identity(1,1) not null,
    Command nvarchar(2000) not null,
    CommandLabel varchar(250) not null,
    DueTime datetimeoffset not null index IX_Schedule_DueTime,
    RetryCount smallint not null constraint DF_Schedule_RetryCount default 0,
    constraint PK_Schedule primary key (Id)
);

commit transaction;