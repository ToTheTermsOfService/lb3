using Core.Entities.OrderAggregate;

namespace Core.Specifications
{
    public class OrderWithItemsSpecififcation:BaseSpecification<Order>
    {
        public OrderWithItemsSpecififcation(string email):base(o=>o.BuyerEmail == email)
        {
            AddInclude(o => o.OrderItems);
            AddInclude(o => o.DeliveryMethod);
            AddOrderByDescending(o => o.OrderDate);
        }
        public OrderWithItemsSpecififcation(int id, string email):
            base(o=>o.Id == id && o.BuyerEmail==email)
        {
            AddInclude(o => o.OrderItems);
            AddInclude(o => o.DeliveryMethod);
        }
    }
}
