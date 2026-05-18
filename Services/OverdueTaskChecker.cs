using Microsoft.AspNetCore.SignalR;             
using TaskManager.Hubs;                                
using TaskManager.Models;                                

// Note: TaskStatus is in TaskManager.Models. We alias it to avoid
// any ambiguity with System.Threading.Tasks types.
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
                // Log but DON'T re-throw — crashing the loop kills the service
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

        // Find all tasks that have a due date, are in the past, and aren't done
        var overdueTasks = _taskStore           
            .GetAllTask()
            .Where(t =>
                t.DueDate.HasValue              // Has a due date set
                && t.DueDate.Value < now       // Due date is on the past
                && t.Status != TaskStatus.Done) 
            .ToList();

        foreach (var task in overdueTasks)               
        {
            string groupName = $"board:{task.BoardId}";

            // Push the alert to ONLY the group for this task's board
            await _hubContext.Clients                   
                .Group(groupName)
                .SendAsync("OverdueAlert", task.Title, task.BoardId);

            _logger.LogWarning(
                "Overdue alert sent for task '{Title}' on board '{Board}'",
                task.Title, task.BoardId);
        }
    }
}