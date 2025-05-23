﻿namespace News.Service.Scheduling;

using System;
using System.Data.Common;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Relay.RequestModel;
using Relay.RequestModel.Default;
using Spryer;

record PersistentCommand : IPersistentCommand
{
    public required int Id { get; init; }
    ICommand IPersistentCommand.Command => (ICommand)JsonSerializer.Deserialize(this.Command, Type.GetType(this.CommandLabel)!)!;
    public required string Command { get; init; }
    public required string CommandLabel { get; init; }
    public required DateTimeOffset DueTime { get; init; }
    public required int RetryCount { get; init; }
}

internal class CommandStore : IPersistentCommandStore
{
    private const int MaxCommandLength = 846;
    private const int MaxCommandLabelLength = 250;
    private const int MaxRetryCount = 1;

    private readonly DbDataSource db;
    private readonly ILogger<CommandStore> log;

    public CommandStore(DbDataSource db, ILogger<CommandStore> log)
    {
        this.db = db;
        this.log = log;
    }

    public async Task<int> PurgeAsync(CancellationToken cancellationToken)
    {
        await using var cnn = await this.db.OpenConnectionAsync(cancellationToken);
        await using var tx = await cnn.BeginTransactionAsync(cancellationToken);
        try
        {
            var affected = await cnn.ExecuteAsync(
                """
                declare @Now datetimeoffset = sysdatetimeoffset();
                delete from rss.Schedule
                where DueTime < @Now;
                """, param: null, tx);
            await tx.CommitAsync(cancellationToken);

            return affected;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            this.log.LogError(EventIds.SchedulingFailed, ex, "Failed to purge command store");
            await tx.RollbackAsync(cancellationToken);

            throw;
        }
    }

    public async ValueTask AddAsync<TCommand>(TCommand command, DateTimeOffset dueTime, CancellationToken cancellationToken) where TCommand : ICommand
    {
        await using var cnn = await this.db.OpenConnectionAsync(cancellationToken);
        await using var tx = await cnn.BeginTransactionAsync(cancellationToken);

        try
        {
            await cnn.ExecuteAsync(
                """
                insert into rss.Schedule (Command, CommandLabel, DueTime)
                values (@Command, @CommandLabel, @DueTime);
                """,
                new
                {
                    Command = JsonSerializer.Serialize(command).AsNVarChar(MaxCommandLength, throwOnMaxLength: true),
                    CommandLabel = command.GetType().AssemblyQualifiedName.AsVarChar(MaxCommandLabelLength, throwOnMaxLength: true),
                    DueTime = dueTime,
                }, tx);

            await tx.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            this.log.LogError(EventIds.SchedulingFailed, ex, "Failed to store scheduled command {Command} to be executed at {DueTime}", command, dueTime);
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async ValueTask<IPersistentCommand?> GetAsync(CancellationToken cancellationToken)
    {
        await using var cnn = await this.db.OpenConnectionAsync(cancellationToken);
        return await cnn.QueryFirstOrDefaultAsync<PersistentCommand>(
            """
            select top 1 s.*
            from rss.Schedule s
            order by s.DueTime; 
            """);
    }

    public async ValueTask RemoveAsync(IPersistentCommand command, CancellationToken cancellationToken)
    {
        await using var cnn = await this.db.OpenConnectionAsync(cancellationToken);
        await using var tx = await cnn.BeginTransactionAsync(cancellationToken);

        try
        {
            await cnn.ExecuteAsync(
                """
                delete from rss.Schedule where Id = @Id;
                """, new { ((PersistentCommand)command).Id }, tx);
            await tx.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            this.log.LogError(EventIds.SchedulingFailed, ex, "Failed to delete command record {Command}", command);
            await tx.RollbackAsync(cancellationToken);

            throw;
        }
    }

    public async ValueTask RetryAsync(IPersistentCommand command, Exception exception, CancellationToken cancellationToken)
    {
        try
        {
            this.log.LogWarning(EventIds.SchedulingFailed, exception, "Failed to execute command {Command}", command);

            var persistent = (PersistentCommand)command;
            if (persistent.RetryCount >= MaxRetryCount)
            {
                return;
            }

            await using var cnn = await this.db.OpenConnectionAsync(cancellationToken);
            await using var tx = await cnn.BeginTransactionAsync(cancellationToken);

            try
            {
                await cnn.ExecuteAsync(
                    """
                    insert into rss.Schedule (Command, CommandLabel, DueTime, RetryCount)
                    values (@Command, @CommandLabel, @DueTime, @RetryCount);
                    """,
                    new
                    {
                        Command = persistent.Command.AsNVarChar(MaxCommandLength),
                        CommandLabel = persistent.CommandLabel.AsVarChar(MaxCommandLabelLength),
                        persistent.DueTime,
                        RetryCount = persistent.RetryCount + 1,
                    }, tx);
                await tx.CommitAsync(cancellationToken);
            }
            catch (Exception x)
            {
                this.log.LogWarning(EventIds.SchedulingFailed, x, "Failed to retry command {Command}", command);
                await tx.RollbackAsync(cancellationToken);
                // no throw
            }
        }
        catch (Exception x) when (x is not OperationCanceledException ocx || ocx.CancellationToken != cancellationToken)
        {
            this.log.LogWarning(EventIds.SchedulingFailed, x, "Failed to roll back from retrying command {Command}", command);
            // no throw
        }
    }
}
