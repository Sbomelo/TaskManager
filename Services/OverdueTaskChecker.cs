using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TaskManager.Data;
using TaskManager.Hubs;                                
using TaskManager.Models;                                

using TaskStatus = TaskManager.Models.TaskStatus;        

namespace TaskManager.Services;

public class OverdueTaskChecker : BackgroundService          
{
    private readonly IHubContext<TaskHub> _hubContext;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OverdueTaskChecker> _logger;   

    public OverdueTaskChecker(                            
        IHubContext<TaskHub> hubContext,
         IServiceScopeFactory scopeFactory,
        ILogger<OverdueTaskChecker> logger)
    {
        _hubContext = hubContext;
        _scopeFactory = scopeFactory;
        _logger     = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)  
    {
        _logger.LogInformation("OverdueTaskChecker started."); 

        // Keeps running until the application shuts down
        while (!stoppingToken.IsCancellationRequested)         
        {
            try
            {
                await CheckOverdueTasks();                   
            }
            catch (Exception ex)                           
            {
                
                _logger.LogError(ex, "Error during overdue task check");
            }

            // Waits 10 seconds before next check (respects cancellation)
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }

        _logger.LogInformation("OverdueTaskChecker stopped.");
    }

    private async Task CheckOverdueTasks()             
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;

        var overdueTasks = await db.Tasks.AsNoTracking()
           .Where(t => t.DueDate.HasValue && t.DueDate.Value < now && t.Status != TaskStatus.Done)
           .ToListAsync();

        foreach (var task in overdueTasks)               
        {
            string groupName = $"board:{task.BoardId}";

            await _hubContext.Clients                   
                .Group(groupName)
                .SendAsync("OverdueAlert", task.Title, task.BoardId);

            _logger.LogWarning(
                "Overdue alert sent for task '{Title}' on board '{Board}'",
                task.Title, task.BoardId);
        }
    }
}