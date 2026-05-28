using LTD_Communication.Vulnerable.Filters;
using LTD_Communication.Vulnerable.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace LTD_Communication.Vulnerable.Controllers;

public class HomeController : Controller
{
    private readonly DbHelper _db;

    public HomeController(DbHelper db)
    {
        _db = db;
    }

    [SessionAuthorize]
    public IActionResult Index()
    {
        ViewBag.Username = HttpContext.Session.GetString("Username");
        ViewBag.CustomerCount = _db.ExecuteScalar("SELECT COUNT(*) FROM Customers") ?? 0;
        return View();
    }

    public IActionResult Error()
    {
        return View();
    }
}
