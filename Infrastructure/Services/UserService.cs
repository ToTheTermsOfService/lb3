using Core.Entities.Identity;
using Core.Interfaces;
using Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class UserService:IUserService
    {
        private readonly AppIdentityDbContext dbContext;
        private readonly DbSet<AppUser> users;

        public UserService(AppIdentityDbContext dbContext)
        {
            this.dbContext = dbContext;
            users =  dbContext.Set<AppUser>();
        }
        public async Task<List<AppUser>> GetUsersAsync()
        {
            return await users.ToListAsync();
        }
    }
}
