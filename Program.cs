using Microsoft.AspNetCore.Components.RenderTree;
using TaskManager.Services;
using TaskManager.Hubs;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);

// SERVICE REGISTRATION

//Register TaskStore
builder.Services.AddSingleton<TaskStore>();

//Overdue Task checker
builder.Services.AddHostedService<OverdueTaskChecker>();

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
