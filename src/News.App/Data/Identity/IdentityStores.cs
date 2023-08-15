namespace News.App.Data.Identity;

using System.Data.Common;
using System.Security.Claims;
using System.Threading;
using Dapper;
using Microsoft.AspNetCore.Identity;

public abstract class UserStore<TKey> : UserStoreBase<IdentityUser<TKey>, IdentityRole<TKey>, TKey,
        IdentityUserClaim<TKey>, IdentityUserRole<TKey>, IdentityUserLogin<TKey>,
        IdentityUserToken<TKey>, IdentityRoleClaim<TKey>>
    where TKey : IEquatable<TKey>, IParsable<TKey>
{
    private readonly DbDataSource _db;

    protected UserStore(DbDataSource db, IdentityErrorDescriber describer) : base(describer)
    {
        _db = db;
    }

    public override async Task AddClaimsAsync(IdentityUser<TKey> user, IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(claims);

        await using var cnn = await _db.OpenConnectionAsync(cancellationToken);
        await using var tx = await cnn.BeginTransactionAsync(cancellationToken);
        try
        {
            await cnn.ExecuteAsync(
                @"insert into asp.UserClaims (UserId, ClaimType, ClaimValue)
                values (@UserId, @ClaimType, @ClaimValue)",
                claims.Select(claim => CreateUserClaim(user, claim)), tx);
            await tx.CommitAsync(cancellationToken);
        }
        catch (Exception)
        {
            await tx.RollbackAsync(cancellationToken);
        }
    }

    public override async Task AddLoginAsync(IdentityUser<TKey> user, UserLoginInfo login, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(login);

        await using var cnn = await _db.OpenConnectionAsync(cancellationToken);
        await using var tx = await cnn.BeginTransactionAsync(cancellationToken);
        try
        {
            await cnn.ExecuteAsync(
                @"insert into asp.UserLogins (UserId, LoginProvider, ProviderKey, ProviderDisplayName)
                values (@UserId, @LoginProvider, @ProviderKey, @ProviderDisplayName)",
                CreateUserLogin(user, login), tx);
            await tx.CommitAsync(cancellationToken);
        }
        catch (Exception)
        {
            await tx.RollbackAsync(cancellationToken);
        }
    }

    public override async Task AddToRoleAsync(IdentityUser<TKey> user, string normalizedRoleName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(normalizedRoleName);

        await using var cnn = await _db.OpenConnectionAsync(cancellationToken);
        var role = await cnn.QueryFirstOrDefaultAsync<IdentityRole<TKey>>(@"
                select * from asp.Roles where NormalizedName = @normalizedRoleName",
                new { normalizedRoleName });
        if (role is null)
            throw new InvalidOperationException($"Role '{normalizedRoleName}' does not exist.");

        await using var tx = await cnn.BeginTransactionAsync(cancellationToken);
        try
        {
            await cnn.ExecuteAsync(
                @"insert into asp.UserRoles (UserId, RoleId)
                values (@UserId, @RoleId)",
                CreateUserRole(user, role), tx);
            await tx.CommitAsync(cancellationToken);
        }
        catch (Exception)
        {
            await tx.RollbackAsync(cancellationToken);
        }
    }

    public override async Task<IdentityResult> CreateAsync(IdentityUser<TKey> user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        await using var cnn = await _db.OpenConnectionAsync(cancellationToken);
        await using var tx = await cnn.BeginTransactionAsync(cancellationToken);
        var result = 0;
        try
        {
            result = await cnn.ExecuteAsync(
                @"insert into asp.Users
                    ([Id],UserName,NormalizedUserName,Email,NormalizedEmail,EmailConfirmed,
                     PasswordHash,SecurityStamp,ConcurrencyStamp,PhoneNumber,PhoneNumberConfirmed,
                     TwoFactorEnabled,LockoutEnd,LockoutEnabled,AccessFailedCount)
                values
                    (@Id,@UserName,@NormalizedUserName,@Email,@NormalizedEmail,@EmailConfirmed,
                     @PasswordHash,@SecurityStamp,@ConcurrencyStamp,@PhoneNumber,@PhoneNumberConfirmed,
                     @TwoFactorEnabled,@LockoutEnd,@LockoutEnabled,@AccessFailedCount)", user, tx);
            await tx.CommitAsync(cancellationToken);
        }
        catch (Exception)
        {
            await tx.RollbackAsync(cancellationToken);
        }

        return result > 0 ? IdentityResult.Success : IdentityResult.Failed(this.ErrorDescriber.DefaultError());
    }

    public override async Task<IdentityResult> DeleteAsync(IdentityUser<TKey> user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        await using var cnn = await _db.OpenConnectionAsync(cancellationToken);
        await using var tx = await cnn.BeginTransactionAsync(cancellationToken);
        var result = 0;
        try
        {
            result = await cnn.ExecuteAsync(@"delete from asp.Users where Id = @Id", new { user.Id }, tx);
            await tx.CommitAsync(cancellationToken);
        }
        catch (Exception)
        {
            await tx.RollbackAsync(cancellationToken);
        }
        return result > 0 ? IdentityResult.Success : IdentityResult.Failed(this.ErrorDescriber.DefaultError());
    }

    public override async Task<IdentityUser<TKey>?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(normalizedEmail);

        await using var cnn = await _db.OpenConnectionAsync(cancellationToken);
        var user = await cnn.QuerySingleOrDefaultAsync<IdentityUser<TKey>>(
            @"select u.* from asp.Users u where u.NormalizedEmail = @normalizedEmail", new { normalizedEmail });
        return user;
    }

    public override Task<IdentityUser<TKey>?> FindByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return FindUserAsync(TKey.Parse(userId, null), cancellationToken);
    }

    public override async Task<IdentityUser<TKey>?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(normalizedUserName);

        await using var cnn = await _db.OpenConnectionAsync(cancellationToken);
        var user = await cnn.QuerySingleOrDefaultAsync<IdentityUser<TKey>>(@"
            select u.* from asp.Users u where u.NormalizedUserName = @normalizedUserName", new { normalizedUserName });
        return user;
    }

    public override async Task<IList<Claim>> GetClaimsAsync(IdentityUser<TKey> user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        await using var cnn = await _db.OpenConnectionAsync(cancellationToken);
        var userClaims = await cnn.QueryAsync<IdentityUserClaim<TKey>>(@"select uc.* from asp.UserClaims uc where uc.UserId = @Id", new { user.Id });
        return userClaims.Select(uc => uc.ToClaim()).ToArray();
    }

    public override async Task<IList<UserLoginInfo>> GetLoginsAsync(IdentityUser<TKey> user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        await using var cnn = await _db.OpenConnectionAsync(cancellationToken);
        var userLogins = await cnn.QueryAsync<IdentityUserLogin<TKey>>(@"select ul.* from asp.UserLogins ul where ul.UserId = @Id", user);
        return userLogins.Select(ul => new UserLoginInfo(ul.LoginProvider, ul.ProviderKey, ul.ProviderDisplayName)).ToArray();
    }

    public override async Task<IList<string>> GetRolesAsync(IdentityUser<TKey> user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        await using var cnn = await _db.OpenConnectionAsync(cancellationToken);
        var userRoles = await cnn.QueryAsync<string>(
            @"select r.Name 
            from asp.UserRoles ur
            inner join asp.Roles r on r.Id = ur.RoleId
            where ur.UserId = @Id", new { user.Id });
        return userRoles.ToArray();
    }

    public override async Task<IList<IdentityUser<TKey>>> GetUsersForClaimAsync(Claim claim, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claim);

        await using var cnn = await _db.OpenConnectionAsync(cancellationToken);
        var users = await cnn.QueryAsync<IdentityUser<TKey>>(
            @"select u.* from asp.Users u
            inner join asp.UserClaims uc on u.Id = uc.UserId
            where uc.ClaimVlaue = @Value and uc.ClaimType = @Type", claim);
        return users.ToArray();
    }

    public override async Task<IList<IdentityUser<TKey>>> GetUsersInRoleAsync(string normalizedRoleName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(normalizedRoleName);

        await using var cnn = await _db.OpenConnectionAsync(cancellationToken);
        var users = await cnn.QueryAsync<IdentityUser<TKey>>(
            @"select u.* from asp.Users u
            inner join asp.UserRoles ur on u.Id = ur.UserId
            inner join asp.Roles r on r.NormalizedName = @normalizedRoleName
            where ur.RoleId = r.Id", new { normalizedRoleName });

        return users.ToArray();
    }

    public override async Task<bool> IsInRoleAsync(IdentityUser<TKey> user, string normalizedRoleName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(normalizedRoleName);

        await using var cnn = await _db.OpenConnectionAsync(cancellationToken);
        var userRole = await cnn.QueryFirstOrDefaultAsync<IdentityUserRole<TKey>>(
            @"select ur.* from asp.UserRoles ur
            inner join asp.Roles r on r.Id = ur.RoleId
            where ur.UserId = @Id and r.NormalizedName = @normalizedRoleName",
            new { user.Id, normalizedRoleName });
        return userRole is not null;
    }

    public override async Task RemoveClaimsAsync(IdentityUser<TKey> user, IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(claims);

        await using var cnn = await _db.OpenConnectionAsync(cancellationToken);
        await using var tx = await cnn.BeginTransactionAsync(cancellationToken);
        try
        {
            await cnn.ExecuteAsync(
                @"delete from asp.UserClaims
                where UserId = @UserId and ClaimType = @ClaimType and ClaimValue = @ClaimValue",
                claims.Select(claim => CreateUserClaim(user, claim)), tx);
            await tx.CommitAsync(cancellationToken);
        }
        catch (Exception)
        {
            await tx.RollbackAsync(cancellationToken);
        }
    }

    public override async Task RemoveFromRoleAsync(IdentityUser<TKey> user, string normalizedRoleName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(normalizedRoleName);

        await using var cnn = await _db.OpenConnectionAsync(cancellationToken);
        await using var tx = await cnn.BeginTransactionAsync(cancellationToken);
        try
        {
            await cnn.ExecuteAsync(
                @"delete from asp.UserRoles
                where UserId = @Id and RoleId = (select r.Id from asp.Roles r where r.NormalizedName = @normalizedRoleName)",
                new { user.Id, normalizedRoleName }, tx);
            await tx.CommitAsync(cancellationToken);
        }
        catch (Exception)
        {
            await tx.RollbackAsync(cancellationToken);
        }
    }

    public override async Task RemoveLoginAsync(IdentityUser<TKey> user, string loginProvider, string providerKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(loginProvider);
        ArgumentNullException.ThrowIfNull(providerKey);

        await using var cnn = await _db.OpenConnectionAsync(cancellationToken);
        await using var tx = await cnn.BeginTransactionAsync(cancellationToken);
        try
        {
            await cnn.ExecuteAsync(
                @"delete from asp.UserLogins
                where UserId = @UserId and LoginProvider = @LoginProvider and ProviderKey = @ProviderKey",
                new { UserId = user.Id, LoginProvider = loginProvider, ProviderKey = providerKey }, tx);
            await tx.CommitAsync(cancellationToken);
        }
        catch (Exception)
        {
            await tx.RollbackAsync(cancellationToken);
        }
    }

    public override async Task ReplaceClaimAsync(IdentityUser<TKey> user, Claim claim, Claim newClaim, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(newClaim);

        await using var cnn = await _db.OpenConnectionAsync(cancellationToken);
        await using var tx = await cnn.BeginTransactionAsync(cancellationToken);
        try
        {
            await cnn.ExecuteAsync(
                @"update asp.UserClaims
                set ClaimType = @NewType, ClaimValue = @NewValue
                where UserId = @UserId and ClaimType = @OldType and ClaimValue = @OldValue",
                new
                {
                    UserId = user.Id,
                    OldType = claim.Type,
                    OldValue = claim.Value,
                    NewType = newClaim.Type,
                    NewValue = newClaim.Value,
                }, tx);
            await tx.CommitAsync(cancellationToken);
        }
        catch (Exception)
        {
            await tx.RollbackAsync(cancellationToken);
        }
    }

    public override async Task<IdentityResult> UpdateAsync(IdentityUser<TKey> user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        await using var cnn = await _db.OpenConnectionAsync(cancellationToken);
        await using var tx = await cnn.BeginTransactionAsync(cancellationToken);
        var result = 0;
        try
        {
            result = await cnn.ExecuteAsync(
                @"update asp.Users
                set Email = @Email,
                    NormalizedEmail = @NormalizedEmail,
                    EmailConfirmed = @EmailConfirmed,
                    PasswordHash = @PasswordHash,
                    SecurityStamp = @SecurityStamp,
                    ConcurrencyStamp = @ConcurrencyStamp,
                    PhoneNumber = @PhoneNumber,
                    PhoneNumberConfirmed = @PhoneNumberConfirmed,
                    TwoFactorEnabled = @TwoFactorEnabled,
                    LockoutEnd = @LockoutEnd,
                    LockoutEnabled = @LockoutEnabled,
                    AccessFailedCount = @AccessFailedCount
                where [Id] = @Id", user, tx);
            await tx.CommitAsync(cancellationToken);
        }
        catch (Exception)
        {
            await tx.RollbackAsync(cancellationToken);
        }

        return result > 0 ? IdentityResult.Success : IdentityResult.Failed(this.ErrorDescriber.DefaultError());
    }

    protected override async Task AddUserTokenAsync(IdentityUserToken<TKey> token)
    {
        ArgumentNullException.ThrowIfNull(token);

        await using var cnn = await _db.OpenConnectionAsync();
        await using var tx = await cnn.BeginTransactionAsync();
        try
        {
            await cnn.ExecuteAsync(
                @"insert into asp.UserTokens (UserId, LoginProvider, Name, [Value])
                values (@UserId, @LoginProvider, @Name, @Value)", token, tx);
            await tx.CommitAsync();
        }
        catch (Exception)
        {
            await tx.RollbackAsync();
        }
    }

    protected override async Task<IdentityRole<TKey>?> FindRoleAsync(string normalizedRoleName, CancellationToken cancellationToken)
    {
        await using var cnn = await _db.OpenConnectionAsync(cancellationToken);
        var role = await cnn.QuerySingleOrDefaultAsync<IdentityRole<TKey>>(
            @"select r.* from asp.Roles r 
            where r.NormalizedName = @normalizedRoleName", new { normalizedRoleName });
        return role;
    }

    protected override async Task<IdentityUserToken<TKey>?> FindTokenAsync(IdentityUser<TKey> user, string loginProvider, string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(loginProvider);
        ArgumentNullException.ThrowIfNull(name);

        await using var cnn = await _db.OpenConnectionAsync(cancellationToken);
        var token = await cnn.QuerySingleOrDefaultAsync<IdentityUserToken<TKey>>(
            @"select ut.* from asp.UserTokens ut 
            where ut.UserId = @UserId and ut.LoginProvider = @LoginProvider and ut.Name = @Name",
            new { UserId = user.Id, LoginProvider = loginProvider, Name = name });
        return token;
    }

    protected override async Task<IdentityUser<TKey>?> FindUserAsync(TKey userId, CancellationToken cancellationToken)
    {
        await using var cnn = await _db.OpenConnectionAsync(cancellationToken);
        var user = await cnn.QuerySingleOrDefaultAsync<IdentityUser<TKey>>(
            @"select u.* from asp.Users u where u.Id = @userId", new { userId });
        return user;
    }

    protected override async Task<IdentityUserLogin<TKey>?> FindUserLoginAsync(TKey userId, string loginProvider, string providerKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(loginProvider);
        ArgumentNullException.ThrowIfNull(providerKey);

        await using var cnn = await _db.OpenConnectionAsync(cancellationToken);
        var login = await cnn.QuerySingleOrDefaultAsync<IdentityUserLogin<TKey>>(
            @"select ul.* from asp.UserLogins ul 
            where ul.UserId = @UserId and ul.LoginProvider = @LoginProvider and ul.ProviderKey = @ProviderKey",
            new { UserId = userId, LoginProvider = loginProvider, ProviderKey = providerKey });
        return login;
    }

    protected override async Task<IdentityUserLogin<TKey>?> FindUserLoginAsync(string loginProvider, string providerKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(loginProvider);
        ArgumentNullException.ThrowIfNull(providerKey);

        await using var cnn = await _db.OpenConnectionAsync(cancellationToken);
        var login = await cnn.QuerySingleOrDefaultAsync<IdentityUserLogin<TKey>>(
            @"select ul.* from asp.UserLogins ul 
            where ul.LoginProvider = @LoginProvider and ul.ProviderKey = @ProviderKey",
            new { LoginProvider = loginProvider, ProviderKey = providerKey });
        return login;
    }

    protected override async Task<IdentityUserRole<TKey>?> FindUserRoleAsync(TKey userId, TKey roleId, CancellationToken cancellationToken)
    {
        await using var cnn = await _db.OpenConnectionAsync(cancellationToken);
        var userRole = await cnn.QuerySingleOrDefaultAsync<IdentityUserRole<TKey>>(
            @"select ur.* from asp.UserRoles ur 
            where ur.UserId = @userId and ur.RoleId = @roleId",
            new { userId, roleId });
        return userRole;
    }

    protected override async Task RemoveUserTokenAsync(IdentityUserToken<TKey> token)
    {
        ArgumentNullException.ThrowIfNull(token);

        await using var cnn = await _db.OpenConnectionAsync();
        await using var tx = await cnn.BeginTransactionAsync();
        try
        {
            await cnn.ExecuteAsync(
                @"delete from asp.UserTokens
                where UserId = @UserId and LoginProvider = @LoginProvider and Name = @Name", token, tx);
            await tx.CommitAsync();
        }
        catch (Exception)
        {
            await tx.RollbackAsync();
        }
    }
}

public abstract class RoleStore<TKey> : RoleStoreBase<IdentityRole<TKey>, TKey, IdentityUserRole<TKey>, IdentityRoleClaim<TKey>>
    where TKey : IEquatable<TKey>
{
    private readonly DbDataSource _db;

    protected RoleStore(DbDataSource db, IdentityErrorDescriber describer) : base(describer)
    {
        _db = db;
    }

    public override async Task AddClaimAsync(IdentityRole<TKey> role, Claim claim, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(role);
        ArgumentNullException.ThrowIfNull(claim);

        await using var cnn = await _db.OpenConnectionAsync(cancellationToken);
        await using var tx = await cnn.BeginTransactionAsync(cancellationToken);
        try
        {
            await cnn.ExecuteAsync(
                @"insert into asp.RoleClaims (RoleId, ClaimType, ClaimValue)
                values (@RoleId, @ClaimType, @ClaimValue)",
                new { RoleId = role.Id, ClaimType = claim.Type, ClaimValue = claim.Value }, tx);
            await tx.CommitAsync(cancellationToken);
        }
        catch (Exception)
        {
            await tx.RollbackAsync(cancellationToken);
        }
    }

    public override async Task<IdentityResult> CreateAsync(IdentityRole<TKey> role, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(role);

        await using var cnn = await _db.OpenConnectionAsync(cancellationToken);
        await using var tx = await cnn.BeginTransactionAsync(cancellationToken);
        var result = 0;
        try
        {
            result = await cnn.ExecuteAsync(
                @"insert into asp.Roles (Id, Name, NormalizedName, ConcurrencyStamp)
                values (@Id, @Name, @NormalizedName, @ConcurrencyStamp)", role, tx);
            await tx.CommitAsync(cancellationToken);
        }
        catch (Exception)
        {
            await tx.RollbackAsync(cancellationToken);
        }

        return result > 0 ? IdentityResult.Success : IdentityResult.Failed(this.ErrorDescriber.DefaultError());
    }

    public override async Task<IdentityResult> DeleteAsync(IdentityRole<TKey> role, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(role);

        await using var cnn = await _db.OpenConnectionAsync(cancellationToken);
        await using var tx = await cnn.BeginTransactionAsync(cancellationToken);
        var result = 0;
        try
        {
            result = await cnn.ExecuteAsync(
                @"delete from asp.Roles where Id = @Id", new { role.Id }, tx);
            await tx.CommitAsync(cancellationToken);
        }
        catch (Exception)
        {
            await tx.RollbackAsync(cancellationToken);
        }

        return result > 0 ? IdentityResult.Success : IdentityResult.Failed(this.ErrorDescriber.DefaultError());
    }

    public override async Task<IdentityRole<TKey>?> FindByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        await using var cnn = await _db.OpenConnectionAsync(cancellationToken);
        var role = await cnn.QuerySingleOrDefaultAsync<IdentityRole<TKey>>(@"
            select r.* from asp.Roles r where r.Id = @id", new { id });
        return role;
    }

    public override async Task<IdentityRole<TKey>?> FindByNameAsync(string normalizedName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(normalizedName);

        await using var cnn = await _db.OpenConnectionAsync(cancellationToken);
        var role = await cnn.QuerySingleOrDefaultAsync<IdentityRole<TKey>>(@"
            select r.* from asp.Roles r where r.NormalizedName = @normalizedName", new { normalizedName });
        return role;
    }

    public override async Task<IList<Claim>> GetClaimsAsync(IdentityRole<TKey> role, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(role);

        await using var cnn = await _db.OpenConnectionAsync(cancellationToken);
        var claims = await cnn.QueryAsync<IdentityRoleClaim<TKey>>(@"
            select c.* from asp.RoleClaims c where c.RoleId = @RoleId", new { RoleId = role.Id });
        return claims.Select(rc => rc.ToClaim()).ToArray();
    }

    public override async Task RemoveClaimAsync(IdentityRole<TKey> role, Claim claim, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(role);
        ArgumentNullException.ThrowIfNull(claim);

        await using var cnn = await _db.OpenConnectionAsync(cancellationToken);
        await using var tx = await cnn.BeginTransactionAsync(cancellationToken);
        try
        {
            await cnn.ExecuteAsync(
                @"delete from asp.RoleClaims where RoleId = @RoleId and ClaimType = @ClaimType and ClaimValue = @ClaimValue",
                new { RoleId = role.Id, ClaimType = claim.Type, ClaimValue = claim.Value }, tx);
            await tx.CommitAsync(cancellationToken);
        }
        catch (Exception)
        {
            await tx.RollbackAsync(cancellationToken);
        }
    }

    public override async Task<IdentityResult> UpdateAsync(IdentityRole<TKey> role, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(role);

        await using var cnn = await _db.OpenConnectionAsync(cancellationToken);
        await using var tx = await cnn.BeginTransactionAsync(cancellationToken);
        var result = 0;
        try
        {
            result = await cnn.ExecuteAsync(
                @"update asp.Roles 
                set Name = @Name, NormalizedName = @NormalizedName, ConcurrencyStamp = @ConcurrencyStamp
                where Id = @Id", role, tx);
            await tx.CommitAsync(cancellationToken);
        }
        catch (Exception)
        {
            await tx.RollbackAsync(cancellationToken);
        }

        return result > 0 ? IdentityResult.Success : IdentityResult.Failed(this.ErrorDescriber.DefaultError());
    }
}
