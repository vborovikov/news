-- Full-text search --
-- en-US 0x0409 --
-- it-IT 0x0410 --
-- ru-RU 0x0419 --

use News;
go

-- full-text search for the database
exec sp_fulltext_database @action = 'enable';

-- full-text catalog and indexes
create fulltext catalog NewsCatalog;
create fulltext stoplist NewsStoplist from system stoplist;

create fulltext index on rss.Posts (
    Title language N'English',
    Content language N'English',
    SafeContent language N'English',
    LocalContent language N'English')
key index PK_Posts_Id
on NewsCatalog with (change_tracking = auto, stoplist = NewsStoplist);