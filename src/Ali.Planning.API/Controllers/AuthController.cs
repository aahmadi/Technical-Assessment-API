using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Cors;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using Entities;
using Ali.Planning.API.Filters;
using Ali.Planning.API.Model;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Linq;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Ali.Planning.API.Controllers
{
    [Route("api/[controller]")]
    [EnableCors("Any")]
    public class AuthController : Controller
    {
        private ILogger<AuthController> _logger;
        private SignInManager<PlanningUser> _signInMgr;
        private UserManager<PlanningUser> _userMgr;
        private IPasswordHasher<PlanningUser> _hasher;
        private IConfigurationRoot _config;

        public AuthController(
            ILogger<AuthController> logger,
            UserManager<PlanningUser> userManager,
            SignInManager<PlanningUser> singInManager,
            IPasswordHasher<PlanningUser> hasher,
            IConfigurationRoot config
            )
        {
            _logger = logger;
            _userMgr = userManager;
            _signInMgr = singInManager;
            _hasher = hasher;
            _config = config;
        }

        [HttpPost("login")]
        [ValidateModel]
        public async Task<IActionResult> Login([FromBody] LoginCredentialsModel model)
        {
            try
            {
                var result = await _signInMgr.PasswordSignInAsync(model.Username, model.Password, false, false);
                if (result == Microsoft.AspNetCore.Identity.SignInResult.Success)
                {
                    return Ok();
                }
                else
                {
                    _logger.LogWarning($"unsuccessful attempt to login with username: {model.Username} "); 
                    return BadRequest("Cannot authenticate user.");
                }
            }
            catch (Exception e)
            {
                ///TODO: Handle exception 
                _logger.LogError(e.ToString());
                return BadRequest(e.ToString());
            }

        }

        [HttpPost("token")]
        [ValidateModel]
        public async Task<IActionResult> CreateToken([FromBody] LoginCredentialsModel model)
        {
            try
            {
                var user = await _userMgr.FindByNameAsync(model.Username);
                if (user != null)
                {
                    if (_hasher.VerifyHashedPassword(user, user.PasswordHash, model.Password) == PasswordVerificationResult.Success)
                    {
                        var userClaims = await _userMgr.GetClaimsAsync(user);

                        var claims = new[]
                        {
                            new Claim(JwtRegisteredClaimNames.Sub, user.UserName),
                            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                            new Claim(JwtRegisteredClaimNames.GivenName, user.FirstName),
                            new Claim(JwtRegisteredClaimNames.FamilyName, user.LastName),
                            new Claim(JwtRegisteredClaimNames.Email, user.Email)
                        }.Union(userClaims);

                        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Tokens:Key"]));
                        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                        var token = new JwtSecurityToken(
                          issuer: _config["Tokens:Issuer"],
                          audience: _config["Tokens:Audience"],
                          claims: claims,
                          expires: DateTime.UtcNow.AddDays(1),
                          signingCredentials: creds
                          );

                        return Ok(new
                        {
                            token = new JwtSecurityTokenHandler().WriteToken(token),
                            expiration = token.ValidTo
                        });
                    }
                }
                return BadRequest("Invalid credentials.");

            }
            catch (Exception e)
            {
                _logger.LogError($"Exception thrown while creating JWT: {e}");
                ///TODO Handle exception
                return BadRequest("Failed to generate token");
            }

            
        }
    }
}
