-- Create database --

use master;
if DB_ID('News') is not null 
begin
    alter database News set single_user with rollback immediate;
    drop database News;
end;

if @@Error = 3702
   RaisError('Cannot delete the database because of the open connections.', 127, 127) with nowait, log;

create database News collate Latin1_General_100_CI_AS;
go

use News;
go

-- Create schemas --

create schema asp authorization dbo;
go
create schema rss authorization dbo;
go


-- Create tables

begin transaction;
go

set quoted_identifier on;
go

create table [asp].[Roles] (
    [Id] uniqueidentifier not null primary key clustered,
    [Name] varchar(128) not null,
    [NormalizedName] varchar(128) not null index [UX_Roles_NormalizedName] unique,
    [ConcurrencyStamp] varchar(128) not null
);
go

create table [asp].[Users] (
    [Id] uniqueidentifier not null primary key clustered,
    [UserName] nvarchar(256) not null,
    [NormalizedUserName] nvarchar(256) not null index [UX_Users_NormalizedUserName] unique,
    [Email] varchar(256) not null,
    [NormalizedEmail] varchar(256) not null index [IX_Users_NormalizedEmail] unique,
    [EmailConfirmed] bit not null,
    [PasswordHash] varchar(128) not null,
    [SecurityStamp] varchar(128) not null,
    [ConcurrencyStamp] varchar(128) not null,
    [PhoneNumber] nvarchar(128) null,
    [PhoneNumberConfirmed] bit not null,
    [TwoFactorEnabled] bit not null,
    [LockoutEnd] datetimeoffset null,
    [LockoutEnabled] bit not null,
    [AccessFailedCount] int not null
);
go

create table [asp].[RoleClaims] (
    [Id] int not null identity primary key,
    [RoleId] uniqueidentifier not null foreign key references [asp].[Roles] ([Id]) on delete cascade,
    [ClaimType] varchar(128) null,
    [ClaimValue] varchar(128) null,
    index [IXC_RoleClaims] clustered ([RoleId], [Id])
);
go

create table [asp].[UserClaims] (
    [Id] int not null identity primary key,
    [UserId] uniqueidentifier not null foreign key references [asp].[Users] ([Id]) on delete cascade,
    [ClaimType] varchar(128) null,
    [ClaimValue] varchar(128) null,
    index [IXC_UserClaims] clustered ([UserId], [Id])
);
go

create table [asp].[UserLogins] (
    [LoginProvider] varchar(128) not null,
    [ProviderKey] varchar(128) not null,
    [ProviderDisplayName] nvarchar(128) null,
    [UserId] uniqueidentifier not null foreign key references [asp].[Users] ([Id]) on delete cascade,
    constraint [PK_UserLogins] primary key ([LoginProvider], [ProviderKey]),
    index [IXC_UserLogins] clustered ([UserId], [LoginProvider], [ProviderKey])
);
go

create table [asp].[UserRoles] (
    [UserId] uniqueidentifier not null foreign key references [asp].[Users] ([Id]) on delete cascade,
    [RoleId] uniqueidentifier not null foreign key references [asp].[Roles] ([Id]) on delete cascade,
    constraint [PK_UserRoles] primary key ([UserId], [RoleId]),
    index [IXC_UserRoles] clustered ([UserId], [RoleId])
);
go

create table [asp].[UserTokens] (
    [UserId] uniqueidentifier not null foreign key references [asp].[Users] ([Id]) on delete cascade,
    [LoginProvider] varchar(128) not null,
    [Name] varchar(128) not null,
    [Value] varchar(128) null,
    constraint [PK_UserTokens] primary key ([UserId], [LoginProvider], [Name]),
    index [IXC_UserTokens] clustered ([UserId], [LoginProvider], [Name])
);
go

set quoted_identifier off;
go

commit;
go
