using Core.Entities.OrderAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Config
{
    public class OrderItemConfig : IEntityTypeConfiguration<OrderItem>
    {
        public void Configure(EntityTypeBuilder<OrderItem> builder)
        {
            builder.OwnsOne(opd => opd.ItemOrdered, po =>
            {
                po.WithOwner();
                po.Property(po => po.ProductName)
                    .IsRequired()
                    .HasMaxLength(50);
            });
            builder.Property(i => i.Price)
                .HasColumnType("decimal(18,2)");
        }
    }
}
