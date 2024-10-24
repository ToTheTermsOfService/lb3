using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Helper
{
    public class AuthorizationConstants
    {
        public const string ADMINISTRATOR = "Administrator";
        public const string CUSTOMER = "Customer";
        public const string DEFAULT_PASSWORD = "Pass@word1";

        public const string JWT_SECRET_KEY = "SecretKeyOfDoomThatMustBeAMinimumNumberOfBytes";
    }
}
