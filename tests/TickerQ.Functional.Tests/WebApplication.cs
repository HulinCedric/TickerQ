using Microsoft.AspNetCore.Mvc.Testing;

namespace TickerQ.Tests.Functional;

public class WebApplication : WebApplicationFactory<Program>
{
    public WebApplication()
    {
        // Force the factory to spin up and run Program.cs
        using var _ = CreateClient();
    }
}