using Core.Entities;
using Core.Entities.OrderAggregate;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Text.Json;

namespace Infrastructure.Data
{
    public class ShopDbContextSeed
    {
        public static async Task SeedAsync(ShopDbContext dbContext, ILoggerFactory loggerFactory)
        {
            try
            {
                var assembly = Assembly.GetAssembly(typeof(ShopDbContextSeed));
                var basePath = Path.GetDirectoryName(assembly.Location);
                var path = Path.GetDirectoryName(Environment.CurrentDirectory);
                if (!dbContext.Brands.Any())
                {
                    var brandsData = File.ReadAllText(path + @"/Infrastructure/Data/SeedData/brands.json");
                    var brands = JsonSerializer.Deserialize<List<Brand>>(brandsData);
                    foreach (var brand in brands)
                    {
                        dbContext.Brands.Add(brand);
                    }
                    await dbContext.SaveChangesAsync();
                }

                if (!dbContext.Categories.Any())
                {
                    var categoriesData = File.ReadAllText(path + @"/Infrastructure/Data/SeedData/categories.json");
                    var categories = JsonSerializer.Deserialize<List<Category>>(categoriesData);
                    foreach (var category in categories)
                    {
                        dbContext.Categories.Add(category);
                    }
                    await dbContext.SaveChangesAsync();
                }

                if (!dbContext.Products.Any())
                {
                    var productsData = File.ReadAllText(path + @"/Infrastructure/Data/SeedData/products.json");
                    var products = JsonSerializer.Deserialize<List<Product>>(productsData);
                    foreach (var product in products)
                    {
                        dbContext.Products.Add(product);
                    }
                    await dbContext.SaveChangesAsync();
                }

                if (!dbContext.DeliveryMethods.Any())
                {
                    var dmData =
                        File.ReadAllText(path + @"/Infrastructure/Data/SeedData/delievery.json");

                    var methods = JsonSerializer.Deserialize<List<DeliveryMethod>>(dmData);

                    foreach (var item in methods)
                    {
                        dbContext.DeliveryMethods.Add(item);
                    }

                    await dbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                var logger = loggerFactory.CreateLogger<ShopDbContextSeed>();
                logger.LogError(ex.Message);
            }
        }
    }
}
