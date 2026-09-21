using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using DiaCompanion.Api.Common;
using DiaCompanion.Api.Data;
using DiaCompanion.Api.Middleware;
using DiaCompanion.Api.Services;
using DiaCompanion.Api.Repositories;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
Console.WriteLine("=== ENVIRONMENT ===");
Console.WriteLine(builder.Environment.EnvironmentName);

Console.WriteLine("=== STORAGE CONFIG ===");
Console.WriteLine("FundusRoot = " + builder.Configuration["Storage:FundusRoot"]);
Console.WriteLine("AiMasksRoot = " + builder.Configuration["Storage:AiMasksRoot"]);
var connectionString =
    builder.Configuration.GetConnectionString("Default");

Console.WriteLine("=== CONNECTION STRING ===");
// (đã bỏ in chuỗi kết nối: chứa mật khẩu và sẽ lọt vào log của container)
Console.WriteLine("=========================");
/* ------------------------------------------------------------------ config */
// QT-19: secret đọc từ biến môi trường, KHÔNG từ bảng SystemConfigs.
// Ví dụ: JWT__SIGNINGKEY=... (hai gạch dưới = phân cấp)
builder.Configuration.AddEnvironmentVariables();

/* ---------------------------------------------------------------- services */
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default"),
        sql => sql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null)));




// Repository is scoped together with AppDbContext (one Unit of Work per request).
builder.Services.AddScoped<IRepository, EfRepository>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<IClinicClock, ClinicClock>();
builder.Services.AddSingleton<IDeferralService, DeferralService>();
builder.Services.AddSingleton<ISymptomAdviceService, SymptomAdviceService>();
builder.Services.AddSingleton<IFileStorageService, FileStorageService>();

builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IConfigService, ConfigService>();
builder.Services.AddScoped<IVoidService, VoidService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IAdherenceService, AdherenceService>();
builder.Services.AddScoped<IClinicalRiskService, ClinicalRiskService>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddHttpClient<ISmsSender, SmsGatewaySender>(c =>
{
    c.BaseAddress = new Uri(
        builder.Configuration["SmsGateway:BaseUrl"] ?? "http://127.0.0.1:8091/");

    c.Timeout = TimeSpan.FromSeconds(
        builder.Configuration.GetValue<int?>("SmsGateway:TimeoutSeconds") ?? 10);
});
builder.Services.AddHttpClient<IAiInferenceClient, AiInferenceClient>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["AiService:BaseUrl"] ?? "http://localhost:8000");
    // QA-02: suy luận không được vượt quá ngưỡng thời gian đã cam kết
    c.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue<int?>("AiService:TimeoutSeconds") ?? 60);
});

// Application services: Controller -> Service -> Repository
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IBlogService, BlogService>();
builder.Services.AddScoped<IDiagnosesService, DiagnosesService>();
builder.Services.AddScoped<IEngagementService, EngagementService>();
builder.Services.AddScoped<IExportService, ExportService>();
builder.Services.AddScoped<IImagesService, ImagesService>();
builder.Services.AddScoped<IMonitoringService, MonitoringService>();
builder.Services.AddScoped<IPatientsService, PatientsService>();
builder.Services.AddScoped<IPrescriptionsService, PrescriptionsService>();
builder.Services.AddScoped<IRecheckService, RecheckService>();
builder.Services.AddScoped<IVisitMaintenanceService, VisitMaintenanceService>();
builder.Services.AddScoped<IReceptionService, ReceptionService>();
builder.Services.AddScoped<ITriageService, TriageService>();
builder.Services.AddScoped<IUsersService, UsersService>();
builder.Services.AddScoped<IVisitsService, VisitsService>();
// Bản thu thập dữ liệu (Lite): không chạy tác vụ nền nhắc thuốc / tái khám.
// builder.Services.AddHostedService<ClinicalReminderWorker>();

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        o.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
    });



builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new
            {
                code = "TOO_MANY_REQUESTS",
                message = "Bạn đã gửi quá nhiều yêu cầu. Vui lòng thử lại sau 1 phút."
            },
            cancellationToken);
    };

    options.AddPolicy("login-limit", httpContext =>
    {
        var ipAddress =
            httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";

        return RateLimitPartition.GetSlidingWindowLimiter(
            ipAddress,
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueLimit = 0,
                QueueProcessingOrder =
                    QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            });
    });

    options.AddPolicy("otp-request-limit", httpContext =>
    {
        var ipAddress =
            httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";

        return RateLimitPartition.GetSlidingWindowLimiter(
            ipAddress,
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 1,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueLimit = 0,
                QueueProcessingOrder =
                    QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            });
    });
});

/* --------------------------------------------------------------------- JWT */
var jwtService = new JwtTokenService(builder.Configuration);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = jwtService.SigningKey,
            // Mặc định .NET cho lệch 5 phút; siết lại để phiên hết hạn đúng lúc
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        opt.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var principal = context.Principal;
                if (principal is null
                    || !string.Equals(principal.FindFirst("token_type")?.Value, "access", StringComparison.Ordinal)
                    || !int.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
                {
                    context.Fail("Token không hợp lệ.");
                    return;
                }

                var repository = context.HttpContext.RequestServices.GetRequiredService<IRepository>();
                var snapshot = await repository.GetAuthorizationSnapshotAsync(userId, context.HttpContext.RequestAborted);
                if (snapshot is null || snapshot.Roles.Count == 0)
                {
                    context.Fail("Tài khoản không còn vai trò đang hoạt động.");
                    return;
                }

                // Không tin role claim cũ trong JWT. Mỗi request xác thực lại role
                // đang active từ dbo.Roles + dbo.UserRoles trước khi [Authorize(Roles=...)] chạy.
                if (principal.Identity is ClaimsIdentity identity)
                {
                    foreach (var claim in identity.FindAll(ClaimTypes.Role).ToArray()) identity.RemoveClaim(claim);
                    foreach (var claim in identity.FindAll("patientId").ToArray()) identity.RemoveClaim(claim);
                    foreach (var claim in identity.FindAll("mustChangePassword").ToArray()) identity.RemoveClaim(claim);
                    foreach (var claim in identity.FindAll("fullName").ToArray()) identity.RemoveClaim(claim);

                    foreach (var role in snapshot.Roles) identity.AddClaim(new Claim(ClaimTypes.Role, role));
                    identity.AddClaim(new Claim("fullName", snapshot.FullName));
                    if (snapshot.PatientId is int patientId) identity.AddClaim(new Claim("patientId", patientId.ToString()));
                    if (snapshot.MustChangePassword) identity.AddClaim(new Claim("mustChangePassword", "true"));
                }
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(o => o.AddPolicy("app", p => p

    .WithOrigins("http://localhost:5173", "http://localhost:8082","http://localhost:9001", "http://localhost:8081", "http://10.33.69.77:8081", "http://diacompanion.io.vn", "https://diacompanion.io.vn")
    .AllowAnyHeader().AllowAnyMethod()));


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "DiaCompanion API",
        Version = "v1",
        Description = "Hệ thống hỗ trợ sàng lọc bệnh võng mạc đái tháo đường — SET490-G6"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        }] = Array.Empty<string>()
    });
});

var app = builder.Build();

/* -------------------------------------------------------------- pipeline */
app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "DiaCompanion API v1"));
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors("app");
app.UseAuthentication();
app.UseRateLimiter();
app.UseMiddleware<MustChangePasswordMiddleware>();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", async (IRepository repository) =>
{
    var canConnect = await repository.CanConnectAsync();
    return Results.Ok(new
    {
        status = canConnect ? "healthy" : "degraded",
        database = canConnect,
        utcNow = DateTime.UtcNow
    });
}).AllowAnonymous();

app.MapFallbackToFile("index.html");

app.Run();
