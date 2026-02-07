using Microsoft.AspNetCore.Mvc;

namespace MedicalAssistant.Controllers;

public class HomeController : Controller
{
    private readonly IConfiguration _configuration;
    
    public HomeController(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    
    public IActionResult Index()
    {
        ViewBag.StripePublishableKey = _configuration["Stripe:PublishableKey"];
        return View();
    }
    
    public IActionResult Privacy()
    {
        return View();
    }
}

