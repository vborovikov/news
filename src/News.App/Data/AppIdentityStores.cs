namespace News.App.Data;

using Microsoft.AspNetCore.Identity;

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
