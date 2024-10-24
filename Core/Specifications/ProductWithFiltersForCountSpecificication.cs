using Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Specifications
{
    public class ProductWithFiltersForCountSpecificication:BaseSpecification<Product>
    {
        public ProductWithFiltersForCountSpecificication(ProductSpecParams specParams) : base(x =>
                    (string.IsNullOrEmpty(specParams.Search)||x.Name.ToLower().Contains(specParams.Search))&&
                    (!specParams.BrandId.HasValue || x.BrandId == specParams.BrandId)&&
                    (!specParams.CategoryId.HasValue || x.CategoryId == specParams.CategoryId))
        {
        }
    }
}
