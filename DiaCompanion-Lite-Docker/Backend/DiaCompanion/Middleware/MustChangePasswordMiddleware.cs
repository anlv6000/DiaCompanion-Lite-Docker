namespace DiaCompanion.Api.Middleware
{
    public class MustChangePasswordMiddleware
    {
        private readonly RequestDelegate _next;

        public MustChangePasswordMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var mustChange = context.User.Identity?.IsAuthenticated == true
                && context.User.HasClaim("mustChangePassword", "true");

            var path = context.Request.Path;

            var allowed =
                path.StartsWithSegments("/api/auth/change-password") ||
                path.StartsWithSegments("/api/auth/logout");
                

            if (mustChange && !allowed)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    messageCode = "MSG-MUST-CHANGE-PASSWORD",
                    message = "Bạn phải đổi mật khẩu tạm trước khi tiếp tục."
                });
                return;
            }

            await _next(context);
        }
    }
}
