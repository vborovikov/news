namespace News.App;

using Data;
using Dodkin.Dispatch;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Relay.InteractionModel;
using Spryer;
using Spryer.AspNetCore.Identity;
using Spryer.AspNetCore.Identity.SqlServer;

public static class Program
{
    static Program()
    {
        DbEnum<PostStatus>.Initialize();
        DbEnum<FeedStatus>.Initialize();
        DbEnum<FeedSafeguard>.Initialize();
        Page.Initialize(10, 15, 25, 50);
    }

    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddSingleton<IApp, AppService>();

        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        builder.Services.Configure<AppOptions>(builder.Configuration.GetSection(AppOptions.AppName));
        builder.Services.AddSingleton(_ => SqlClientFactory.Instance.CreateDataSource(connectionString));
        builder.Services.AddSingleton<IQueueRequestDispatcher>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AppOptions>>().Value;
            var logging = sp.GetRequiredService<ILoggerFactory>();

            var dispatcher = new QueueRequestDispatcher(options.ServiceQueue, options.Endpoint,
                logging.CreateLogger("News.App.Service"));
            dispatcher.RecognizeTypesFrom(typeof(UpdateFeedCommand).Assembly);

            return dispatcher;
        });

        builder.Services.AddIdentity<AppUser, AppRole>(options => options.SignIn.RequireConfirmedAccount = false)
            .AddDapperStores(options =>
            {
                options.UseSqlServer(dbSchema: "asp");
            })
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
        var appOptions = app.Services.GetRequiredService<IOptions<AppOptions>>();
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(appOptions.Value.ImagePath),
            RequestPath = "/img",
        });

        app.UseRouting();
        app.UseAuthorization();
        app.UseAntiforgery();
        app.MapRazorPages();
        app.MapFallbackToPage("/Index");

        Api.Register(app);

        app.Run();
    }
}
