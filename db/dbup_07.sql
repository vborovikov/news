use News;
go

-- v0.5.0-beta.22

begin transaction;
go

alter table rss.Feeds add
    TitlePath nvarchar(200) null constraint CK_Feeds_TitlePath check (TitlePath != N''),
    AuthorPath nvarchar(200) null constraint CK_Feeds_AuthorPath check (AuthorPath != N''),
    DescriptionPath nvarchar(200) null constraint CK_Feeds_DescriptionPath check (DescriptionPath != N''),
    ContentPath nvarchar(200) null constraint CK_Feeds_ContentPath check (ContentPath != N'');
go

commit transaction;
go
