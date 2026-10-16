namespace SuperApp.Domain.Enums;

/// <summary>Who the account belongs to.</summary>
public enum AccountType : byte
{
    Person = 0,
    AiAgentServiceAccount = 1,
    System = 2,
}

/// <summary>Whether the account can use the platform.</summary>
public enum UserStatus : byte
{
    Active = 0,
    Suspended = 1,
    BlockedFromBookingAndOrdering = 2,
}

/// <summary>Cash-on-delivery rule for printed items: automatic (after refusals), always prepay, or never.</summary>
public enum PrepayRule : byte
{
    Automatic = 0,
    Always = 1,
    Never = 2,
}

/// <summary>A system role, an admin role made by the CEO, or an agent role.</summary>
public enum RoleKind : byte
{
    System = 0,
    AdminRole = 1,
    AgentRole = 2,
}
