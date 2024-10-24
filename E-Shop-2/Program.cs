using Core.Entities.Identity;
using E_Shop_2.Extensions;
using E_Shop_2.Middlewares;
using Infrastructure.Data;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.ConfigureCore(builder.Configuration);
builder.Services.AddAppServices();
builder.Services.AddIdentityExtensions(builder.Configuration);
builder.Services.ConfigureSwagger();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    using (var skope = app.Services.CreateScope())
    {
        var skopeProvider = skope.ServiceProvider;
        var loggerFactory = skopeProvider.GetRequiredService<ILoggerFactory>();
        try
        {
            var dbContext = skopeProvider.GetRequiredService<ShopDbContext>();
            await ShopDbContextSeed.SeedAsync(dbContext, loggerFactory);

            var userManager = skopeProvider.GetRequiredService<UserManager<AppUser>>();
            var roleManager = skopeProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var identityContext = skopeProvider.GetRequiredService<AppIdentityDbContext>();
            await AppIdentityDbContextSeed.SeedUsersAsync(identityContext, userManager, roleManager);
        }
        catch (Exception ex)
        {
            var logger = loggerFactory.CreateLogger<Program>();
            logger.LogError(ex, "An error occured during migration");
        }
    }
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseMiddleware<ExceptionMiddleware>();
app.UseHttpsRedirection();
app.UseCors(builder => builder
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod());
app.UseAuthorization();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
                    Path.Combine(Directory.GetCurrentDirectory(), "Content")
                ),
    RequestPath = "/content"
});
app.MapControllers();

app.Run();
