namespace SuperApp.Application.Security;

/// <summary>Names of the roles the seed creates. System roles cannot be deleted (409); the CEO role cannot be edited.</summary>
public static class SystemRoles
{
    public const string Ceo = "CEO";
    public const string AdminManager = "AdminManager";
    public const string Support = "Support";
    public const string Finance = "Finance";
    public const string Content = "Content";
    public const string Marketing = "Marketing";
    public const string Moderator = "Moderator";
    public const string AgentBase = "AgentBase";
    public const string Customer = "Customer";
    public const string PitchOwner = "PitchOwner";
    public const string Seller = "Seller";
    public const string FactoryOwner = "FactoryOwner";
    public const string FactoryStaff = "FactoryStaff";
}
