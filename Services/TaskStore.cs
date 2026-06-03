using System.Collections.Concurrent;
using Microsoft.VisualBasic;
using TaskManager.Models;
using TaskStatus = TaskManager.Models.TaskStatus;

namespace TaskManager.Services;

public class TaskStore
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, TaskItem>> _boardTasks = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _boardViewers = new();

    public IEnumerable<TaskItem> GetBoardTasks (string boardId)
    {
        /*return _boardTasks.TryGetValue(boardId, out var innerDict)
                ? innerDict.Values
                : Enumerable.Empty<TaskItem>();*/

        if (_boardTasks.TryGetValue(boardId, out var innerDict))
        {
            return innerDict.Values;
        } 
        else
        {
            return Enumerable.Empty<TaskItem>();
        }
    }

    public TaskItem CreateTask (string boardId, CreateTaskRequest request)
    {
        var task = new TaskItem                       
        {
            Id          = Guid.NewGuid().ToString(),           
            BoardId     = boardId,                           
            Title       = request.Title.Trim(),                
            Description = request.Description.Trim(),         
            Priority    = request.Priority,                  
            DueDate     = request.DueDate,                    
            Status      = TaskStatus.Todo,                      
            AssignedTo  = null,                                  
            CreatedAt   = DateTime.UtcNow,                       
            UpdatedAt   = DateTime.UtcNow                    
        };

        //Get an innerDictionary for this BoardId or create a new one if it does not exist
        var innerDict = _boardTasks.GetOrAdd(boardId, _=> new ConcurrentDictionary<string, TaskItem>());

        //TryAdd on the innerDictionary, task.Id is the key, task object is the value
        innerDict.TryAdd(task.Id, task);

        return task;
    }

    public TaskItem? UpdateStatus(string boardId, string taskId, Models.TaskStatus newStatus)
    {
        //Lookup 1, find the boards inner dictionary
        if(!_boardTasks.TryGetValue(boardId, out var innerDict))
            return null;
        
        //Lookup 2,inner dictionary find the task by task it
        if(!innerDict.TryGetValue(taskId, out var task))
            return null;

        task.Status = newStatus;
        return task;
    }

    public TaskItem? ClaimTask (string boardId, string taskId, string claimedBy)
    {
        //Look up 1 ,outer dictionary find the dictionary of the board
        if(!_boardTasks.TryGetValue(boardId , out var innerDict))
            return null;
        //Lookup 2, inner dictionary find the task by task it in inner dictionary 
        if(!innerDict.TryGetValue(taskId, out var task))
            return null;

        lock (task)
        {
            //check if the task is already claimed
            if(task.AssignedTo != null)
                return null;

            task.AssignedTo = claimedBy;
            task.Status = TaskStatus.InProgress;
        }
        return task;
    }

    // Get all tasks across ALL boards (used by OverdueTaskChecker).
    public IEnumerable<TaskItem> GetAllTask()
    {
       return _boardTasks.Values
                         .SelectMany(innerDict => innerDict.Values);
    }              
                   
    // Private helper: find a task by board ID and task ID.
    private TaskItem? FindTask(string boardId, string taskId)     
    {
        if (!_boardTasks.TryGetValue(boardId, out var innerDict)) 
            return null; 

        if(!innerDict.TryGetValue(taskId, out var task))
            return null;
        return task;     
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




