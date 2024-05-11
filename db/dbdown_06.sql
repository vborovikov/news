use News;
go

-- v0.5.0-beta.15

drop fulltext index on rss.Posts;
drop fulltext stoplist NewsStoplist;
drop fulltext catalog NewsCatalog;

exec sp_fulltext_database @action = 'disable';
