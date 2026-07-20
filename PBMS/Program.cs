using BLL.Implements;
using BLL.Interfaces;
using Common.Settings;
using DAL.Implements;
using DAL.Interfaces;
using DAL.Models;
using DAL.UnitOfWorks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PayOS;
using PBMS.Extensions;
using System.Text;
using Hangfire;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddSwaggerDocumentation();

// Cấu hình PayOS
builder.Services.Configure<PayOSConfig>(builder.Configuration.GetSection("PayOS"));

// Thêm CORS vào services (Mở đường cho React)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy =>
        {
            policy.WithOrigins("http://localhost:5173")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
});

// Cấu hình dbcontext
var databaseConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

builder.Services.AddDbContext<ParkingDBContext>(options =>
    options.UseSqlServer(databaseConnectionString));

// Cấu hình MailSettings + MemoryCache
builder.Services.Configure<MailSettings>(builder.Configuration.GetSection("MailSettings"));
builder.Services.AddMemoryCache();

// Đăng ký Repositories
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<ITokenRepository, TokenRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IVehicleTypeRepository, VehicleTypeRepository>();
builder.Services.AddScoped<IFloorRepository, FloorRepository>();
builder.Services.AddScoped<IParkingSessionRepository, ParkingSessionRepository>();
builder.Services.AddScoped<IParkingSlotRepository, ParkingSlotRepository>();
builder.Services.AddScoped<IMonthlySubscriptionRepository, MonthlySubscriptionRepository>();
builder.Services.AddScoped<IGateRepository, GateRepository>();
builder.Services.AddScoped<IPricingPolicyRepository, PricingPolicyRepository>();
builder.Services.AddScoped<IReservationRepository, ReservationRepository>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IIncidentReportRepository, IncidentReportRepository>();
builder.Services.AddScoped<ISubscriptionPackageRepository, SubscriptionPackageRepository>();
builder.Services.AddScoped<IVehicleChangeRequestRepository, VehicleChangeRequestRepository>();
builder.Services.AddScoped<ISubscriptionRenewalRepository, SubscriptionRenewalRepository>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();

// Đăng ký Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IVehicleTypeService, VehicleTypeService>();
builder.Services.AddScoped<IFloorService, FloorService>();
builder.Services.AddScoped<IReservationService, ReservationService>();
builder.Services.AddScoped<IPayOSService, PayOSService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<ISubscriptionPackageService, SubscriptionPackageService>();
builder.Services.AddScoped<IGateService, GateService>();
builder.Services.AddScoped<IParkingSlotService, ParkingSlotService>();
builder.Services.AddScoped<IMonthlySubscriptionService, MonthlySubscriptionService>();
builder.Services.AddScoped<IPricingPolicyService, PricingPolicyService>();
builder.Services.AddScoped<IParkingSessionService, ParkingSessionService>();
builder.Services.AddScoped<IIncidentReportService, IncidentReportService>();
builder.Services.AddScoped<IParkingOperationService, ParkingOperationService>();
builder.Services.AddScoped<ISubscriptionRenewalService, SubscriptionRenewalService>();
builder.Services.AddScoped<IVehicleChangeRequestService, VehicleChangeRequestService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddHttpClient<IOcrService, OcrService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(databaseConnectionString));
builder.Services.AddHangfireServer(); 

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHangfireDashboard();
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseCors("AllowReactApp");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

RecurringJob.AddOrUpdate<IReservationService>(
    "process-overdue-reservations",
    service => service.ProcessOverdueReservationsAsync(),
    Cron.Minutely);

app.Run();
