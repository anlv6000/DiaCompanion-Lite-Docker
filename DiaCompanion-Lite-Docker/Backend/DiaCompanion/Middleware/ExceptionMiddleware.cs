using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Middleware;

/// <summary>
/// Gom mọi lỗi về một định dạng thống nhất có mã MSG-xx, để giao diện tra bảng
/// thông điệp trong Report 3 mục 5.2 và hiển thị đúng câu tiếng Việt đã đặc tả.
/// </summary>
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _log;
    private readonly IWebHostEnvironment _env;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> log, IWebHostEnvironment env)
    { _next = next; _log = log; _env = env; }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (AppException ex)
        {
            await WriteAsync(ctx, ex.StatusCode, ex.MessageCode, ex.Message);
        }
        catch (DbUpdateConcurrencyException)
        {
            await WriteAsync(ctx, 409, Msg.ConcurrentEdit,
                "Dữ liệu vừa được người khác thay đổi. Vui lòng tải lại và thử lại.");
        }
        catch (DbUpdateException ex)
        {
            _log.LogError(ex, "Lỗi ghi cơ sở dữ liệu tại {Path}", ctx.Request.Path);

            if (ex.InnerException is Microsoft.Data.SqlClient.SqlException sql &&
                sql.Number is 2601 or 2627)
            {
                await WriteAsync(
                    ctx,
                    409,
                    Msg.PhoneTaken,
                    "Dữ liệu bị trùng với một bản ghi đã có. Vui lòng kiểm tra lại.",
                    _env.IsDevelopment() ? sql.Message : null);

                return;
            }

            await WriteAsync(
                ctx,
                500,
                Msg.LoadFailed,
                "Không lưu được dữ liệu. Vui lòng thử lại.",
                _env.IsDevelopment()
                    ? ex.InnerException?.Message ?? ex.Message
                    : null);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Lỗi không lường trước tại {Path}", ctx.Request.Path);
            await WriteAsync(ctx, 500, Msg.LoadFailed,
                "Không tải được dữ liệu. Vui lòng thử lại.",
                _env.IsDevelopment() ? ex.ToString() : null);
        }
    }

    private static async Task WriteAsync(HttpContext ctx, int status, string code, string message, string? detail = null)
    {
        if (ctx.Response.HasStarted) return;

        ctx.Response.Clear();
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json; charset=utf-8";

        await ctx.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            messageCode = code,
            message,
            detail,
            traceId = ctx.TraceIdentifier
        }, new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }));
    }
}
