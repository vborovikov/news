namespace News.App.Pages;

using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize]
public class FeedModel : EditPageModel
{
    private readonly ILogger<ImportUrlModel> log;

    public FeedModel(DbDataSource db, ILogger<ImportUrlModel> log)
        : base(db)
    {
        this.log = log;
        this.Input = new();
    }

    [BindProperty]
    public InputModel Input { get; set; }

    public async Task OnGet(Guid id)
    {
        await using var cnn = await this.db.OpenConnectionAsync(this.HttpContext.RequestAborted);
        this.Input = await cnn.QuerySingleAsync<InputModel>(
            """
            select af.FeedId, af.Source as FeedUrl, af.Title as FeedTitle, af.Slug as FeedSlug, af.ChannelId
            from rss.AppFeeds af
            where af.UserId = @UserId and af.FeedId = @FeedId;
            """, new { this.UserId, FeedId = id });
    }

    public record InputModel
    {
        public Guid FeedId { get; init; }

        [Required, Url, Display(Name = "Feed URL")]
        public string FeedUrl { get; init; } = "";

        [Display(Name = "Feed title")]
        public string? FeedTitle { get; init; }

        [Required, RegularExpression("^[a-z][a-z0-9-]*$"), MaxLength(50), Display(Name = "Feed slug")]
        public string FeedSlug { get; init; } = "";

        [Required, Display(Name = "Feed channel")]
        public Guid ChannelId { get; init; }
    }
}
