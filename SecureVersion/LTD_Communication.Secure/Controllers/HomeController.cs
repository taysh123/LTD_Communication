using LTD_Communication.Secure.Filters;
using LTD_Communication.Secure.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace LTD_Communication.Secure.Controllers;

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
        ViewBag.Username      = HttpContext.Session.GetString("Username");
        ViewBag.CustomerCount = _db.ExecuteScalar("SELECT COUNT(*) FROM Customers") ?? 0;
        return View();
    }

    public IActionResult Error()
    {
        return View();
    }
}
