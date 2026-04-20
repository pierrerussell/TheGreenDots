using ProjectCallisto.Domain.Users;

namespace ProjectCallisto.Domain.Organisations;

public class OrganisationUser
{
    private OrganisationUser() {}

    public OrganisationUser(Guid organisationId, Guid userId, OrganisationRole role)
    {
        OrganisationId = organisationId;
        UserId = userId;
        Role = role;
    }
    
    public Guid OrganisationId { get; set; }
    public OrganisationRole Role { get; set; }
    public Guid UserId { get; set; }

    public User User { get; set; } = null!;
    public Organisation Organisation { get; set; } = null!;
}

public enum OrganisationRole
{
    Admin,
    Member
}

