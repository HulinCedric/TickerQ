using TickerQ.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTickerQ();

var app = builder.Build();

app.UseTickerQ();

await app.RunAsync();