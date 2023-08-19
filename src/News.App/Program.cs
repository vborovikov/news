namespace News.App;

using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using News.App.Data;

public static class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        builder.Services.AddScoped(_ => SqlClientFactory.Instance.CreateDataSource(connectionString));

        builder.Services.AddIdentity<AppUser, AppRole>(options => options.SignIn.RequireConfirmedAccount = false)
            .AddUserStore<AppUserStore>()
            .AddRoleStore<AppRoleStore>()
            .AddDefaultUI()
            .AddDefaultTokenProviders();

        builder.Services.AddRazorPages();
        builder.Services.Configure<RouteOptions>(options =>
        {
            options.LowercaseUrls = true;
        });

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsProduction())
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthorization();
        app.MapRazorPages();
        app.MapFallbackToPage("/Index");

        app.Run();
    }
}
