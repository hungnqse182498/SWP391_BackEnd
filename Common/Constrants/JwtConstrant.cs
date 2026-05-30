using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Constrants
{
    public class JwtConstant
    {
        /// <summary>
        /// Provider constans of the header.
        /// </summary>
        public class Header
        {
            /// <summary>
            /// The key authorization of the header.
            /// </summary>
            public const string Authorization = nameof(Authorization);

            /// <summary>
            /// The postfix of the authorization value.
            /// </summary>
            public const string Bearer = nameof(Bearer);
        }

        /// <summary>
        /// Provider constans of the key claim.
        /// </summary>
        public class KeyClaim
        {
            public const string UserId = nameof(UserId);   
            public const string UserName = nameof(UserName);
            public const string Email = nameof(Email);       
            public const string RoleId = nameof(RoleId);     
        }
    }
}
