using System.Text.RegularExpressions;

namespace BuildingBlocks.SharedKernel.Common;

public class CurrentUser
{
    public UserId Id { get; init; } = null!;
    public string UserName { get; init; } = null!;
    public EmailAddress Email { get; init; } = null!;
}

public sealed record UserId
{
    private UserId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty.", nameof(value));

        Value = value;
    }

    public Guid Value { get; }

    public static UserId New()
    {
        return new UserId(Guid.NewGuid());
    }

    public static UserId From(Guid value)
    {
        return new UserId(value);
    }

    public override string ToString()
    {
        return Value.ToString();
    }
}

/// <summary>
///     EmailAddress value object that encapsulates validation and normalization logic for email addresses.
///     Do not support internal email/local domains (e.g., "user@localhost") to avoid ambiguity and ensure compatibility
///     with external email systems.
/// </summary>
public sealed partial record EmailAddress
{
    // RFC 5321 limits
    private const int MaxLength = 254;
    private const int LocalPartMaxLength = 64;
    private const int DomainMaxLength = 253; // RFC 5321: 255 octets

    private EmailAddress(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public string Domain
    {
        get
        {
            var atIndex = Value.IndexOf('@');
            return Value[(atIndex + 1)..];
        }
    }

    public string LocalPart
    {
        get
        {
            var atIndex = Value.IndexOf('@');
            return Value[..atIndex];
        }
    }

    [GeneratedRegex(
        @"^[a-z0-9.!#$%&'*+/=?^_`{|}~-]+"
        + @"@"
        + @"[a-z0-9]"
        + @"(?:[a-z0-9-]{0,61}[a-z0-9])?"
        + @"(?:\.[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?)*$",
        RegexOptions.None,
        100)]
    private static partial Regex EmailRegex();

    public static EmailAddress Create(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException($"Invalid email address: {email}", nameof(email));

        email = Normalize(email);

        if (!IsValid(email))
            throw new ArgumentException($"Invalid email address: {email}", nameof(email));

        return new EmailAddress(email);
    }

    public static bool TryCreate(string? email, out EmailAddress? emailAddress)
    {
        emailAddress = null;

        if (string.IsNullOrWhiteSpace(email))
            return false;

        email = Normalize(email);

        if (!IsValid(email))
            return false;

        emailAddress = new EmailAddress(email);
        return true;
    }

    private static string Normalize(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private static bool IsValid(string email)
    {
        if (email.Length > MaxLength)
            return false;

        var atIndex = email.IndexOf('@');

        if (atIndex <= 0)
            return false;

        if (atIndex != email.LastIndexOf('@'))
            return false;

        var localPart = email[..atIndex];
        var domain = email[(atIndex + 1)..];

        if (localPart.Length > LocalPartMaxLength)
            return false;

        if (string.IsNullOrEmpty(domain) || domain.Length > DomainMaxLength)
            return false;

        if (domain.StartsWith('.') || domain.StartsWith('-')
                                   || domain.EndsWith('.') || domain.EndsWith('-'))
            return false;

        if (domain.Contains(".."))
            return false;

        if (!domain.Contains('.'))
            return false;

        return EmailRegex().IsMatch(email);
    }

    public override string ToString()
    {
        return Value;
    }

    public static implicit operator string(EmailAddress email)
    {
        return email.Value;
    }

    public static explicit operator EmailAddress(string email)
    {
        return Create(email);
    }
}