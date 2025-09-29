using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace StreamIt;

public static class ServiceExtension
{
    public static void AddStreamIt(this IServiceCollection services)
    {
        services.AddOptions<StreamItOptions>();
    }

    public static void UseStreamIt(this WebApplication app, string path, IStreamItEventHandler eventHandler)
    {
        app.UseWebSockets();
        var options = app.Services.GetRequiredService<IOptions<StreamItOptions>>();
        app.MapGet(path, async (HttpContext context) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
                return Results.BadRequest();
            var socket = await context.WebSockets.AcceptWebSocketAsync();
            using var connectionContext = new StreamItConnectionContext(Guid.NewGuid(), socket, options.Value);
            using var requestHandler = new StreamItRequestHandler(connectionContext, options, eventHandler, app.Services);
            await requestHandler.HandleConnection(app.Lifetime.ApplicationStopping);
            return Results.Ok();
        });
    }
}