using Core.Entities.OrderAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Config
{
    public class OrderConfig : IEntityTypeConfiguration<Order>
    {
        public void Configure(EntityTypeBuilder<Order> builder)
        {
            builder.OwnsOne(o => o.ShipToAddress, a =>
            {
                a.WithOwner();
                a.Property(c=>c.Zipcode)
                    .HasMaxLength(18)
                    .IsRequired();
                a.Property(c => c.Street)
                    .HasMaxLength(180)
                    .IsRequired();
                a.Property(a => a.State)
                    .HasMaxLength(60);
                a.Property(a => a.Country)
                    .HasMaxLength(90)
                    .IsRequired();
                a.Property(a => a.City)
                    .HasMaxLength(100)
                    .IsRequired();
            });
            builder.Property(s => s.Status)
                .HasConversion(
                    o => o.ToString(),
                    o => (OrderStatus)Enum.Parse(typeof(OrderStatus), o)
                );
            builder.HasMany(o => o.OrderItems).WithOne().OnDelete(DeleteBehavior.Cascade);

        }
    }
}
