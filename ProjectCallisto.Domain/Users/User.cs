namespace ProjectCallisto.Domain.Users;

public class User
{
    public Guid Id { get; set; }
    public string SubjectId { get; set; } = null!;
    public string? Name { get; set; }
    public string Email { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}