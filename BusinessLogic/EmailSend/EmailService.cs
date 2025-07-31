using Microsoft.Extensions.Configuration;
using SendGrid;
using SendGrid.Helpers.Mail;
using System;

namespace BusinessLogic.EmailSend;

public class EmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    public async Task<Response> SendEmailAsync(string toEmail, string subject, string htmlContent)
    {
        var apiKey = _config["SendGrid:ApiKey"];
        var fromEmail = _config["SendGrid:FromEmail"];
        var fromName = _config["SendGrid:FromName"];

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(fromEmail))
        {
            throw new InvalidOperationException("SendGrid API ключ или email отправителя не заданы в конфигурации.");
        }

        var client = new SendGridClient(apiKey);
        var from = new EmailAddress(fromEmail, fromName ?? "No Name");
        var to = new EmailAddress(toEmail);

        var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent: "Ваш браузер не поддерживает HTML-письма.", htmlContent);
        var response = await client.SendEmailAsync(msg);
        
        Console.WriteLine($"[SendGrid] Статус: {response.StatusCode}");

        if ((int)response.StatusCode >= 400)
        {
            var errorBody = await response.Body.ReadAsStringAsync();
            Console.WriteLine("[SendGrid] Ошибка при отправке письма:");
            Console.WriteLine(errorBody);
        }

        return response;
    }
}