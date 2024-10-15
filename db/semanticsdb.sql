use master;
go

-- Attach SemanticsDb files after installing SemanticLanguageDatabase.msi
create database semanticsdb
    on (filename = 'D:\Path\To\SemanticsDb\semanticsDB.mdf')
    log on (filename = 'D:\Path\To\SemanticsDb\semanticsdb_log.ldf')
    for attach;
go

-- Enable Semantic Search
exec sp_fulltext_semantic_register_language_statistics_db @DbName = N'semanticsdb';  
go  