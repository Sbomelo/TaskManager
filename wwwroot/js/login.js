async function login() {
    const res = await fetch("/auth/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ username: document.getElementById("u").value, password: document.getElementById("p").value })
    });
    if (!res.ok) { document.getElementById("err").style.display = "block"; return; }
    const data = await res.json();
    sessionStorage.setItem("jwt", data.token);
    sessionStorage.setItem("name", data.name);
    location.href = "board.html";
}

async function register() {
    const res = await fetch("/auth/register", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
            username: document.getElementById("u").value,
            password: document.getElementById("p").value,
            displayName: document.getElementById("u").value
        })
    });
    if (!res.ok) {
        document.getElementById("err").textContent = "Username already taken.";
        document.getElementById("err").style.display = "block";
        return;
    }
    await login();
}

document.getElementById("login").addEventListener("click", login);
document.getElementById("register").addEventListener("click", register);