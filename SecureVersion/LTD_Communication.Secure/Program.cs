using LTD_Communication.Secure.Helpers;
using LTD_Communication.Secure.Services;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

builder.Services.AddScoped<DbHelper>(_ =>
    new DbHelper(builder.Configuration.GetConnectionString("DefaultConnection")!));
builder.Services.AddScoped<PasswordPolicyService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Auto-seed admin user with PBKDF2-hashed password on first run
using (var scope = app.Services.CreateScope())
{
    SeedAdmin(scope.ServiceProvider.GetRequiredService<DbHelper>());
}

app.Run();

static void SeedAdmin(DbHelper db)
{
    try
    {
        // Seed common data
        db.ExecuteNonQuery(
            @"IF NOT EXISTS (SELECT 1 FROM Sectors WHERE Name = 'Residential')
              INSERT INTO Sectors (Name, Description) VALUES ('Residential', 'Home internet services for individual customers');
              IF NOT EXISTS (SELECT 1 FROM Sectors WHERE Name = 'Commercial')
              INSERT INTO Sectors (Name, Description) VALUES ('Commercial', 'Business internet solutions for SMEs and enterprises');
              IF NOT EXISTS (SELECT 1 FROM Sectors WHERE Name = 'Industrial')
              INSERT INTO Sectors (Name, Description) VALUES ('Industrial', 'High-bandwidth connectivity for industrial facilities');");

        db.ExecuteNonQuery(
            @"IF NOT EXISTS (SELECT 1 FROM InternetPackages WHERE Name = 'Basic')
              INSERT INTO InternetPackages (Name, Speed, Price, Description) VALUES ('Basic', '50 Mbps', 29.99, 'Suitable for light browsing and email');
              IF NOT EXISTS (SELECT 1 FROM InternetPackages WHERE Name = 'Standard')
              INSERT INTO InternetPackages (Name, Speed, Price, Description) VALUES ('Standard', '100 Mbps', 49.99, 'Great for HD streaming and remote work');
              IF NOT EXISTS (SELECT 1 FROM InternetPackages WHERE Name = 'Premium')
              INSERT INTO InternetPackages (Name, Speed, Price, Description) VALUES ('Premium', '500 Mbps', 79.99, 'Perfect for multiple devices and 4K streaming');
              IF NOT EXISTS (SELECT 1 FROM InternetPackages WHERE Name = 'Ultra')
              INSERT INTO InternetPackages (Name, Speed, Price, Description) VALUES ('Ultra', '1 Gbps', 119.99, 'Maximum speed for power users and businesses');");

        // Seed admin user with PBKDF2 hash
        var count = db.ExecuteScalar(
            "SELECT COUNT(*) FROM Users WHERE Username = @Username",
            new SqlParameter("@Username", "admin"));

        if (Convert.ToInt32(count) == 0)
        {
            var (hash, salt) = PasswordHelper.HashPassword("Admin@12345!");

            db.ExecuteNonQuery(
                "INSERT INTO Users (Username, Email, PasswordHash, Salt) VALUES (@Username, @Email, @Hash, @Salt)",
                new SqlParameter("@Username", "admin"),
                new SqlParameter("@Email",    "admin@ltd-communication.com"),
                new SqlParameter("@Hash",     hash),
                new SqlParameter("@Salt",     salt));

            db.ExecuteNonQuery(
                @"INSERT INTO PasswordHistory (UserId, PasswordHash, Salt)
                  SELECT Id, @Hash, @Salt FROM Users WHERE Username = @Username",
                new SqlParameter("@Hash",     hash),
                new SqlParameter("@Salt",     salt),
                new SqlParameter("@Username", "admin"));
        }
    }
    catch
    {
        // DB not yet set up — run 01_Schema.sql first
    }
}
