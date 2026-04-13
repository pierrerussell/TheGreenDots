namespace ProjectCallisto.Domain.Users;

public class User
{
    public Guid  Id { get; set; }
    public string SubjectId { get; set; }
    public string? Name { get; set; }
    public string Email { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}