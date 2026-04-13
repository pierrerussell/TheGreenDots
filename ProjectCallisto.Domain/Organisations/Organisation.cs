namespace ProjectCallisto.Domain.Organisations;

public class Organisation
{
    public Guid Id { get;  set; }
    public string Name  { get;  set; }
    // Id of the microsoft entra tenant
    public string TenantId  { get;  set; }
    // Id of the Active Access token used to connect to this tenant
    public Guid ActiveConnectionId  { get;  set; }
    public DateTimeOffset CreatedAt { get;  set; }
}