using Microsoft.AspNetCore.Identity;
using ProjectApplication.Models;

namespace ProjectApplication.Data
{
    public class SeedData
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // 1. Seed Roles
            string[] roles = { "Admin", "Manager", "User" };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }


            // 2. Seed Admin Account

            string adminEmail = "admin@site.com";
            string adminPassword = "Admin@1234";

            if (await userManager.FindByEmailAsync(adminEmail) == null)
            {
                var admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FirstName = "System",
                    LastName = "Admin",
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(admin, adminPassword);

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(admin, "Admin");
                }
            }

            // 3. Seed Manager Account
            string managerEmail = "manager@site.com";
            string managerPassword = "Manager@1234";

            if (await userManager.FindByEmailAsync(managerEmail) == null)
            {
                var manager = new ApplicationUser
                {
                    UserName = managerEmail,
                    Email = managerEmail,
                    FirstName = "System",
                    LastName = "Manager",
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(manager, managerPassword);
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(manager, "Manager");
            }

            // 4. Seed User Account
            string userEmail = "user@site.com";
            string userPassword = "User@1234";

            if (await userManager.FindByEmailAsync(userEmail) == null)
            {
                var user = new ApplicationUser
                {
                    UserName = userEmail,
                    Email = userEmail,
                    FirstName = "System",
                    LastName = "User",
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(user, userPassword);
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(user, "User");
            }

            // 5. Seed Fashion Products
            var context = serviceProvider.GetRequiredService<AppDbContext>();

            if (!context.Products.Any())
            {
                var products = new List<Product>
                {
                    new Product { Name = "Classic White Tee", Category = "Tops", Price = 19.99m, Description = "A clean everyday essential.", ImageUrl = "https://images.unsplash.com/photo-1521572163474-6864f9cf17ab?w=400" },
                    new Product { Name = "Slim Fit Jeans", Category = "Bottoms", Price = 59.99m, Description = "Modern slim cut in dark wash denim.", ImageUrl = "https://images.unsplash.com/photo-1542272604-787c3835535d?w=400" },
                    new Product { Name = "Floral Summer Dress", Category = "Dresses", Price = 49.99m, Description = "Light and breezy for warm days.", ImageUrl = "https://images.unsplash.com/photo-1572804013309-59a88b7e92f1?w=400" },
                    new Product { Name = "Leather Jacket", Category = "Outerwear", Price = 129.99m, Description = "Classic biker style leather jacket.", ImageUrl = "https://images.unsplash.com/photo-1551028719-00167b16eac5?w=400" },
                    new Product { Name = "White Sneakers", Category = "Shoes", Price = 79.99m, Description = "Minimalist sneakers for any outfit.", ImageUrl = "https://images.unsplash.com/photo-1549298916-b41d501d3772?w=400" },
                    new Product { Name = "Wool Scarf", Category = "Accessories", Price = 24.99m, Description = "Soft merino wool in neutral tones.", ImageUrl = "https://images.unsplash.com/photo-1601924351433-3d7a64c1f883?w=400" },
                };
        
                context.Products.AddRange(products);
                await context.SaveChangesAsync();
            }
        }
    }
}
