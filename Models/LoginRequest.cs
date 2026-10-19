namespace TaskManager.Models
{
    public record RegisterRequest(string Username, string Password, string DisplayName);
    public record LoginRequest(string Username, string Password);
    public record LoginResponse(string Token, string Name);
}
