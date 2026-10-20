const jwt = sessionStorage.getItem("jwt");
if (!jwt) location.href = "/";

let myBoardId = "";
const taskMap = new Map();

const connection = new signalR.HubConnectionBuilder()
    .withUrl("/taskHub", { accessTokenFactory: () => sessionStorage.getItem("jwt") })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .configureLogging(signalR.LogLevel.Information)
    .build();

connection.on("Connected", function () { setStatus("Connected", true); loadBoards(); });

connection.on("BoardState", function (boardId, tasks) {
    taskMap.clear();
    clearAllColumns();
    tasks.forEach(task => { taskMap.set(task.id, task); renderTaskCard(task, false); });
    updateAllCounts();
    showBoard();
});

connection.on("TaskCreated", function (task) { taskMap.set(task.id, task); renderTaskCard(task, true); updateAllCounts(); });
connection.on("TaskUpdated", function (task) { taskMap.set(task.id, task); removeTaskCard(task.id); renderTaskCard(task, false); updateAllCounts(); });
connection.on("ViewerJoined", function () { refreshViewerCount(); toast("A teammate joined the board", "info"); });
connection.on("ViewerLeft", function () { refreshViewerCount(); });
connection.on("ViewerCountUpdated", (count) => { document.getElementById("viewer-n").textContent = count; });
connection.on("Error", function (message) { toast(message, "error"); });
connection.on("OverdueAlert", function (taskTitle) { toast(` Overdue: "${taskTitle}"`, "warn"); });

connection.onreconnecting(function () { setStatus("Reconnecting...", false); toast("Connection lost — reconnecting...", "warn"); });
connection.onreconnected(async function () {
    setStatus("Connected", true);
    toast("Reconnected — refreshing board...", "success");
    if (myBoardId) await connection.invoke("JoinBoard", myBoardId);
});
connection.onclose(function () { setStatus("Disconnected — refresh to reconnect", false); });

async function startConnection() {
    try { await connection.start(); }
    catch (err) { setStatus("Connection failed — retrying...", false); setTimeout(startConnection, 5000); }
}
startConnection();

async function loadBoards() {
    const res = await fetch("/api/boards", { headers: { "Authorization": "Bearer " + jwt } });
    const boards = await res.json();
    const select = document.getElementById("board-id");
    select.innerHTML = boards.map(b => `<option value="${b.slug}">${b.displayName} (${b.viewerCount} viewing)</option>`).join("")
        + `<option value="__new__">+ Create new board</option>`;
    document.getElementById("join-btn").disabled = false;
}

document.getElementById("board-id").addEventListener("change", (e) => {
    document.getElementById("new-board-field").style.display = e.target.value === "__new__" ? "block" : "none";
});

document.getElementById("join-btn").addEventListener("click", async function () {
    let boardId = document.getElementById("board-id").value;
    if (boardId === "__new__") {
        const name = document.getElementById("new-board-name").value.trim();
        if (!name) { toast("Enter a name for the new board.", "error"); return; }
        const res = await fetch("/api/boards", {
            method: "POST",
            headers: { "Content-Type": "application/json", "Authorization": "Bearer " + jwt },
            body: JSON.stringify({ name })
        });
        if (!res.ok) { toast(await res.text(), "error"); return; }
        boardId = (await res.json()).slug;
    }
    if (!boardId) { toast("Select a board.", "error"); return; }

    myBoardId = boardId;
    document.getElementById("board-name-display").textContent = ` ${boardId} board`;
    try { await connection.invoke("JoinBoard", boardId); }
    catch (err) { toast("Failed to join board: " + err.message, "error"); }
});

document.getElementById("open-modal").onclick = () => document.getElementById("modal").classList.add("open");
document.getElementById("close-modal").onclick = () => document.getElementById("modal").classList.remove("open");

document.getElementById("submit-task").addEventListener("click", async function () {
    const title = document.getElementById("t-title").value.trim();
    const desc = document.getElementById("t-desc").value.trim();
    const pri = document.getElementById("t-pri").value;
    const due = document.getElementById("t-due").value;
    if (!title) { toast("Title is required.", "error"); return; }

    try {
        await connection.invoke("CreateTask", myBoardId, { title, description: desc, priority: pri, dueDate: due ? new Date(due).toISOString() : null });
        document.getElementById("modal").classList.remove("open");
        document.getElementById("t-title").value = "";
        document.getElementById("t-desc").value = "";
    } catch (err) { toast("Failed to create task: " + err.message, "error"); }
});

function renderTaskCard(task, animate) {
    const col = document.getElementById(`col-${task.status}`);
    if (!col) return;
    const pri = (task.priority || "Medium").toLowerCase();
    const created = new Date(task.createdAt).toLocaleTimeString();

    const card = document.createElement("div");
    card.className = `task-card p-${pri}`;
    card.dataset.id = task.id;

    let actions = "";
    if (!task.assignedTo) actions += `<button class="ta-btn claim" onclick="claimTask('${task.id}')">Claim</button>`;
    if (task.status !== "Done") {
        const nextStatus = task.status === "Todo" ? "InProgress" : "Done";
        const label = task.status === "Todo" ? "→ Start" : "✓ Complete";
        actions += `<button class="ta-btn" onclick="updateStatus('${task.id}','${nextStatus}')">${label}</button>`;
    }

    card.innerHTML = `
      <div class="task-title">${esc(task.title)}</div>
      ${task.description ? `<div class="task-desc">${esc(task.description)}</div>` : ""}
      <div class="task-meta">
        <span class="assignee">${task.assignedTo ? "👤 " + esc(task.assignedTo) : "Unassigned"}</span>
        <span>${created}</span>
      </div>
      <div class="task-actions">${actions}</div>`;

    if (animate) col.insertBefore(card, col.firstChild); else col.appendChild(card);
}

function removeTaskCard(taskId) { const el = document.querySelector(`[data-id="${taskId}"]`); if (el) el.remove(); }
function clearAllColumns() { ["col-Todo", "col-InProgress", "col-Done"].forEach(id => document.getElementById(id).innerHTML = ""); }
function updateAllCounts() {
    const tasks = [...taskMap.values()];
    document.getElementById("cnt-todo").textContent = tasks.filter(t => t.status === "Todo").length;
    document.getElementById("cnt-prog").textContent = tasks.filter(t => t.status === "InProgress").length;
    document.getElementById("cnt-done").textContent = tasks.filter(t => t.status === "Done").length;
}
async function updateStatus(taskId, newStatus) { try { await connection.invoke("UpdateTaskStatus", myBoardId, taskId, newStatus); } catch (err) { toast(err.message, "error"); } }
async function claimTask(taskId) { try { await connection.invoke("ClaimTask", myBoardId, taskId); } catch (err) { toast(err.message, "error"); } }
function showBoard() { document.getElementById("setup").style.display = "none"; document.getElementById("board").classList.add("visible"); }
function setStatus(text, live) { document.getElementById("conn-txt").textContent = text; document.getElementById("dot").classList.toggle("live", live); }
function esc(text) { const d = document.createElement("div"); d.appendChild(document.createTextNode(text || "")); return d.innerHTML; }
function toast(msg, type = "info") {
    const el = document.createElement("div"); el.className = `toast ${type}`; el.textContent = msg;
    document.getElementById("toasts").appendChild(el);
    setTimeout(() => el.remove(), 10000);
}