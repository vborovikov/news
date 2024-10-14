use master;
if DB_ID('News') is not null 
begin
    alter database News set single_user with rollback immediate;
    drop database News;
end;

if @@Error = 3702
   RaisError('Cannot delete the database because of the open connections.', 127, 127) with nowait, log;
