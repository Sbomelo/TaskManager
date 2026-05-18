namespace TaskManager.Models;

public class CreateTaskRequest
{
    public string Title  {get; set;} = string.Empty;
    public string Description {get; set;} = string.Empty;
    public TaskPriority Priority {get; set;} = TaskPriority.Medium;
    public DateTime? DueDate {get; set;}
    public string CreatedBy {get; set;} = string.Empty;
}