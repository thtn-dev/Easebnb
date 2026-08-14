namespace BuildingBlocks.Application.Email;

public readonly struct EmailMessage(
    string to,
    string subject,
    string content)
{
    public string To { get; } = to;

    public string Subject { get; } = subject;

    public string Content { get; } = content;
}