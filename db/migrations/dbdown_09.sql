-- Disabling semantic statistics
-- v0.5.0-beta.34

alter fulltext index on rss.Posts
    alter column Content drop statistical_semantics;
alter fulltext index on rss.Posts
    alter column LocalContent drop statistical_semantics;
alter fulltext index on rss.Posts
    alter column SafeContent drop statistical_semantics;
