using DataAccess;
using Microsoft.EntityFrameworkCore;
using Service;

namespace BusinessLogic.EmailSend;

public class ReportService
{
        private readonly FarmDbContext _db;
        private readonly EmailService _email;

        public ReportService(FarmDbContext db, EmailService email)
        {
            _db = db;
            _email = email;
        }

        public async Task GenerateDailyReportAsync()
        {
            var today = DateTime.UtcNow.Date;

            var harvests = await _db.Harvests
                .Where(h => h.DateHarvest >= today && h.DateHarvest < today.AddDays(1))
                .Include(h => h.Product)
                .ToListAsync();


            var htmlContent = $"""
                               <h1>Отчет за {DateTime.Today:dd.MM.yyyy}</h1>
                               <table border="1">
                                   <tr><th>Продукт</th><th>Количество</th></tr>
                                   {string.Join("", harvests.Select(h => $"<tr><td>{h.Product}</td><td>{h.Quantity}</td></tr>"))}
                               </table>
                               """;

            await _email.SendEmailAsync("dark77_77@mail.ru", "Ежедневный отчет", htmlContent);
        }
}