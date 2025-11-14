using Sample;
using StreamIt;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHostedService<BackgroundClient>();
builder.Services.AddStreamIt();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStreamIt();
app.MapStreamIt<SampleStreamItStream>("streamit");
app.Run();