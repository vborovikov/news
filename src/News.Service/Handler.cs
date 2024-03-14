namespace News.Service
{
    using System;
    using Dodkin;
    using Dodkin.Dispatch;
    using Microsoft.Extensions.Logging;
    using Relay.RequestModel.Default;

    sealed class Handler : QueueRequestHandler
    {
        public Handler(Worker worker, MessageEndpoint endpoint, ILogger logger)
            : base(new RequestHandler(worker), endpoint, logger)
        {
            this.PeekProperties = MessageProperty.CorrelationId;
        }

        // filter out the messages with CorrelationId set, these are the response messages for the UserAgent dispatcher
        protected override bool CanDispatchRequest(in Message message) => message.CorrelationId != default;
        // do nothing, the message will be picked up by the UserAgent dispatcher
        protected override bool TryDispatchRequest(in Message message) => true;

        private sealed class RequestHandler : DefaultRequestDispatcherBase
        {
            private readonly Worker worker;

            public RequestHandler(Worker worker)
            {
                this.worker = worker;
            }

            protected override object GetRequestHandler(Type requestHandlerType) => this.worker;
        }
    }
}
