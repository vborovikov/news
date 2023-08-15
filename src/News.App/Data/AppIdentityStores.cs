namespace News.App.Data;

using System.Data.Common;
using Microsoft.AspNetCore.Identity;
using Identity;

sealed class AppUserStore : UserStore<Guid>
{
    public AppUserStore(DbDataSource db, IdentityErrorDescriber describer) : base(db, describer)
    {
    }
}

sealed class AppRoleStore : RoleStore<Guid>
{
    public AppRoleStore(DbDataSource db, IdentityErrorDescriber describer) : base(db, describer)
    {
    }
}
