using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Services;

namespace PropertyManagement.Controllers;

[Route("injuries")]
public class InjuriesController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["ActiveModule"] = "injuries";
        return View(InjuryCatalog.Parts);
    }
}
