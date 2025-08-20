using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using PixelSolution.Data;
using PixelSolution.Services;
using PixelSolution.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Database Configuration
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Authentication and Authorization
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
.AddCookie(options =>
{
    options.LoginPath = "/Auth/Login";
    options.LogoutPath = "/Auth/Logout";
    options.AccessDeniedPath = "/Auth/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("ManagerOrAdmin", policy => policy.RequireRole("Admin", "Manager"));
    options.AddPolicy("AllUsers", policy => policy.RequireRole("Admin", "Manager", "Employee"));
});

// Service Registration
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IDepartmentService, DepartmentService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ISupplierService, SupplierService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ISaleService, SaleService>();
builder.Services.AddScoped<IPurchaseRequestService, PurchaseRequestService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<ISalesService, SalesService>();
builder.Services.AddScoped<IBarcodeService, BarcodeService>();
builder.Services.AddScoped<IReceiptPrintingService, ReceiptPrintingService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IActivityLogService, ActivityLogService>();

// New Customer and Employee Management Services
builder.Services.AddScoped<ICustomerCartService, CustomerCartService>();
builder.Services.AddScoped<IProductRequestService, ProductRequestService>();
builder.Services.AddScoped<IEmployeeManagementService, EmployeeManagementService>();
builder.Services.AddScoped<IPaymentReminderService, PaymentReminderService>();
builder.Services.AddHostedService<BackgroundPaymentReminderService>();

// MPESA Service Configuration
builder.Services.Configure<MpesaSettings>(builder.Configuration.GetSection("MpesaSettings"));
builder.Services.AddHttpClient<IMpesaService, MpesaService>();
builder.Services.AddScoped<IMpesaService, MpesaService>();

// Additional Services
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Logging Configuration
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Anti-forgery token configuration
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
    options.SuppressXFrameOptionsHeader = false;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

// Configure routing with role-based redirects
app.MapControllerRoute(
    name: "admin",
    pattern: "admin/{action=Dashboard}",
    defaults: new { controller = "Admin" });

app.MapControllerRoute(
    name: "employee",
    pattern: "employee/{action=Index}",
    defaults: new { controller = "Employee" });

app.MapControllerRoute(
    name: "auth",
    pattern: "auth/{action=Login}",
    defaults: new { controller = "Auth" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");

// Add middleware to redirect employees trying to access admin pages
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.ToLower();
    
    if (path != null && path.StartsWith("/admin") && context.User.Identity?.IsAuthenticated == true)
    {
        var userRole = context.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        
        if (userRole?.ToLower() == "employee")
        {
            context.Response.Redirect("/Employee/Index");
            return;
        }
    }
    
    await next();
});

// Database initialization with BCrypt password hashing
try
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        logger.LogInformation("=== STARTING DATABASE INITIALIZATION ===");
        
        // Check if database exists and if migrations are needed
        bool databaseExists = await context.Database.CanConnectAsync();
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
        bool hasPendingMigrations = pendingMigrations.Any();
        
        logger.LogInformation("Database exists: {DatabaseExists}", databaseExists);
        logger.LogInformation("Pending migrations count: {PendingCount}", pendingMigrations.Count());
        
        if (!databaseExists || hasPendingMigrations)
        {
            logger.LogInformation("Database needs initialization or migration. Running DbInitializer...");
            logger.LogInformation("Initializing database with BCrypt password hashing");
            
            // Apply any pending migrations first
            if (hasPendingMigrations)
            {
                logger.LogInformation("Applying pending migrations...");
                await context.Database.MigrateAsync();
                logger.LogInformation("Migrations applied successfully");
            }
            
            // Initialize database with seed data
            await DbInitializer.InitializeAsync(context);
            logger.LogInformation("Database initialization completed successfully");
        }
        else
        {
            logger.LogInformation("Database already exists and is up to date. Skipping initialization.");
        }

        // Verify users exist and test BCrypt password verification
        var adminUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "dennisngugi219@gmail.com");
        var salesUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "sales@pixelsolution.com");
        var managerUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "manager@pixelsolution.com");

        if (adminUser != null)
        {
            logger.LogInformation($"=== ADMIN USER VERIFICATION ===");
            logger.LogInformation($"Email: {adminUser.Email}");
            logger.LogInformation($"Password Hash: {adminUser.PasswordHash?.Substring(0, 20)}...");
            logger.LogInformation($"Expected Password: 'Admin1234'");
            
            bool passwordValid = BCrypt.Net.BCrypt.Verify("Admin1234", adminUser.PasswordHash);
            logger.LogInformation($"BCrypt Verification: {passwordValid}");
            logger.LogInformation($"Status: {adminUser.Status}");
            logger.LogInformation($"UserType: {adminUser.UserType}");
        }
        else
        {
            logger.LogError("Admin user not found in database!");
        }

        if (salesUser != null)
        {
            logger.LogInformation($"=== SALES USER VERIFICATION ===");
            logger.LogInformation($"Email: {salesUser.Email}");
            logger.LogInformation($"Password Hash: {salesUser.PasswordHash?.Substring(0, 20)}...");
            logger.LogInformation($"Expected Password: 'Employee123!'");
            
            bool passwordValid = BCrypt.Net.BCrypt.Verify("Employee123!", salesUser.PasswordHash);
            logger.LogInformation($"BCrypt Verification: {passwordValid}");
            logger.LogInformation($"Status: {salesUser.Status}");
            logger.LogInformation($"UserType: {salesUser.UserType}");
        }
        else
        {
            logger.LogError("Sales user not found in database!");
        }

        if (managerUser != null)
        {
            logger.LogInformation($"=== MANAGER USER VERIFICATION ===");
            logger.LogInformation($"Email: {managerUser.Email}");
            logger.LogInformation($"Password Hash: {managerUser.PasswordHash?.Substring(0, 20)}...");
            logger.LogInformation($"Expected Password: 'Manager123!'");
            
            bool passwordValid = BCrypt.Net.BCrypt.Verify("Manager123!", managerUser.PasswordHash);
            logger.LogInformation($"BCrypt Verification: {passwordValid}");
            logger.LogInformation($"Status: {managerUser.Status}");
            logger.LogInformation($"UserType: {managerUser.UserType}");
        }

        // Check total user count
        var totalUsers = await context.Users.CountAsync();
        logger.LogInformation($"Total users in database: {totalUsers}");

        // Display login credentials for development
        if (app.Environment.IsDevelopment())
        {
            logger.LogInformation("======================================");
            logger.LogInformation("=== DEBUG LOGIN CREDENTIALS ===");
            logger.LogInformation("Admin: dennisngugi219@gmail.com / Admin1234");
            logger.LogInformation("Employee: sales@pixelsolution.com / Employee123!");
            logger.LogInformation("Manager: manager@pixelsolution.com / Manager123!");
            logger.LogInformation("======================================");
            logger.LogInformation("NOTE: Passwords are stored as PLAIN TEXT for debugging");
            logger.LogInformation("======================================");
        }

        // Optional: Test authentication service directly
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        if (adminUser != null)
        {
            logger.LogInformation("=== TESTING AUTHENTICATION SERVICE ===");
            try
            {
                var testUser = await authService.AuthenticateAsync("dennisngugi219@gmail.com", "Admin1234");
                logger.LogInformation($"Auth service test result: {(testUser != null ? "SUCCESS" : "FAILED")}");
                if (testUser != null)
                {
                    logger.LogInformation($"Authenticated user: {testUser.Email}, Type: {testUser.UserType}");
                }
            }
            catch (Exception authEx)
            {
                logger.LogError(authEx, "Error testing authentication service");
            }
        }
    }
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "An error occurred while initializing the database.");
    logger.LogError($"Exception details: {ex.Message}");
    logger.LogError($"Stack trace: {ex.StackTrace}");

    // Don't stop the application, but log the error
    logger.LogWarning("Continuing without database initialization. Please check your connection string and database setup.");
}

app.Run();