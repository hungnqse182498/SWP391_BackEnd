using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class AuthDTO
    {
        public class LoginDTO
        {
            public string Email { get; set; }
            public string Password { get; set; }
        }
        public class RegisterDTO
        {
            public string UserName { get; set; }
            public string FullName { get; set; }
            public string Email { get; set; }
            public string PhoneNumber { get; set; }
            public string Password { get; set; }
            public string ConfirmPassword { get; set; }
        }
    } 
}
