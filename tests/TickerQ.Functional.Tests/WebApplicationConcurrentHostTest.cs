using TickerQ.Utilities;

namespace TickerQ.Tests.Functional;

public abstract class WebApplicationConcurrentHostTest
{
    private static void VerifyTickerFunctionsRegistered()
        => Assert.True(
            TickerFunctionProvider.TickerFunctions.Count > 0,
            "Ticker functions should be registered." +
            "TickerQ use source generation and static function registration at startup." +
            "If no function is registered, this means that race condition empty registered functions.");

    public class FirstWebApplicationShould : IClassFixture<WebApplication>
    {
        [Fact]
        public void Have_ticker_functions_registered() => VerifyTickerFunctionsRegistered();
    }

    public class SecondWebApplicationShould : IClassFixture<WebApplication>
    {
        [Fact]
        public void Have_ticker_functions_registered() => VerifyTickerFunctionsRegistered();
    }
}