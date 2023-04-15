using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.Options;
using System.Timers;
using Timer = System.Timers.Timer;

namespace DetectIdleCircuits;

public sealed class IdleCircuitHandler : CircuitHandler, IDisposable
{
    readonly Timer timer;
    readonly ILogger logger;

    public IdleCircuitHandler(IOptions<IdleCircuitOptions> options, ILogger<IdleCircuitHandler> logger)
    {
        this.logger = logger;
        timer = new Timer();
        timer.Interval = options.Value.IdleTimeout.TotalMilliseconds;
        timer.AutoReset = false;
        timer.Elapsed += IdleTimerElapsed;
        timer.Start();
    }

    private void IdleTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        logger.LogInformation("Circuit idle");
        if (CircuitIdle is not null)
        {
            CircuitIdle(this, new EventArgs());
        }
    }

    public event EventHandler? CircuitIdle;

    public bool IsIdle => !timer.Enabled;

    public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(Func<CircuitInboundActivityContext, Task> next)
    {
        return context =>
        {
            timer.Stop();
            timer.Start();
            return next(context);
        };
    }

    public void Dispose()
    {
        timer.Dispose();
    }
}

public class IdleCircuitOptions
{
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(5);
}

public static class IdleCircuitHandlerServiceCollectionExtensions
{
    public static IServiceCollection AddIdleCircuitHandler(this IServiceCollection services, Action<IdleCircuitOptions> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddIdleCircuitHandler();
        return services;
    }

    public static IServiceCollection AddIdleCircuitHandler(this IServiceCollection services)
    {
        services.AddScoped<CircuitHandler, IdleCircuitHandler>();
        services.AddScoped(services =>
        {
            var handlers = services.GetRequiredService<IEnumerable<CircuitHandler>>();
            return handlers.OfType<IdleCircuitHandler>().First();
        });
        return services;
    }
}