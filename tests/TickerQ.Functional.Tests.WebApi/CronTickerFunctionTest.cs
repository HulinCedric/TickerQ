using TickerQ.Utilities.Base;

namespace TickerQ.Functional.Tests.WebApi;

public class CronTickerFunctionTest
{
    [TickerFunction(functionName: nameof(CronTickerFunctionTest), cronExpression: "* * * * * *")]
    public Task Run(CancellationToken cancellationToken) => Task.Delay(1000, cancellationToken);
}