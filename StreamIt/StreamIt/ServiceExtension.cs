using System.Net.WebSockets;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace StreamIt;

public static class ServiceExtension
{
    public static void AddStreamIt(this IServiceCollection services)
    {
        var ass = Assembly.GetExecutingAssembly();
        foreach (var type in ass.GetTypes().Where(x => x == typeof(StreamItRequestHandler)))
        {
            services.AddScoped(type);
        }

        services.AddSingleton<StreamItStorage>();
    }
    
    
    public static void UseStreamIt<T>(this WebApplication app, string path, T handler) where T: StreamItRequestHandler
    {
        var options = app.Services.GetRequiredService<IOptions<StreamItOptions>>();
        app.MapGet(path, async (HttpContext context) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
                return Results.BadRequest();
            var socket = await context.WebSockets.AcceptWebSocketAsync() as ClientWebSocket;
            ArgumentNullException.ThrowIfNull(socket);
            var ctx = new StreamItConnectionContext(Guid.NewGuid(), socket, options.Value);
            await handler.HandleConnection(ctx, app.Lifetime.ApplicationStopping);
            return Results.Ok();
        });
    }
}