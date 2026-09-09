using BuildingBlocks.IntegrationEvents.Contracts.Identity;
using Easebnb.Organization.Infrastructure.Database;
using Easebnb.Organization.Infrastructure.IntegrationEvents;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Easebnb.Organization.UnitTests.IntegrationEvents;

public class UserRegisteredConsumerTests : IDisposable
{
    private readonly OrganizationDbContext _dbContext;
    private readonly UserRegisteredConsumer _sut;

    public UserRegisteredConsumerTests()
    {
        var options = new DbContextOptionsBuilder<OrganizationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new OrganizationDbContext(options);
        _sut = new UserRegisteredConsumer(
            NullLogger<UserRegisteredConsumer>.Instance, _dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    private static ConsumeContext<UserRegisteredIntegrationEvent> CreateContext(
        Guid userId,
        string email,
        string? userName)
    {
        var contextMock = new Mock<ConsumeContext<UserRegisteredIntegrationEvent>>();
        contextMock.Setup(c => c.Message)
            .Returns(new UserRegisteredIntegrationEvent
            {
                UserId = userId,
                Email = email,
                UserName = userName
            });
        contextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        return contextMock.Object;
    }

    [Fact]
    public async Task Consume_WhenUserIsNotProjectedYet_InsertsRegisteredUser()
    {
        var userId = Guid.NewGuid();

        await _sut.Consume(CreateContext(userId, "user@test.com", "user"));

        var registeredUser = await _dbContext.RegisteredUsers.AsNoTracking().SingleAsync();
        registeredUser.Id.Should().Be(userId, "the Identity user id is the projection primary key");
        registeredUser.Email.Should().Be("user@test.com");
        registeredUser.UserName.Should().Be("user");
    }

    [Fact]
    public async Task Consume_WhenUserIsAlreadyProjected_UpdatesWithoutDuplicating()
    {
        var userId = Guid.NewGuid();
        await _sut.Consume(CreateContext(userId, "old@test.com", "old-name"));

        await _sut.Consume(CreateContext(userId, "new@test.com", "new-name"));

        var registeredUsers = await _dbContext.RegisteredUsers.AsNoTracking().ToListAsync();
        registeredUsers.Should().ContainSingle(
            "redelivering the same user must update the existing row, not add another");
        registeredUsers[0].Email.Should().Be("new@test.com");
        registeredUsers[0].UserName.Should().Be("new-name");
    }
}
