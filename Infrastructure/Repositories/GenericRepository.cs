using Core.Entities;
using Core.Interfaces;
using Core.Specifications;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Repositories
{
    public class GenericRepository<T> : IGenericRepository<T> where T : BaseEntity
    {
        private readonly ShopDbContext _dbContext;
        protected DbSet<T> Entities;
        public GenericRepository(ShopDbContext dbContext)
        {
            _dbContext = dbContext;
            Entities = dbContext.Set<T>();
        }
        public async Task<T> GetByIdAsync(int id)
        {
            return await Entities.FirstOrDefaultAsync(i => i.Id.Equals(id));
        }
        public async Task<IReadOnlyList<T>> ListAllAsync()
        {
            return await Entities.ToListAsync();
        }
        public async Task<T> GetEntityWithSpec(ISpecification<T> specification)
        {
            return await ApplySpecification(specification).FirstOrDefaultAsync();
        }
        public async Task<IReadOnlyList<T>> ListAsync(ISpecification<T> specification)
        {
            return await ApplySpecification(specification).ToListAsync();
        }
        public async Task<int> CountAsync(ISpecification<T> spec)
        {
            return await ApplySpecification(spec).CountAsync();
        }
        public void Update(T entity)
        {
            Entities.Attach(entity);
            Entities.Entry(entity).State = EntityState.Modified;
        }
        public void Add(T entity)
        {
            Entities.Add(entity);
        }
        public void Delete(T entity)
        {
            Entities.Remove(entity);
        }
        private IQueryable<T> ApplySpecification(ISpecification<T> specification)
        {
            return SpecificationEvaluator<T>.GetQuery(Entities.AsQueryable(), specification);
        }

        
    }
}
