using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TaskManager.Data;
using TaskManager.Models;
using TaskManager.Services;
using TaskStatus = TaskManager.Models.TaskStatus;

namespace TaskManager.Hubs;

[Authorize]
public class TaskHub : Hub
{
    private readonly AppDbContext _db;
    private readonly TaskStore _viewers;

    public TaskHub(AppDbContext db, TaskStore viewers)
    {
        _db = db;
        _viewers = viewers;
    }

    public async Task JoinBoard(string boardId)
    {
        boardId = boardId.Trim().ToLowerInvariant();

        var board = await _db.Boards.AsNoTracking().FirstOrDefaultAsync(b => b.Slug == boardId);
        if (board is null)
        {
            await Clients.Caller.SendAsync("Error", $"Board '{boardId}' does not exist.");
            return;
        }

        string groupName = $"board:{boardId}";
        Context.Items["boardId"] = boardId;
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        int viewerCount = _viewers.AddViewer(boardId, Context.ConnectionId);
        var currentTasks = await _db.Tasks.AsNoTracking().Where(t => t.BoardId == boardId).ToListAsync();

        await Clients.Caller.SendAsync("BoardState", boardId, currentTasks);
        await Clients.OthersInGroup(groupName).SendAsync("ViewerJoined", Context.ConnectionId);
        await Clients.Group(groupName).SendAsync("ViewerCountUpdated", viewerCount);
    }

    public async Task LeaveBoard(string boardId)
    {
        boardId = boardId.ToLowerInvariant();
        string groupName = $"board:{boardId}";

        int viewerCount = _viewers.RemoveViewer(boardId, Context.ConnectionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        await Clients.OthersInGroup(groupName).SendAsync("ViewerLeft", Context.ConnectionId);
        await Clients.Group(groupName).SendAsync("ViewerCountUpdated", viewerCount);
        Context.Items.Remove("boardId");
    }

    public async Task CreateTask(string boardId, CreateTaskRequest request)
    {
        boardId = boardId.ToLowerInvariant();

        // SignalR doesn't auto-validate DataAnnotations the way [ApiController] does so we enforce them here too.
        var title = request.Title?.Trim() ?? "";
        if (title.Length < 3 || title.Length > 120)
        {
            await Clients.Caller.SendAsync("Error", "Title must be between 3 and 120 characters.");
            return;
        }
        var description = (request.Description ?? "").Trim();
        if (description.Length > 1000)
        {
            await Clients.Caller.SendAsync("Error", "Description is too long (max 1000 characters).");
            return;
        }

        var task = new TaskItem
        {
            BoardId = boardId,
            Title = title,
            Description = description,
            Priority = request.Priority,
            DueDate = request.DueDate,
            Status = TaskStatus.Todo,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Tasks.Add(task);
        await _db.SaveChangesAsync();
        await Clients.Group($"board:{boardId}").SendAsync("TaskCreated", task);
    }

    public async Task UpdateTaskStatus(string boardId, string taskId, TaskStatus newStatus)
    {
        boardId = boardId.ToLowerInvariant();

        var rows = await _db.Tasks
            .Where(t => t.Id == taskId && t.BoardId == boardId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Status, newStatus)
                .SetProperty(t => t.UpdatedAt, DateTime.UtcNow));

        if (rows == 0)
        {
            await Clients.Caller.SendAsync("Error", $"Task {taskId} not found");
            return;
        }

        var updated = await _db.Tasks.AsNoTracking().FirstAsync(t => t.Id == taskId);
        await Clients.Group($"board:{boardId}").SendAsync("TaskUpdated", updated);
    }

    public async Task ClaimTask(string boardId, string taskId)
    {
        boardId = boardId.ToLowerInvariant();

        var claimedBy = Context.User?.Identity?.Name ?? "Unknown";

        var rows = await _db.Tasks
            .Where(t => t.Id == taskId && t.BoardId == boardId && t.AssignedTo == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.AssignedTo, claimedBy)
                .SetProperty(t => t.Status, TaskStatus.InProgress)
                .SetProperty(t => t.UpdatedAt, DateTime.UtcNow));

        if (rows == 0)
        {
            await Clients.Caller.SendAsync("Error", "Could not claim task — it may already be assigned.");
            return;
        }

        var claimed = await _db.Tasks.AsNoTracking().FirstAsync(t => t.Id == taskId);
        await Clients.Group($"board:{boardId}").SendAsync("TaskUpdated", claimed);
    }

    public override async Task OnConnectedAsync()
    {
        var userName = Context.User?.Identity?.Name ?? "Unknown";
        await Clients.Caller.SendAsync("Connected", Context.ConnectionId, userName, DateTime.UtcNow.ToString("o"));
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.TryGetValue("boardId", out var boardIdObj) && boardIdObj is string boardId && !string.IsNullOrWhiteSpace(boardId))
        {
            string groupName = $"board:{boardId}";
            int viewerCount = _viewers.RemoveViewer(boardId, Context.ConnectionId);
            await Clients.OthersInGroup(groupName).SendAsync("ViewerLeft", Context.ConnectionId);
            await Clients.Group(groupName).SendAsync("ViewerCountUpdated", viewerCount);
        }
        await base.OnDisconnectedAsync(exception);
    }
}