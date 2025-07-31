using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Authorization;
using AutoMapper;
using BusinessLogic.Harvests.Manager;
using BusinessLogic.Harvests.Model;
using BusinessLogic.EmailSend;
using DataAccess.Entity;
using DinkToPdf;
using DinkToPdf.Contracts;
using System.Net;
using BusinessLogic.Harvests.Provider;
using Microsoft.AspNetCore.Identity;


[Authorize(Roles = "Admin")]
public class DailyModel : PageModel
{
    private readonly HarvestManager _harvestManager;
    private readonly UserProvider _userManager;
    private readonly IMapper _mapper;
    private readonly EmailService _emailService;
    private readonly IConverter _pdfConverter;

    public DailyModel(
        HarvestManager harvestManager,
        UserProvider userRepository,
        IMapper mapper,
        EmailService emailService,
        IConverter pdfConverter)
    {
        _harvestManager = harvestManager;
        _userManager = userRepository;
        _mapper = mapper;
        _emailService = emailService;
        _pdfConverter = pdfConverter;
    }

    [BindProperty(SupportsGet = true)]
    public DailyFilterModel Filter { get; set; } = new();

    public string HtmlReportContent { get; set; } = "";

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadUsersAsync();
        await LoadFilteredReportAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostFormAsync()
    {
        await LoadUsersAsync();
        await LoadFilteredReportAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadUsersAsync();
        await LoadFilteredReportAsync();

        var response = await _emailService.SendEmailAsync("example@mail.com", "Farm Report", HtmlReportContent);
        TempData["Message"] = response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Accepted
            ? "Письмо успешно отправлено!"
            : $"Ошибка: {response.StatusCode} - {await response.Body.ReadAsStringAsync()}";

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDownloadAsync()
    {
        await LoadUsersAsync();
        await LoadFilteredReportAsync();

        var pdfDoc = new HtmlToPdfDocument
        {
            GlobalSettings = new GlobalSettings
            {
                PaperSize = PaperKind.A4,
                Orientation = Orientation.Portrait,
                DocumentTitle = "Farm Daily Report"
            },
            Objects =
            {
                new ObjectSettings
                {
                    HtmlContent = HtmlReportContent,
                    WebSettings = { DefaultEncoding = "utf-8" }
                }
            }
        };

        byte[] pdfBytes = _pdfConverter.Convert(pdfDoc);
        return File(pdfBytes, "application/pdf", $"report_{DateTime.UtcNow:yyyyMMdd}.pdf");
    }

    private async Task LoadUsersAsync()
    {
        var users = await _userManager.GetAllAsync();
        Filter.Users = users
            .Select(u => new SelectListItem { Value = u.Id.ToString(), Text = u.UserName })
            .ToList();
    }

    private async Task LoadFilteredReportAsync()
    {
        var allHarvests = await _harvestManager.GetAllAsync();
        var mapped = _mapper.Map<List<HarvestModel>>(allHarvests);

        var filtered = mapped.AsQueryable();

        if (Filter.FromDate.HasValue)
            filtered = filtered.Where(h => h.DateHarvest.Date >= Filter.FromDate.Value.Date);

        if (Filter.ToDate.HasValue)
            filtered = filtered.Where(h => h.DateHarvest.Date <= Filter.ToDate.Value.Date);

        if (Filter.UserId.HasValue)
            filtered = filtered.Where(h => h.UserId == Filter.UserId.Value);

        Filter.Results = filtered.ToList();

        HtmlReportContent = $"""
            <h1>Отчет за период с {Filter.FromDate:dd.MM.yyyy} по {Filter.ToDate:dd.MM.yyyy}</h1>
            <table border="1">
                <thead><tr><th>ID</th><th>Имя</th><th>Продукт</th><th>Количество</th><th>Единица</th></tr></thead>
                <tbody>
                    {string.Join("", Filter.Results.Select(h => $"<tr><td>{h.UserId}</td><td>{h.UserName}</td><td>{h.ProductName}</td><td>{h.Quantity}</td><td>{h.UnitName}</td></tr>"))}
                </tbody>
            </table>
        """;
    }
}
