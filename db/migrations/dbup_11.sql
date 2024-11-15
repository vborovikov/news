begin transaction;

create table rss.Schedule
(
    Id int identity(1,1) not null,
    Command nvarchar(846) not null, -- (1700-8)/2 where 1700 is the maximum length of the index key
    CommandLabel varchar(250) not null,
    DueTime datetimeoffset(0) not null, -- second-level accuracy, 8 bytes
    RetryCount smallint not null constraint DF_Schedule_RetryCount default 0,
    constraint PK_Schedule_Id primary key (Id),
    index IX_Schedule_Command_DueTime unique nonclustered (Command, DueTime), -- no command can be scheduled twice
    index IX_Schedule_DueTime nonclustered (DueTime asc) -- fast lookup by the oldest due-time
);

commit transaction;