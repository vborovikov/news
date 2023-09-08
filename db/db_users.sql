use News;
go

-- IIS user

create user [IIS AppPool\news] for login [IIS APPPOOL\news];
go

-- default schema [rss]
alter user [IIS AppPool\news] with default_schema=[rss]
go

-- read/write roles
alter role [db_datareader] add member [IIS AppPool\news]
go
alter role [db_datawriter] add member [IIS AppPool\news]
go

-- [asp] operations
grant insert on schema::[asp] to [IIS AppPool\news]
go
grant select on schema::[asp] to [IIS AppPool\news]
go
grant update on schema::[asp] to [IIS AppPool\news]
go
grant delete on schema::[asp] to [IIS AppPool\news]
go
grant execute on schema::[asp] to [IIS AppPool\news]
go

-- [rss] operations
grant insert on schema::[rss] to [IIS AppPool\news]
go
grant select on schema::[rss] to [IIS AppPool\news]
go
grant update on schema::[rss] to [IIS AppPool\news]
go
grant delete on schema::[rss] to [IIS AppPool\news]
go
grant execute on schema::[rss] to [IIS AppPool\news]
go

-- [dbo] operations
grant insert on schema::[dbo] to [IIS AppPool\news]
go
grant select on schema::[dbo] to [IIS AppPool\news]
go
grant update on schema::[dbo] to [IIS AppPool\news]
go
grant delete on schema::[dbo] to [IIS AppPool\news]
go
grant execute on schema::[dbo] to [IIS AppPool\news]
go

-- SVC user

create user [NT AUTHORITY\LOCAL SERVICE] for login [NT AUTHORITY\LOCAL SERVICE];
go
alter user [NT AUTHORITY\LOCAL SERVICE] with default_schema=[rss];
go

-- read/write roles
alter role [db_datareader] add member [NT AUTHORITY\LOCAL SERVICE];
go
alter role [db_datawriter] add member [NT AUTHORITY\LOCAL SERVICE];
go

-- [rss] operations
grant insert on schema::[rss] to [NT AUTHORITY\LOCAL SERVICE];
go
grant select on schema::[rss] to [NT AUTHORITY\LOCAL SERVICE];
go
grant update on schema::[rss] to [NT AUTHORITY\LOCAL SERVICE];
go
grant delete on schema::[rss] to [NT AUTHORITY\LOCAL SERVICE];
go
grant execute on schema::[rss] to [NT AUTHORITY\LOCAL SERVICE];
go

-- [dbo] operations
grant insert on schema::[dbo] to [NT AUTHORITY\LOCAL SERVICE];
go
grant select on schema::[dbo] to [NT AUTHORITY\LOCAL SERVICE];
go
grant update on schema::[dbo] to [NT AUTHORITY\LOCAL SERVICE];
go
grant delete on schema::[dbo] to [NT AUTHORITY\LOCAL SERVICE];
go
grant execute on schema::[dbo] to [NT AUTHORITY\LOCAL SERVICE];
go
