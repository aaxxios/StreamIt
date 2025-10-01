using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
    }

    /// <summary>
    /// usee StreamIt in app
    /// </summary>
    /// <param name="app"></param>
    /// <returns></returns>
    public static WebApplication UseStreamIt(this WebApplication app)
    {
        app.UseWebSockets();
        return app;
    }

    public static RouteHandlerBuilder UseStreamIt(this WebApplication app, string path, IStreamItEventHandler eventHandler)
    {
        var options = app.Services.GetRequiredService<IOptions<StreamItOptions>>();
       return app.MapGet(path, async (HttpContext context) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
                return Results.BadRequest();
            var socket = await context.WebSockets.AcceptWebSocketAsync();
            using var connectionContext = new StreamItConnectionContext(Guid.NewGuid(), socket, options);
            using var requestHandler = new StreamItRequestHandler(connectionContext, options, eventHandler, app.Services);
            await requestHandler.HandleConnection(app.Lifetime.ApplicationStopping);
            return Results.Ok();
        });
    }
}