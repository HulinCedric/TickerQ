using TickerQ.Utilities;

namespace TickerQ.Tests.Functional;

public abstract class WebApplicationConcurrentHostTest
{
    private static void VerifyTickerFunctionsRegistered()
        => Assert.True(
            TickerFunctionProvider.TickerFunctions.Count > 0,
            "Ticker functions should be registered." +
            "TickerQ uses source generation and static function registration at startup. " +
            "If no function is registered, it means a race condition cleared the registered functions.");

    public class FirstWebApplicationShould : IClassFixture<WebApplication>
    {
        [Fact]
        public void Had_ticker_functions_registered() => VerifyTickerFunctionsRegistered();
    }

    public class SecondWebApplicationShould : IClassFixture<WebApplication>
    {
        [Fact]
        public void Had_ticker_functions_registered() => VerifyTickerFunctionsRegistered();
    }
}