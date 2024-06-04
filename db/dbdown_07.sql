use News;
go

-- v0.5.0-beta.22

begin transaction;
go

alter table rss.Feeds drop
    constraint CK_Feeds_TitlePath,
    constraint CK_Feeds_AuthorPath,
    constraint CK_Feeds_DescriptionPath,
    constraint CK_Feeds_ContentPath,
    column TitlePath,
    column AuthorPath,
    column DescriptionPath,
    column ContentPath;
go

commit transaction;
go
