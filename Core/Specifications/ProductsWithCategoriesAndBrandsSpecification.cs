using Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Specifications
{
    public class ProductsWithCategoriesAndBrandsSpecification:BaseSpecification<Product>
    {
        public ProductsWithCategoriesAndBrandsSpecification(ProductSpecParams specParams)
            :base(x=>(string.IsNullOrEmpty(specParams.Search)||x.Name.ToLower().Contains(specParams.Search)) &&
                 (!specParams.BrandId.HasValue || x.BrandId == specParams.BrandId) &&
                 (!specParams.CategoryId.HasValue || x.CategoryId == specParams.CategoryId))
        {
            AddInclude(x => x.Brand);
            AddInclude(x => x.Category);
            AddOrderBy(x => x.Name);
            ApplyPaging(specParams.PageSize * (specParams.PageIndex - 1), specParams.PageSize);

            if (!string.IsNullOrEmpty(specParams.Sort))
            {
                switch(specParams.Sort)
                {
                    case "priceAsc":
                        AddOrderBy(p => p.Price);
                        break;
                    case "priceDesc":
                        AddOrderByDescending(p => p.Price);
                        break;
                    default:
                        AddOrderBy(n => n.Name);
                        break;
                }
            }
        }
        public ProductsWithCategoriesAndBrandsSpecification(int id)
            : base(x => x.Id == id)
        {
            AddInclude(x => x.Category);
            AddInclude(x => x.Brand);
        }

    }
}
