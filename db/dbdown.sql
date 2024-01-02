use News;
go

begin transaction;
go

drop table asp.UserTokens;
go

drop table asp.UserRoles;
go

drop table asp.UserLogins;
go

drop table asp.UserClaims;
go

drop table asp.RoleClaims;
go

drop table asp.Users;
go

drop table asp.Roles;
go

commit;
go

drop schema asp;
go

drop database News;
go