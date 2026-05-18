using System.Collections.Concurrent;
using Microsoft.VisualBasic;
using TaskManager.Models;
using TaskStatus = TaskManager.Models.TaskStatus;

namespace TaskManager.Services;

public class TaskStore
{
    private readonly ConcurrentDictionary<string, ConcurrentBag<TaskItem>>
    _boards = new ConcurrentDictionary<string, ConcurrentBag<TaskItem>>();

    private readonly ConcurrentDictionary<string, HashSet<string>> 
    _boardViewers = new();


    //Get all tasks for a board, Returns empty if list not found
    public IEnumerable<TaskItem> GetBoardTasks (string boardId)
    {
        return _boards.TryGetValue(boardId, out var bag)
        ? bag.OrderByDescending(t => t.CreatedAt)
        : Enumerable.Empty<TaskItem>();
    }


    //Create a new task on a board. Generates server-controlled fields
    public TaskItem CreateTask(string boardId, CreateTaskRequest request)
    {
        var task = new TaskItem
        {
            Id = Guid.NewGuid().ToString(),
            BoardId = boardId,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Priority = request.Priority,
            DueDate = request.DueDate,
            Status = TaskStatus.Todo,
            AssignedTo = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        //GetorAdd : get existing bag or create and add a new one
        var bag = _boards.GetOrAdd(boardId, _ => new ConcurrentBag<TaskItem>());

        bag.Add(task);

        return task;
    }


    //Update a task's status, returns the updated task or nul if not found
    public TaskItem? UpdateStatus(string boardId, string taskId, TaskStatus newStatus)
    {
        var task = FindTask(boardId, taskId);
        if(task == null) return null;

        task.Status = newStatus;
        task.UpdatedAt = DateTime.UtcNow;

        return task;
    }


    public TaskItem? ClaimTask(String boardId, string taskId, string claimedBy)
    {
        var task = FindTask(boardId, taskId);

        //Cannot claim a task if it is already claimed or doesn't exist
        if(task == null || task.AssignedTo != null ) return null;

        task.AssignedTo = claimedBy;
        task.Status = TaskStatus.InProgress;
        task.UpdatedAt = DateTime.UtcNow;
        return task;
    }


    //GET all tasks across all boards(Used by OverDueTaskChecker)
    public IEnumerable<TaskItem> GetAllTask() 
    => _boards.Values.SelectMany(bag => bag);


    //Private Helper find task by board ID and task ID
    private TaskItem? FindTask(string boardId, string taskId)
    {
        if(!_boards.TryGetValue(boardId, out var bag)) return null;
        return bag.FirstOrDefault(t => t.Id == taskId);
    }


        public int AddViewer(string boardId, string connectionId)
    {
        var viewers = _boardViewers.GetOrAdd(boardId, _ => new HashSet<string>());
        lock (viewers) { viewers.Add(connectionId); return viewers.Count; }
    }

    public int RemoveViewer(string boardId, string connectionId)
    {
        if (_boardViewers.TryGetValue(boardId, out var viewers))
            lock (viewers) { viewers.Remove(connectionId); return viewers.Count; }
        return 0;
    }
}




