using LTD_Communication.Vulnerable.Filters;
using LTD_Communication.Vulnerable.Helpers;
using LTD_Communication.Vulnerable.Models;
using LTD_Communication.Vulnerable.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace LTD_Communication.Vulnerable.Controllers;

[SessionAuthorize]
public class CustomerController : Controller
{
    private readonly DbHelper _db;

    public CustomerController(DbHelper db)
    {
        _db = db;
    }

    [HttpGet]
    public IActionResult Add()
    {
        var model = new AddCustomerViewModel
        {
            Sectors  = GetSectors(),
            Packages = GetPackages()
        };
        return View(model);
    }

    [HttpPost]
    public IActionResult Add(AddCustomerViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.Sectors  = GetSectors();
            model.Packages = GetPackages();
            return View(model);
        }

        int userId = HttpContext.Session.GetInt32("UserId")!.Value;

        // VULNERABLE: SQL Injection via any string field
        // VULNERABLE: FullName is stored raw — <script> tags preserved → Stored XSS
        string sectorId  = model.SectorId.HasValue  ? model.SectorId.Value.ToString()  : "NULL";
        string packageId = model.PackageId.HasValue ? model.PackageId.Value.ToString() : "NULL";
        string phone     = model.Phone    != null   ? $"'{model.Phone}'"               : "NULL";
        string address   = model.Address  != null   ? $"'{model.Address}'"             : "NULL";

        _db.ExecuteNonQuery(
            $"INSERT INTO Customers (FullName, Email, Phone, Address, SectorId, PackageId, CreatedBy) " +
            $"VALUES ('{model.FullName}', '{model.Email}', {phone}, {address}, {sectorId}, {packageId}, {userId})");

        TempData["Success"] = $"Customer '{model.FullName}' added successfully!";
        return RedirectToAction("List");
    }

    public IActionResult List()
    {
        var rows = _db.ExecuteQuery(
            "SELECT c.*, s.Name AS SectorName, p.Name AS PackageName " +
            "FROM Customers c " +
            "LEFT JOIN Sectors s ON c.SectorId = s.Id " +
            "LEFT JOIN InternetPackages p ON c.PackageId = p.Id " +
            "ORDER BY c.CreatedAt DESC");

        var customers = rows.Select(r => new Customer
        {
            Id          = Convert.ToInt32(r["Id"]),
            FullName    = r["FullName"]?.ToString() ?? "",
            Email       = r["Email"]?.ToString()    ?? "",
            Phone       = r["Phone"]?.ToString(),
            Address     = r["Address"]?.ToString(),
            SectorName  = r["SectorName"]?.ToString(),
            PackageName = r["PackageName"]?.ToString(),
            CreatedAt   = r["CreatedAt"] != null ? Convert.ToDateTime(r["CreatedAt"]) : DateTime.Now
        }).ToList();

        return View(customers);
    }

    private List<Sector> GetSectors()
        => _db.ExecuteQuery("SELECT * FROM Sectors ORDER BY Name")
              .Select(r => new Sector
              {
                  Id   = Convert.ToInt32(r["Id"]),
                  Name = r["Name"]?.ToString() ?? ""
              }).ToList();

    private List<InternetPackage> GetPackages()
        => _db.ExecuteQuery("SELECT * FROM InternetPackages ORDER BY Price")
              .Select(r => new InternetPackage
              {
                  Id    = Convert.ToInt32(r["Id"]),
                  Name  = r["Name"]?.ToString()  ?? "",
                  Speed = r["Speed"]?.ToString() ?? "",
                  Price = r["Price"] != null ? Convert.ToDecimal(r["Price"]) : 0
              }).ToList();
}
