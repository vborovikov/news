namespace News.Service.Scheduling;

using System;
using System.Threading;
using System.Threading.Tasks;
using Relay.RequestModel.Default;

sealed class CommandScheduler : DefaultRequestScheduler
{
    private readonly Worker worker;
    private readonly ILogger<CommandScheduler> log;

    public CommandScheduler(Worker worker, IPersistentCommandStore commandStore, ILogger<CommandScheduler> log)
        : base(commandStore)
    {
        this.worker = worker;
        this.log = log;
    }

    protected override object GetRequestHandler(Type requestHandlerType) => this.worker;

    public override async Task ScheduleAsync<TCommand>(TCommand command, DateTimeOffset at)
    {
        try
        {
            await base.ScheduleAsync(command, at).ConfigureAwait(false);
        }
        catch (Exception x) when (x is not OperationCanceledException ocx || ocx.CancellationToken != command.CancellationToken)
        {
            this.log.LogError(EventIds.SchedulingFailed, x, "Failed to schedule command {Command} to be executed at {DueTime}.", command, at);
            throw;
        }
    }

    public override async Task ProcessAsync(CancellationToken cancellationToken)
    {
        try
        {
            this.log.LogInformation(EventIds.SchedulingStarted, "Started scheduling.");

            await base.ProcessAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception x) when (x is not OperationCanceledException ocx || ocx.CancellationToken != cancellationToken)
        {
            this.log.LogError(EventIds.SchedulingFailed, x, "Failed to process scheduled commands.");
            throw;
        }
        finally
        {
            this.log.LogInformation(EventIds.SchedulingStopped, "Stopped scheduling.");
        }
    }
}
