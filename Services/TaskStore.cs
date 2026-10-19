using System.Collections.Concurrent;
using TaskManager.Models;
namespace TaskManager.Services;

public class TaskStore
{
    private readonly ConcurrentDictionary<string, HashSet<string>> _boardViewers = new();

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

    public int GetViewerCount(string boardId) =>
        _boardViewers.TryGetValue(boardId, out var viewers) ? viewers.Count : 0;

}




