using Microsoft.AspNetCore.SignalR;             
using TaskManager.Hubs;                                
using TaskManager.Models;                                

using TaskStatus = TaskManager.Models.TaskStatus;        

namespace TaskManager.Services;

public class OverdueTaskChecker : BackgroundService          
{
    private readonly IHubContext<TaskHub> _hubContext;         
    private readonly TaskStore _taskStore;                  
    private readonly ILogger<OverdueTaskChecker> _logger;   

    public OverdueTaskChecker(                            
        IHubContext<TaskHub> hubContext,
        TaskStore taskStore,
        ILogger<OverdueTaskChecker> logger)
    {
        _hubContext = hubContext;
        _taskStore  = taskStore;
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

            // Waits 30 seconds before next check (respects cancellation)
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }

        _logger.LogInformation("OverdueTaskChecker stopped.");
    }

    private async Task CheckOverdueTasks()             
    {

        var now = DateTime.UtcNow;

        var overdueTasks = _taskStore           
            .GetAllTask()
            .Where(t =>
                t.DueDate.HasValue            
                && t.DueDate.Value < now      
                && t.Status != TaskStatus.Done) 
            .ToList();

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