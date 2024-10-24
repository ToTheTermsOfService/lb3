using AutoMapper;
using Core.Entities;
using Core.Interfaces;
using Core.Specifications;
using E_Shop_2.Dtos;
using E_Shop_2.Errors;
using E_Shop_2.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace E_Shop_2.Controllers
{
    public class ProductController:BaseApiController
    {
        private readonly IGenericRepository<Product> productRepository;
        private readonly IGenericRepository<Category> categoryRepository;
        private readonly IGenericRepository<Brand> brandRepository;
        private readonly IMapper mapper;

        public ProductController(IGenericRepository<Product> productRepository, 
                                IGenericRepository<Category> categoryRepository, 
                                IGenericRepository<Brand> brandRepository,
                                IMapper mapper)
        {
            this.productRepository = productRepository;
            this.categoryRepository = categoryRepository;
            this.brandRepository = brandRepository;
            this.mapper = mapper;
        }
        [HttpGet]
        public async Task<ActionResult<Pagination<ProductToReturnDto>>> GetProducts([FromQuery] ProductSpecParams productParams)
        {
            var spec = new ProductsWithCategoriesAndBrandsSpecification(productParams);
            var countSpec = new ProductWithFiltersForCountSpecificication(productParams);
            var totalItems = await productRepository.CountAsync(countSpec);
            var products = await productRepository.ListAsync(spec);
            var data = mapper
                .Map<IReadOnlyList<Product>, IReadOnlyList<ProductToReturnDto>>(products);
            return Ok(new Pagination<ProductToReturnDto>(productParams.PageIndex, productParams.PageSize, totalItems, data));
        }
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ProductToReturnDto>> GetProduct(int id)
        {
            var spec = new ProductsWithCategoriesAndBrandsSpecification(id);
            var product = await productRepository.GetEntityWithSpec(spec);
            if (product == null) return NotFound(new ApiResponse(404));
            return mapper.Map<Product, ProductToReturnDto>(product);
        }
        [HttpGet("brands")]
        public async Task<ActionResult<IReadOnlyList<Brand>>> GetProductBrands()
        {
            return Ok(await brandRepository.ListAllAsync());
        }
        [HttpGet("categories")]
        public async Task<ActionResult<IReadOnlyList<Category>>> GetProductTypes()
        {
            return Ok(await categoryRepository.ListAllAsync());
        }
    }
}
