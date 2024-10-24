using Core.Entities.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Interfaces
{
    public interface ITokenClaimService
    {
        Task<string> GetTokenAsync(string userName);
        string CreateToken(AppUser user);
    }
}
