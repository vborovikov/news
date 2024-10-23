-- Create database --

use master;
if DB_ID('News') is not null 
begin
    alter database News set single_user with rollback immediate;
    drop database News;
end;

if @@Error = 3702
   RaisError('Cannot delete the database because of the open connections.', 127, 127) with nowait, log;

create database News collate Latin1_General_100_CI_AS_SC;
go

use News;
go

-- Run general migrations --
:r .\migrations\dbup_01.sql
:r .\migrations\dbup_02.sql
:r .\migrations\dbup_03.sql
:r .\migrations\dbup_04.sql
:r .\migrations\dbup_05.sql
:r .\migrations\dbup_06.sql
-- dbup_07.sql fulltext only
:r .\migrations\dbup_08.sql
-- dbup_09.sql fulltext only
:r .\migrations\dbup_10.sql