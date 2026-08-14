namespace BuildingBlocks.Application;

public sealed class CurrentUser
{
    public UserId Id { get; init; } = null!;
    public string UserName { get; init; } = null!;
    public EmailAddress Email { get; init; } = null!;
}

public record UserId(Guid Value)
{
    public override string ToString()
    {
        return Value.ToString();
    }

    public static UserId New()
    {
        return new UserId(Guid.NewGuid());
    }
}

public sealed record EmailAddress(string Value);