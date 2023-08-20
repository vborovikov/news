namespace News.App.Data;

using System.Data.Common;
using Microsoft.AspNetCore.Identity;
using Identity;

public sealed class AppUser : IdentityUser<Guid>
{
    public AppUser()
    {
        // default Identity UI uses this ctor when registering new users
        this.Id = Guid.NewGuid();
        this.SecurityStamp = Guid.NewGuid().ToString();
    }
}

public sealed class AppRole : IdentityRole<Guid>
{
    public AppRole()
    {
        // default Identity UI uses this ctor when creating new roles
        this.Id = Guid.NewGuid();
    }
}

sealed class AppUserStore : UserStore<AppUser, AppRole, Guid>
{
    public AppUserStore(DbDataSource db, IdentityErrorDescriber describer) : base(db, describer)
    {
    }
}

sealed class AppRoleStore : RoleStore<AppRole, Guid>
{
    public AppRoleStore(DbDataSource db, IdentityErrorDescriber describer) : base(db, describer)
    {
    }
}
