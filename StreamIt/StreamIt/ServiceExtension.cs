using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace StreamIt;

public static class ServiceExtension
{
    /// <summary>
    /// add services required by StreamIt
    /// </summary>
    /// <param name="services"></param>
    public static void AddStreamIt(this IServiceCollection services)
    {
        services.AddOptions<StreamItOptions>();
        services.AddSingleton<StreamItStorage>(_ => new StreamItStorage());
        AddDiscoveredHandlers(services);
    }

    private static void AddDiscoveredHandlers(this IServiceCollection services)
    {
        var assembly = Assembly.GetEntryAssembly();
        ArgumentNullException.ThrowIfNull(assembly);
        var type = typeof(StreamItStream);
        foreach (var handler in assembly.GetTypes().Where(t => t.IsAssignableTo(type) && t != type))
        {
            services.AddScoped(handler);
        }
    }
    
    public static WebApplication UseStreamIt(this WebApplication app)
    {
        app.Services.GetRequiredService<StreamItStorage>();
        app.UseWebSockets();
        return app;
    }

    public static RouteHandlerBuilder MapStreamIt<T>(this WebApplication app, string path)
        where T : StreamItStream
    {
        return app.MapGet(path, Task (HttpContext context, [FromServices] IServiceScopeFactory serviceProvider) =>
        {
            var handler = serviceProvider.CreateScope().ServiceProvider.GetRequiredService<T>();
            return handler.HandleConnection(context, context.RequestAborted);
        });
    }
}