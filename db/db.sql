-- Create database --

use master;
if DB_ID('News') is not null 
begin
    alter database News set single_user with rollback immediate;
    drop database News;
end;

if @@Error = 3702
   RaisError('Cannot delete the database because of the open connections.', 127, 127) with nowait, log;

create database News;
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
    [Id] uniqueidentifier not null primary key,
    [Name] nvarchar(256) null,
    [NormalizedName] nvarchar(256) null index [UX_Roles_NormalizedName] unique where [NormalizedName] is not null,
    [ConcurrencyStamp] nvarchar(max) null
);
go

create table [asp].[Users] (
    [Id] uniqueidentifier NOT NULL primary key,
    [UserName] nvarchar(256) NULL,
    [NormalizedUserName] nvarchar(256) NULL index [UX_Users_NormalizedUserName] unique where [NormalizedUserName] is not null,
    [Email] nvarchar(256) NULL,
    [NormalizedEmail] nvarchar(256) null index [IX_Users_NormalizedEmail],
    [EmailConfirmed] bit NOT NULL,
    [PasswordHash] nvarchar(max) NULL,
    [SecurityStamp] nvarchar(max) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [PhoneNumberConfirmed] bit NOT NULL,
    [TwoFactorEnabled] bit NOT NULL,
    [LockoutEnd] datetimeoffset NULL,
    [LockoutEnabled] bit NOT NULL,
    [AccessFailedCount] int NOT NULL
);
go

create table [asp].[RoleClaims] (
    [Id] int not null identity primary key,
    [RoleId] uniqueidentifier not null index [IX_RoleClaims_RoleId],
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    constraint [FK_RoleClaims_Roles_RoleId] foreign key ([RoleId]) references [asp].[Roles] ([Id]) on delete cascade
);
go

create table [asp].[UserClaims] (
    [Id] int not null identity primary key,
    [UserId] uniqueidentifier not null index [IX_UserClaims_UserId],
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    constraint [FK_UserClaims_Users_UserId] foreign key ([UserId]) references [asp].[Users] ([Id]) on delete cascade
);
go

create table [asp].[UserLogins] (
    [LoginProvider] nvarchar(450) NOT NULL,
    [ProviderKey] nvarchar(450) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] uniqueidentifier not null index [IX_UserLogins_UserId],
    constraint [PK_UserLogins] primary key ([LoginProvider], [ProviderKey]),
    constraint [FK_UserLogins_Users_UserId] foreign key ([UserId]) references [asp].[Users] ([Id]) on delete cascade
);
go

create table [asp].[UserRoles] (
    [UserId] uniqueidentifier not null,
    [RoleId] uniqueidentifier not null index [IX_UserRoles_RoleId],
    constraint [PK_UserRoles] primary key ([UserId], [RoleId]),
    constraint [FK_UserRoles_Roles_RoleId] foreign key ([RoleId]) references [asp].[Roles] ([Id]) on delete cascade,
    constraint [FK_UserRoles_Users_UserId] foreign key ([UserId]) references [asp].[Users] ([Id]) on delete cascade
);
go

create table [asp].[UserTokens] (
    [UserId] uniqueidentifier NOT NULL,
    [LoginProvider] nvarchar(450) NOT NULL,
    [Name] nvarchar(450) NOT NULL,
    [Value] nvarchar(max) NULL,
    constraint [PK_UserTokens] primary key ([UserId], [LoginProvider], [Name]),
    constraint [FK_UserTokens_Users_UserId] foreign key ([UserId]) references [asp].[Users] ([Id]) on delete cascade
);
go

set quoted_identifier off;
go

commit;
go
