namespace TaskManager.Models;

public enum TaskStatus
{
    Todo,
    InProgress,
    Done
}

public enum TaskPriority
{
    Low,
    Medium,
    High
}

public class TaskItem
{
    public string     Id          { get; set; } = string.Empty;
    public string     BoardId     { get; set; } = string.Empty; 
    public string     Title       { get; set; } = string.Empty;
    public string     Description { get; set; } = string.Empty;
    public TaskStatus   Status    { get; set; } = TaskStatus.Todo;
    public TaskPriority Priority  { get; set; } = TaskPriority.Medium; 
    public string?    AssignedTo  { get; set; }                 
    public DateTime   CreatedAt   { get; set; }            
    public DateTime?  DueDate     { get; set; }                
    public DateTime   UpdatedAt   { get; set; }
}