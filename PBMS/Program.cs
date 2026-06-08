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

builder.Services.AddDbContext<ParkingDBContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Đăng ký Repositories
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<ITokenRepository, TokenRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IVehicleTypeRepository, VehicleTypeRepository>();
builder.Services.AddScoped<IFloorRepository, FloorRepository>();
builder.Services.AddScoped<IParkingSessionRepository, ParkingSessionRepository>();
builder.Services.AddScoped<IParkingCardRepository, ParkingCardRepository>();
builder.Services.AddScoped<IParkingSlotRepository, ParkingSlotRepository>();
builder.Services.AddScoped<IMonthlySubscriptionRepository, MonthlySubscriptionRepository>();
builder.Services.AddScoped<IGateRepository, GateRepository>();
builder.Services.AddScoped<IPricingPolicyRepository, PricingPolicyRepository>();
builder.Services.AddScoped<IReservationRepository, ReservationRepository>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IIncidentReportRepository, IncidentReportRepository>();

// Đăng ký Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IVehicleTypeService, VehicleTypeService>();
builder.Services.AddScoped<IFloorService, FloorService>();
builder.Services.AddScoped<IReservationService, ReservationService>();
builder.Services.AddScoped<IPayOSService, PayOSService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IGateService, GateService>();
builder.Services.AddScoped<IParkingSlotService, ParkingSlotService>();
builder.Services.AddScoped<IParkingCardService, ParkingCardService>();
builder.Services.AddScoped<IMonthlySubscriptionService, MonthlySubscriptionService>();
builder.Services.AddScoped<IPricingPolicyService, PricingPolicyService>();
builder.Services.AddScoped<IParkingSessionService, ParkingSessionService>();
builder.Services.AddScoped<IIncidentReportService, IncidentReportService>();
builder.Services.AddScoped<IParkingOperationService, ParkingOperationService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowReactApp");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
