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
        this.PeekProperties = MessageProperty.CorrelationId;
        this.ReadProperties = MessageProperty.None; // ignore request TTL
        RecognizeTypesFrom(typeof(UpdateFeedCommand).Assembly);
    }

    // filter out the messages with CorrelationId set, these are the response messages for the UserAgent dispatcher
    protected override bool CanDispatchRequest(in Message message) => message.CorrelationId != default;
    // do nothing, the message will be picked up by the UserAgent dispatcher
    protected override bool TryDispatchRequest(in Message message) => true;
}
