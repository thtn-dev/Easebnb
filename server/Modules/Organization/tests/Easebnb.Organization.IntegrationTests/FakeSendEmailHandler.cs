using Easebnb.Identity.Infrastructure.Services;
using MediatR;

namespace Easebnb.Organization.IntegrationTests;

/// <summary>
///     Replaces <see cref="SendMailHandler"/> (which simulates sending with a
///     hard-coded 10s delay) so registration flows complete instantly and the
///     emitted <see cref="SendEmailEvent"/>s can be asserted.
/// </summary>
public sealed class FakeSendEmailHandler : INotificationHandler<SendEmailEvent>
{
    private readonly Lock _lock = new();
    private readonly List<SendEmailEvent> _events = [];

    public IReadOnlyList<SendEmailEvent> Events
    {
        get
        {
            lock (_lock)
            {
                return _events.ToArray();
            }
        }
    }

    public Task Handle(SendEmailEvent notification, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _events.Add(notification);
        }

        return Task.CompletedTask;
    }

    public void Reset()
    {
        lock (_lock)
        {
            _events.Clear();
        }
    }
}
