using FluentAssertions;
using NSubstitute;
using TickerQ.Utilities.Entities;
using TickerQ.Utilities.Interfaces;
using TickerQ.Utilities.Interfaces.Managers;
using TickerQ.Utilities.Managers;
using TickerQ.Utilities.Models;
using TickerQ.Utilities;
using TickerQ.Utilities.Enums;

namespace TickerQ.Tests;

public class RunCronTickerOnDemandTests
{
    [Fact]
    public async Task RunCronTickerOnDemand_HappyPath_DispatchesAndNotifies()
    {
        // Arrange
        var persistence = Substitute.For<ITickerPersistenceProvider<TimeTickerEntity, CronTickerEntity>>();
        var scheduler = Substitute.For<ITickerQHostScheduler>();
        var clock = Substitute.For<ITickerClock>();
        var notifier = Substitute.For<ITickerQNotificationHubSender>();
        var dispatcher = Substitute.For<ITickerQDispatcher>();

        clock.UtcNow.Returns(DateTime.UtcNow);
        dispatcher.IsEnabled.Returns(true);

        var executionContext = new TickerExecutionContext();

        var manager = new TickerManager<TimeTickerEntity, CronTickerEntity>(
            persistence,
            scheduler,
            clock,
            notifier,
            executionContext,
            dispatcher);

        var cronId = Guid.NewGuid();
        var cron = new CronTickerEntity { Id = cronId, Function = "TestFunc", Retries = 0, RetryIntervals = Array.Empty<int>() };

        persistence.GetCronTickerById(cronId, Arg.Any<CancellationToken>()).Returns(Task.FromResult(cron));

        // When InsertCronTickerOccurrences is called, just return 1
        persistence.InsertCronTickerOccurrences(Arg.Any<CronTickerOccurrenceEntity<CronTickerEntity>[]>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(1));

        // Simulate acquire returning the occurrence with the CronTicker populated
        persistence.AcquireImmediateCronOccurrencesAsync(Arg.Any<Guid[]>(), Arg.Any<CancellationToken>()).Returns(ci => Task.FromResult(Array.Empty<CronTickerOccurrenceEntity<CronTickerEntity>>()));

        // Register function in provider so caching works
        TickerFunctionProvider.RegisterFunctions(new System.Collections.Generic.Dictionary<string, (string, TickerTaskPriority, TickerFunctionDelegate)>
        {
            { "TestFunc", ("* * * * *", TickerTaskPriority.Normal, (ct, sp, ctx) => Task.CompletedTask) }
        });
        TickerFunctionProvider.Build();

        // Act
        var result = await ((ICronTickerManager<CronTickerEntity>)manager).RunCronTickerOnDemandAsync(cronId, CancellationToken.None);

        // Assert
        result.IsSucceeded.Should().BeTrue();
        result.Result.Id.Should().Be(cronId);

        await persistence.Received(1).InsertCronTickerOccurrences(Arg.Any<CronTickerOccurrenceEntity<CronTickerEntity>[]>(), Arg.Any<CancellationToken>());
        await notifier.Received(1).AddCronOccurrenceAsync(cronId, Arg.Any<object>());

        // Because AcquireImmediateCronOccurrencesAsync returned empty array, dispatcher should not be called
        await dispatcher.DidNotReceiveWithAnyArgs().DispatchAsync(default!);
    }

    [Fact]
    public async Task RunCronTickerOnDemand_MissingTicker_ReturnsFailure()
    {
        // Arrange
        var persistence = Substitute.For<ITickerPersistenceProvider<TimeTickerEntity, CronTickerEntity>>();
        var scheduler = Substitute.For<ITickerQHostScheduler>();
        var clock = Substitute.For<ITickerClock>();
        var notifier = Substitute.For<ITickerQNotificationHubSender>();
        var dispatcher = Substitute.For<ITickerQDispatcher>();

        var executionContext = new TickerExecutionContext();

        var manager = new TickerManager<TimeTickerEntity, CronTickerEntity>(
            persistence,
            scheduler,
            clock,
            notifier,
            executionContext,
            dispatcher);

        var cronId = Guid.NewGuid();
        persistence.GetCronTickerById(cronId, Arg.Any<CancellationToken>()).Returns(Task.FromResult<CronTickerEntity>(null!));

        // Act
        var result = await ((ICronTickerManager<CronTickerEntity>)manager).RunCronTickerOnDemandAsync(cronId, CancellationToken.None);

        // Assert
        result.IsSucceeded.Should().BeFalse();
        result.Exception.Should().NotBeNull();
    }

    [Fact]
    public async Task RunCronTickerOnDemand_WhenAcquireReturnsOccurrence_DispatchIsCalledAndNotified()
    {
        // Arrange
        var persistence = Substitute.For<ITickerPersistenceProvider<TimeTickerEntity, CronTickerEntity>>();
        var scheduler = Substitute.For<ITickerQHostScheduler>();
        var clock = Substitute.For<ITickerClock>();
        var notifier = Substitute.For<ITickerQNotificationHubSender>();
        var dispatcher = Substitute.For<ITickerQDispatcher>();

        var now = DateTime.UtcNow;
        clock.UtcNow.Returns(now);
        dispatcher.IsEnabled.Returns(true);

        var executionContext = new TickerExecutionContext();

        var manager = new TickerManager<TimeTickerEntity, CronTickerEntity>(
            persistence,
            scheduler,
            clock,
            notifier,
            executionContext,
            dispatcher);

        var cronId = Guid.NewGuid();
        var cron = new CronTickerEntity { Id = cronId, Function = "TestFunc", Retries = 0, RetryIntervals = Array.Empty<int>() };
        persistence.GetCronTickerById(cronId, Arg.Any<CancellationToken>()).Returns(Task.FromResult(cron));

        persistence.InsertCronTickerOccurrences(Arg.Any<CronTickerOccurrenceEntity<CronTickerEntity>[]>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(1));

        var occurrenceId = Guid.NewGuid();
        var acquiredOccurrence = new CronTickerOccurrenceEntity<CronTickerEntity>
        {
            Id = occurrenceId,
            Status = TickerStatus.Idle,
            ExecutionTime = now,
            CronTickerId = cronId,
            CronTicker = cron
        };

        persistence.AcquireImmediateCronOccurrencesAsync(Arg.Any<Guid[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new[] { acquiredOccurrence }));

        // Register function in provider so caching works
        TickerFunctionProvider.RegisterFunctions(new System.Collections.Generic.Dictionary<string, (string, TickerTaskPriority, TickerFunctionDelegate)>
        {
            { "TestFunc", ("* * * * *", TickerTaskPriority.Normal, (ct, sp, ctx) => Task.CompletedTask) }
        });
        TickerFunctionProvider.Build();

        // Act
        var result = await ((ICronTickerManager<CronTickerEntity>)manager).RunCronTickerOnDemandAsync(cronId, CancellationToken.None);

        // Assert
        result.IsSucceeded.Should().BeTrue();
        result.Result.Id.Should().Be(cronId);

        await persistence.Received(1).InsertCronTickerOccurrences(Arg.Any<CronTickerOccurrenceEntity<CronTickerEntity>[]>(), Arg.Any<CancellationToken>());

        // Dispatcher called with context that references the cron ticker and occurrence
        await dispatcher.Received(1).DispatchAsync(Arg.Is<InternalFunctionContext[]>(ctxs =>
            ctxs.Length == 1 &&
            ctxs[0].ParentId == cronId &&
            ctxs[0].FunctionName == "TestFunc" &&
            ctxs[0].TickerId == occurrenceId
        ), Arg.Any<CancellationToken>());

        // Notification should have been sent with the acquired occurrence
        await notifier.Received(1).AddCronOccurrenceAsync(cronId, Arg.Is<object>(o => ((CronTickerOccurrenceEntity<CronTickerEntity>)o).Id == occurrenceId));

    }
}
