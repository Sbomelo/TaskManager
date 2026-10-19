namespace TaskManager.Models
{
    public class Board
    {
        public int Id { get; set; }
        public string Slug { get; set; } = string.Empty;        // "engineering" — used in group names
        public string DisplayName { get; set; } = string.Empty; // "Engineering" — shown in the picker
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
