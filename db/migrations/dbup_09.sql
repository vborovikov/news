-- Enabling semantic statistics
-- v0.5.0-beta.34

alter fulltext index on rss.Posts
    alter column Content add statistical_semantics;
alter fulltext index on rss.Posts
    alter column LocalContent add statistical_semantics;
alter fulltext index on rss.Posts
    alter column SafeContent add statistical_semantics;
