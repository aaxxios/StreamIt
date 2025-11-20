using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

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
        if (!_isInitialized)
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
            var routeAttribute = type.GetCustomAttribute<StreamRouteAttribute>();
            if (routeAttribute == null)
                continue;
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
        return app.MapGet(path, Task (HttpContext context, [FromServices] IServiceScopeFactory serviceProvider) =>
        {
            using var scope = serviceProvider.CreateScope();
            var stream = scope.ServiceProvider.GetRequiredService<T>();
            return stream.HandleConnection(context, context.RequestAborted);
        });
    }
}