using System.ComponentModel.DataAnnotations;

namespace TaskManager.Models;

public class CreateTaskRequest
{
    [Required, MinLength(3), MaxLength(120)]
    public string Title  {get; set;} = string.Empty;

    [MaxLength(1000)]
    public string Description {get; set;} = string.Empty;

    public TaskPriority Priority {get; set;} = TaskPriority.Medium;
    public DateTime? DueDate {get; set;}
    public string CreatedBy {get; set;} = string.Empty;
}