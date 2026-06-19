let myName     = "";             // Set when user fills setup screen   
let myBoardId  = "";             // The board this client has joined            
const taskMap  = new Map();      // taskId → task object (our local state)   

// ── BUILD CONNECTION
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/taskHub")                                  
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])         
    .configureLogging(signalR.LogLevel.Information)
    .build();


// Server confirmed our WebSocket connection
connection.on("Connected", function(connectionId, serverTime) {    
    setStatus("Connected", true);
    // Enable the Join button now that the connection is live
    document.getElementById("join-btn").disabled = false;              
});

// Server sends the complete current board state when we join
connection.on("BoardState", function(boardId, tasks) {               
    taskMap.clear();                                                 
    clearAllColumns();                                               
    tasks.forEach(task => {                                               
        taskMap.set(task.id, task);
        renderTaskCard(task, false);                                       
    });
    updateAllCounts();
    showBoard();                                                           
});

// Another user created a new task — add it to our board
connection.on("TaskCreated", function(task) {                            
    taskMap.set(task.id, task);
    renderTaskCard(task, true);                                           
    updateAllCounts();
});

// A task was updated (status change, claim, etc.) — replace its card
connection.on("TaskUpdated", function(task) {                            
    taskMap.set(task.id, task);                                          
    removeTaskCard(task.id);                                              
    renderTaskCard(task, false);                                          
    updateAllCounts();
});

// A viewer joined our board — increment count
connection.on("ViewerJoined", function(connectionId) {                    
    //viewerCount++;
    refreshViewerCount();
    toast("A teammate joined the board", "info");
});

// A viewer left our board — decrement count
connection.on("ViewerLeft", function(connectionId) {                   
   // viewerCount = Math.max(0, viewerCount - 1);
    refreshViewerCount();
});
//Counter handled by the server for accuracy, so we just refresh the displayed count when notified of changes
connection.on("ViewerCountUpdated", (count) => {
    document.getElementById("viewer-n").textContent = count;
});

// Server sent an error (validation failure, task not found, etc.)
connection.on("Error", function(message) {                             
    toast(message, "error");
});

// Server sent an overdue task warning (from OverdueTaskChecker service)
connection.on("OverdueAlert", function(taskTitle, boardId) {           
    toast(`⏰ Overdue: "${taskTitle}"`, "warn");
});

// ── RECONNECTION HANDLERS ── (See Step 7 for full explanation)
connection.onreconnecting(function() {                                    
    setStatus("Reconnecting...", false);
    toast("Connection lost — reconnecting...", "warn");
});

connection.onreconnected(async function(newConnectionId) {                 
    setStatus("Connected", true);
    toast("Reconnected — refreshing board...", "success");
    // Critical: re-join the group with the new connection ID
    if (myBoardId) {
        await connection.invoke("JoinBoard", myBoardId);              
    }
});

connection.onclose(function() {                                       
    setStatus("Disconnected — refresh to reconnect", false);
});

// ── START THE CONNECTION ─────
async function startConnection() {
    try {
        await connection.start();                              
    } catch (err) {
        setStatus("Connection failed — retrying...", false);
        setTimeout(startConnection, 5000);
    }
}
startConnection();

// ── JOIN BOARD ─────────
document.getElementById("join-btn").addEventListener("click", async function() {
    const name    = document.getElementById("my-name").value.trim();
    const boardId = document.getElementById("board-id").value.trim().toLowerCase();

    if (!name || !boardId) { toast("Please fill in both fields.", "error"); return; }

    myName    = name;
    myBoardId = boardId;                                          

    document.getElementById("board-name-display").textContent =
        `📋 ${boardId} board`;
    //viewerCount = 1;                                              
    //refreshViewerCount();

    try {
        await connection.invoke("JoinBoard", boardId);                     
        // showBoard() is called by the BoardState handler, not here
    } catch (err) {
        toast("Failed to join board: " + err.message, "error");
    }
});

// ── MODAL (Add Task) ────
document.getElementById("open-modal").onclick =  () =>
    document.getElementById("modal").classList.add("open");
document.getElementById("close-modal").onclick = () =>
    document.getElementById("modal").classList.remove("open");

document.getElementById("submit-task").addEventListener("click", async function() {
    const title   = document.getElementById("t-title").value.trim();
    const desc    = document.getElementById("t-desc").value.trim();
    const pri     = document.getElementById("t-pri").value;
    const due     = document.getElementById("t-due").value;
    if (!title) { toast("Title is required.", "error"); return; }

    const request = {
        title,
        description: desc,
        priority: pri,
        dueDate: due ? new Date(due).toISOString() : null,           
        createdBy: myName
    };

    try {
        await connection.invoke("CreateTask", myBoardId, request);       
        document.getElementById("modal").classList.remove("open");
        document.getElementById("t-title").value = "";
        document.getElementById("t-desc").value  = "";
    } catch (err) {
        toast("Failed to create task: " + err.message, "error");
    }
});

// ── TASK CARD RENDERING ──────────
function renderTaskCard(task, animate) {                                
    const col     = document.getElementById(`col-${task.status}`);          
    if (!col) return;

    const pri     = (task.priority || "Medium").toLowerCase();
    const isOwner = task.assignedTo === myName;                  
    const created = new Date(task.createdAt).toLocaleTimeString();

    const card = document.createElement("div");
    card.className = `task-card p-${pri}`;
    card.dataset.id = task.id;                              

    // Build action buttons based on task state
    let actions = "";
    if (!task.assignedTo) {
        actions += `<button class="ta-btn claim" onclick="claimTask('${task.id}')">Claim</button>`; 
    }
    if (task.status !== "Done") {
        const nextStatus = task.status === "Todo" ? "InProgress" : "Done";
        const label      = task.status === "Todo" ? "→ Start" : "✓ Complete";
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

    if (animate) {
        col.insertBefore(card, col.firstChild);                      
    } else {
        col.appendChild(card);                                            
    }
}

function removeTaskCard(taskId) {                                   
    const existing = document.querySelector(`[data-id="${taskId}"]`);
    if (existing) existing.remove();
}

function clearAllColumns() {
    ["col-Todo", "col-InProgress", "col-Done"].forEach(id => {
        document.getElementById(id).innerHTML = "";
    });
}

function updateAllCounts() {
    const tasks = [...taskMap.values()];
    document.getElementById("cnt-todo").textContent = tasks.filter(t => t.status === "Todo").length;
    document.getElementById("cnt-prog").textContent = tasks.filter(t => t.status === "InProgress").length;
    document.getElementById("cnt-done").textContent = tasks.filter(t => t.status === "Done").length;
}

// ── ACTIONS (called from button onclick attributes) ───────
async function updateStatus(taskId, newStatus) {
    try {
        await connection.invoke("UpdateTaskStatus", myBoardId, taskId, newStatus);  
    } catch (err) { toast(err.message, "error"); }
}

async function claimTask(taskId) {
    if (!myName) { toast("Set your name first.", "error"); return; }
    try {
        await connection.invoke("ClaimTask", myBoardId, taskId, myName);       
    } catch (err) { toast(err.message, "error"); }
}

// ── HELPERS ─
function showBoard() {
    document.getElementById("setup").style.display = "none";
    document.getElementById("board").classList.add("visible");
}
function setStatus(text, live) {
    document.getElementById("conn-txt").textContent = text;
    document.getElementById("dot").classList.toggle("live", live);
}
/*function refreshViewerCount() {
    document.getElementById("viewer-n").textContent = viewerCount;
}*/
function esc(text) {
    const d = document.createElement("div");
    d.appendChild(document.createTextNode(text || ""));
    return d.innerHTML;
}
function toast(msg, type = "info") {
    const el = document.createElement("div");
    el.className = `toast ${type}`;
    el.textContent = msg;
    document.getElementById("toasts").appendChild(el);
    setTimeout(() => el.remove(), 10000);                                // Toast auto-removes after 10s
}