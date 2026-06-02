using GestorCampo.Domain.Interfaces.Services;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace GestorCampo.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _user;
    private readonly string _password;
    private readonly string _from;
    private readonly string _appUrl;

    public EmailService(IConfiguration config)
    {
        _host = config["Email:Host"]!;
        _port = int.Parse(config["Email:Port"]!);
        _user = config["Email:User"]!;
        _password = config["Email:Password"]!;
        _from = config["Email:From"]!;
        _appUrl = (config["App:Url"] ?? string.Empty).TrimEnd('/');
    }

    private async Task SendAsync(string to, string name, string subject, string htmlBody, CancellationToken ct)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("GestorCampo", _from));
        message.To.Add(new MailboxAddress(name, to));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        using var client = new SmtpClient();
        await client.ConnectAsync(_host, _port, MailKit.Security.SecureSocketOptions.StartTlsWhenAvailable, ct);
        if (!string.IsNullOrEmpty(_user) && client.AuthenticationMechanisms.Count > 0)
            await client.AuthenticateAsync(_user, _password, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }

    public Task SendEmailVerificationAsync(string toEmail, string name, string token, CancellationToken ct = default) =>
        SendAsync(toEmail, name, "Activa tu cuenta en GestorCampo",
            $"<p>Hola {name},</p><p>Activa tu cuenta haciendo clic en el siguiente enlace:</p><p><a href='{_appUrl}/verify-email?token={token}'>Activar cuenta</a></p><p>El enlace expira en 48 horas.</p>",
            ct);

    public Task SendPasswordResetAsync(string toEmail, string name, string token, CancellationToken ct = default) =>
        SendAsync(toEmail, name, "Recupera tu contraseña en GestorCampo",
            $"<p>Hola {name},</p><p>Recibimos una solicitud para restablecer tu contraseña:</p><p><a href='{_appUrl}/reset-password?token={token}'>Restablecer contraseña</a></p><p>El enlace expira en 24 horas. Si no solicitaste esto, ignora este mensaje.</p>",
            ct);

    public Task SendOtpAsync(string toEmail, string name, string code, CancellationToken ct = default) =>
        SendAsync(toEmail, name, "Tu código de verificación - GestorCampo",
            $"<p>Hola {name},</p><p>Tu código de verificación es:</p><h2>{code}</h2><p>Expira en 5 minutos. No lo compartas con nadie.</p>",
            ct);
}
