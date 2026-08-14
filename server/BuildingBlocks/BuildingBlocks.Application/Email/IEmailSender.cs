namespace BuildingBlocks.Application.Email;

public interface IEmailSender
{
    Task SendEmail(EmailMessage message);
}