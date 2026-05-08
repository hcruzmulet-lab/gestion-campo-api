namespace GestorCampo.Domain.Interfaces.Services;

public interface IEmailService
{
    Task SendEmailVerificationAsync(string toEmail, string name, string token, CancellationToken ct = default);
    Task SendPasswordResetAsync(string toEmail, string name, string token, CancellationToken ct = default);
    Task SendOtpAsync(string toEmail, string name, string code, CancellationToken ct = default);
}
