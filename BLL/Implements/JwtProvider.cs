using Common.Settings;
using DAL.Models;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Implements
{
    public class JwtProvider
    {
        public static string GenerateAccessToken(List<Claim> claims)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(JwtSettingModel.SecretKey);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(JwtSettingModel.ExpireDayAccessToken),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
                Issuer = JwtSettingModel.Issuer, // Thêm Issuer
                Audience = JwtSettingModel.Audience // Thêm Audience
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }


        public static string GenerateRefreshToken(string userId)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(JwtSettingModel.SecretKey);

            var claims = new List<Claim>
            {
                new Claim("TokenPurpose", "RefreshToken"),
                new Claim(ClaimTypes.NameIdentifier, userId)
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddDays(JwtSettingModel.ExpireDayRefreshToken),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
                Issuer = JwtSettingModel.Issuer,
                Audience = JwtSettingModel.Audience 
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
        public static bool Validation(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(JwtSettingModel.SecretKey);

                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = JwtSettingModel.Issuer,
                    ValidateAudience = true,
                    ValidAudience = JwtSettingModel.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero 
                }, out SecurityToken validatedToken);

                return true; 
            }
            catch (Exception)
            {
                return false;
            }
        }
        public static List<Claim> DecodeToken(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(JwtSettingModel.SecretKey);

                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = JwtSettingModel.Issuer,
                    ValidateAudience = true,
                    ValidAudience = JwtSettingModel.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);

                var jwtToken = (JwtSecurityToken)validatedToken;
                return jwtToken.Claims.ToList();
            }
            catch (Exception)
            {
                return new List<Claim>();
            }
        }
        public static string GetValueFromToken(string token, string claimType)
        {
            var claims = DecodeToken(token);
            var claim = claims.FirstOrDefault(c => c.Type == claimType);

            return claim?.Value ?? string.Empty;
        }

    }
}
