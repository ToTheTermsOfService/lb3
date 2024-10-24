using Core.Entities;
using Core.Interfaces;

namespace Core.Interfaces
{
    public interface IUnitOfWork
    {
        Task<int> Complete();
        IGenericRepository<TEntity> Repository<TEntity>() where TEntity : BaseEntity;
    }
}