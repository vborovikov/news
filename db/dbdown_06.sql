use News;
go

drop fulltext index on rss.Posts;
drop fulltext stoplist NewsStoplist;
drop fulltext catalog NewsCatalog;

exec sp_fulltext_database @action = 'disable';
