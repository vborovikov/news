namespace News.Service;

using Dodkin;
using Dodkin.Dispatch;
using Microsoft.Extensions.Logging;
using Relay.RequestModel.Default;

sealed class RequestHandler : QueueRequestHandler
{
    public RequestHandler(Worker worker, MessageEndpoint endpoint, ILogger logger)
        : base(DefaultRequestDispatcher.From(worker), endpoint, logger)
    {
        this.ReadProperties = MessageProperty.None; // ignore request TTL
        RecognizeTypesFrom(typeof(UpdateFeedCommand).Assembly);
    }
}
