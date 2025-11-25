using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace StreamIt;

public static class ServiceExtension
{
    private static bool _isInitialized;
    private static readonly Type streamType = typeof(StreamItStream);

    /// <summary>
    /// add services required by StreamIt
    /// </summary>
    /// <param name="services"></param>
    public static IServiceCollection AddStreamIt(this IServiceCollection services)
    {
        return AddStreamIt(services, _ => { });
    }

    public static IServiceCollection AddStreamIt(this IServiceCollection services,
        Action<StreamItOptions> configureOptions)
    {
        if (_isInitialized)
            return services;
        var streams = DiscoverStreams();
        foreach (var stream in streams)
        {
            services.AddScoped(stream);
        }
        services.AddOptions<StreamItOptions>().PostConfigure(configureOptions);
        var storage = new StreamItStorage();
        services.AddSingleton<StreamItStorage>(_ => storage);
        _isInitialized = true;
        return services;
    }


    private static IEnumerable<Type> DiscoverStreams()
    {
        var assembly = Assembly.GetEntryAssembly();
        ArgumentNullException.ThrowIfNull(assembly);
        return assembly.GetTypes().Where(IsStream);
    }

    private static bool IsStream(Type typeInfo)
    {
        return typeInfo.IsAssignableTo(streamType) && typeInfo is { IsClass: true, IsAbstract: false, IsPublic: true };
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
        return app.MapGet(path,
            async Task (HttpContext context, IServiceProvider serviceProvider,
                IHostApplicationLifetime lifetime) =>
            {
                await using var scope = serviceProvider.CreateAsyncScope();
                using var stream = scope.ServiceProvider.GetRequiredService<T>();
                await stream.HandleConnection(context, lifetime.ApplicationStopping);
            });
    }
}