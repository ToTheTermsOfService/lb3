using Core.Entities.Identity;
using Infrastructure.Helper;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Identity
{
    public class AppIdentityDbContextSeed
    {
        public static async Task SeedUsersAsync(AppIdentityDbContext dbContext, UserManager<AppUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            await roleManager.CreateAsync(new IdentityRole(UserType.Administrator.ToString()));
            await roleManager.CreateAsync(new IdentityRole(UserType.Customer.ToString()));
            var defaultUser = new AppUser { UserName = "user@gmail.com", Email = "user@gmail.com", DisplayName = "user@gmail.com" };
            await userManager.CreateAsync(defaultUser, AuthorizationConstants.DEFAULT_PASSWORD);
            defaultUser = await userManager.FindByNameAsync("user@gmail.com");
            if (defaultUser != null)
            {
                await userManager.AddToRoleAsync(defaultUser, UserType.Customer.ToString());
            }

            string adminUserName = "admin@gmail.com";
            var adminUser = new AppUser { UserName = adminUserName, Email = adminUserName, DisplayName = adminUserName };
            await userManager.CreateAsync(adminUser, AuthorizationConstants.DEFAULT_PASSWORD);
            adminUser = await userManager.FindByNameAsync(adminUserName);
            if (adminUser != null)
            {
                await userManager.AddToRoleAsync(adminUser, UserType.Administrator.ToString());
            }
        }
    }
}
