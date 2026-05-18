using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.SignalR;
using TaskManager.Models;
using TaskManager.Services;

namespace TaskManager.Hubs;

public class TaskHub : Hub
{
    private readonly TaskStore _taskStore;

    public TaskHub(TaskStore taskStore)
    {
        _taskStore = taskStore;
    }

    //Hub Method 1: JoinBoard
    public async Task JoinBoard(string boardId)
    {
        boardId = boardId.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(boardId))
        {
            await Clients.Caller.SendAsync("Error", "Board ID cannot be empty.");
            return;
        }

        //Build the group name using the "board :"prefix convention
        string groupName = $"board:{boardId}";

        //Stores boardId on this connection for use in OnDisconnectedAsync
        Context.Items["boardId"] = boardId;

        //Adds this connection to the board's group
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        // Track viewer and get accurate count
        int viewerCount = _taskStore.AddViewer(boardId, Context.ConnectionId);

        //Get all Current tasks for this this board from the task store
        var currentTasks = _taskStore.GetBoardTasks(boardId).ToList();


        //Send ONLY to this new client the current board state 
        await Clients.Caller.SendAsync("BoardState", boardId, currentTasks);

        //Notify everyone else on this board that a new viewer has joined
        await Clients.OthersInGroup(groupName).SendAsync("ViewerJoined", Context.ConnectionId);

        //Broadcast accurate count to EVERYONE in the group
        await Clients.Group(groupName).SendAsync("ViewerCountUpdated", viewerCount);

    }

    //SignalR auto removes from groups on disconnect
    //This is for voluntary mid-session board switches
    public async Task LeaveBoard(string boardId)
    {
        boardId = boardId.ToLowerInvariant();
        string groupName = $"board:{boardId}";

        int viewerCount = _taskStore.RemoveViewer(boardId, Context.ConnectionId);

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

        //Notify everyone else that the viewer left the group
        await Clients.OthersInGroup(groupName).SendAsync("ViewerLeft", Context.ConnectionId);

        //Broadcast updated count
        await Clients.Group(groupName).SendAsync("ViewerCountUpdated", viewerCount);

        Context.Items.Remove("boardId");
    }

    public async Task CreateTask(string boardId, CreateTaskRequest request)
    {
        boardId = boardId.ToLowerInvariant();

        //Reject empty titles before touching the store
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            await Clients.Caller.SendAsync("Error", "Task title cannot be empty.");
            return;
        }

        //Delegate creation to the TaskStore
        var newTask = _taskStore.CreateTask(boardId, request);


        //Broadcast to everyone viewing this board that a new task was created
        //Include the one who created it this is their confirmation
        await Clients.Group($"board:{boardId}").SendAsync("TaskCreated", newTask);

    }


    public async Task UpdateTaskStatus(string boardId, string taskId, Models.TaskStatus newStatus)
    {
        boardId = boardId.ToLowerInvariant();

        var updateTask = _taskStore.UpdateStatus(boardId, taskId, newStatus);

        if(updateTask == null)
        {
            await Clients.Caller.SendAsync("Error", $"Task {taskId} not found");
            return;
        }


        //Broadcast the update task to the whole group
        await Clients.Group($"board:{boardId}").SendAsync("TaskUpdated", updateTask);
    }


    public async Task ClaimTask(string boardId, string taskId, string claimedBy)
    {
        boardId = boardId.ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(claimedBy))
        {
            await Clients.Caller.SendAsync("Error", "You must provide your name to claim a task");
            return;
        }

        var claimed = _taskStore.ClaimTask(boardId, taskId, claimedBy);

        if(claimed == null)
        {
            //Could be not found, OR already claimed by someone else
            await Clients.Caller.SendAsync("Error", "Could not claim task - it maybe already assigned.");
            return;
        }

        await Clients.Group($"board:{boardId}").SendAsync("TaskUpdated", claimed);
    }


    //LIFE CYCLE : OnConnectedAsync
    //Minimal here clients call JoinedBoard explicitly to join
    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync(
            "Connected", 
            Context.ConnectionId, 
            DateTime.UtcNow.ToString("o"));

        await base.OnConnectedAsync();
    }

    //LIFECYCLE : OnDisconnectedAsync
    //Groups are auto-cleaned we just need to notify others
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        //Receive with board this connection was viewing
        if(Context.Items.TryGetValue("boardId", out var boardIdObj) 
                && boardIdObj is string boardId 
                && !string.IsNullOrWhiteSpace(boardId))
        {
            string groupName = $"board:{boardId}";

            //Removes viewer and broadcast updated count
            int viewerCount = _taskStore.RemoveViewer(boardId, Context.ConnectionId);


            //Tell remaining board viewers this connection left
            await Clients.OthersInGroup(groupName).SendAsync("ViewerLeft", Context.ConnectionId);
            await Clients.Group(groupName).SendAsync("ViewerCountUpdated", viewerCount);

            //NOTES : Groups.RemoveFromGroupsAsync is Not needed here
            //SignalR removes disconnected connections from all groups automatically
        }

        


        await base.OnDisconnectedAsync(exception);
    }
}