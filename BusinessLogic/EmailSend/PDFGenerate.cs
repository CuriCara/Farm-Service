using DataAccess;
using DinkToPdf;
using DinkToPdf.Contracts;
using Microsoft.EntityFrameworkCore;

public class PDFGenerate
{
    private readonly FarmDbContext _db;
    private readonly IConverter _converter;

    public PDFGenerate(FarmDbContext db, IConverter converter)
    {
        _db = db;
        _converter = converter;
    }

    public async Task GenerateDailyReportAsync()
    {
        var today = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);

        var harvests = await _db.Harvests
            .Where(h => h.DateHarvest >= today && h.DateHarvest < today.AddDays(1))
            .Include(h => h.Product)
            .ToListAsync();

        var htmlContent = $"""
                           <h1>Отчет за {DateTime.Today:dd.MM.yyyy}</h1>
                           <table border="1" cellspacing="0" cellpadding="5">
                               <tr><th>Продукт</th><th>Количество</th></tr>
                               {string.Join("", harvests.Select(h => $"<tr><td>{h.Product?.ProductName}</td><td>{h.Quantity}</td></tr>"))}
                           </table>
                           """;

        var doc = new HtmlToPdfDocument()
        {
            GlobalSettings = new GlobalSettings
            {
                PaperSize = PaperKind.A4,
                Orientation = Orientation.Portrait,
                DocumentTitle = "Daily Report",
                Out = @"C:\Users\dark7\OneDrive\Рабочий стол\report.pdf"
            },
            Objects = {
                new ObjectSettings
                {
                    HtmlContent = htmlContent,
                    WebSettings = { DefaultEncoding = "utf-8" }
                }
            }
        };

        _converter.Convert(doc); // создаёт PDF
    }
}