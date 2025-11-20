using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace StreamIt;

public static class ServiceExtension
{
    private static bool _isInitialized;

    /// <summary>
    /// add services required by StreamIt
    /// </summary>
    /// <param name="services"></param>
    public static void AddStreamIt(this IServiceCollection services)
    {
        if (_isInitialized)
            return;
        services.AddOptions<StreamItOptions>();
        services.AddSingleton<StreamItStorage>(_ => new StreamItStorage());
        DiscoverStreams(services);
        _isInitialized = true;
    }

    private static void DiscoverStreams(this IServiceCollection services)
    {
        var assembly = Assembly.GetEntryAssembly();
        ArgumentNullException.ThrowIfNull(assembly);
        var type = typeof(StreamItStream);
        foreach (var stream in assembly.GetTypes().Where(t => t.IsAssignableTo(type) && t != type && !t.IsAbstract))
        {
            services.AddScoped(stream);
        }
    }

    public static IApplicationBuilder UseStreamIt(this IApplicationBuilder app)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("StreamIt service not configured");
        app.UseWebSockets();
        return app;
    }

    public static RouteHandlerBuilder MapStream<T>(this IEndpointRouteBuilder app, string path)
        where T : StreamItStream
    {
        return app.MapGet(path, async Task (HttpContext context, [FromServices] IServiceProvider serviceProvider, IHostApplicationLifetime lifetime) =>
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            using var stream = scope.ServiceProvider.GetRequiredService<T>();
            await stream.HandleConnection(context, lifetime.ApplicationStopping);
        });
    }
}