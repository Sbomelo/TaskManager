using Microsoft.AspNetCore.Components.RenderTree;
using TaskManager.Services;
using TaskManager.Hubs;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);

// Service Registration

//Register TaskStore as a singleton - one instance for the app lifetime
//Every hub method invocation and background service gets the SAME instance
builder.Services.AddSingleton<TaskStore>();

//Register SignalR with JSON enum string serialization configured
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.Converters.Add(
            new JsonStringEnumConverter());
    }
    );




var app = builder.Build();


//PHASE 2 : MIDDLEWARE PIPELINE
app.UseDefaultFiles();
app.UseStaticFiles();




app.MapHub<TaskHub>("/taskHub");

app.Run();
