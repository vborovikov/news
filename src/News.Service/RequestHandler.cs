namespace News.Service;

using Dodkin.Dispatch;
using Microsoft.Extensions.Logging;
using Relay.RequestModel.Default;

sealed class RequestHandler : UdsRequestHandler
{
    public RequestHandler(Worker worker, string endpoint, ILogger logger)
        : base(endpoint, DefaultRequestDispatcher.From(worker), logger)
    {
        RecognizeTypesFrom(typeof(UpdateFeedCommand).Assembly);
    }
}
