using System.Security.Claims;
using FrageFejden.Api.Auth.Dto;
using FrageFejden.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FrageFejden.Api.Auth
{
    /// <summary>
    /// API för registrering, inloggning, utloggning och identitet.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public sealed class AuthController : ControllerBase
    {
        private readonly SignInManager<AppUser> _signInManager;
        private readonly UserManager<AppUser> _userManager;
        private readonly IJwtTokenService _jwt;

        public AuthController(SignInManager<AppUser> signInManager,
                              UserManager<AppUser> userManager,
                              IJwtTokenService jwt)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _jwt = jwt;
        }

        /// <summary>Registrerar en ny användare.</summary>
        /// <response code="201">Användaren skapades och en JWT returneras.</response>
        /// <response code="400">Validering misslyckades (t.ex. ogiltig e-post/lösenord).</response>
        /// <response code="409">E-post eller användarnamn används redan.</response>
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        {
            try
            {
                if (!ModelState.IsValid) return ValidationProblem(ModelState);

                if (await _userManager.FindByEmailAsync(req.Email) is not null)
                    return Conflict(new { error = "Email already in use." });

                if (await _userManager.FindByNameAsync(req.UserName) is not null)
                    return Conflict(new { error = "Username is taken." });



                var user = new AppUser { UserName = req.UserName, Email = req.Email, FullName = req.Fullname };
                var create = await _userManager.CreateAsync(user, req.Password);


                if (!create.Succeeded)
                    return BadRequest(new { errors = create.Errors.Select(e => e.Description) });

                _ = await _userManager.AddToRoleAsync(user, "Student");

                var token = await _jwt.CreateTokenAsync(user);
                return Created(string.Empty, token);
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: 500);
            }
        }

        /// <summary>Loggar in en användare och returnerar JWT.</summary>
        /// <response code="200">Inloggning lyckades och en JWT returneras.</response>
        /// <response code="401">Ogiltiga uppgifter eller spärrat konto.</response>
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.EmailOrUserName) || string.IsNullOrWhiteSpace(req.Password))
                return Unauthorized();

            var user = await _userManager.FindByEmailAsync(req.EmailOrUserName)
                       ?? await _userManager.FindByNameAsync(req.EmailOrUserName);
            if (user is null) return Unauthorized();

            if (_userManager.SupportsUserLockout && await _userManager.IsLockedOutAsync(user))
                return Unauthorized();

            var pwValid = await _userManager.CheckPasswordAsync(user, req.Password);
            if (!pwValid)
            {
                if (_userManager.SupportsUserLockout) await _userManager.AccessFailedAsync(user);
                return Unauthorized();
            }

            if (_userManager.SupportsUserLockout) await _userManager.ResetAccessFailedCountAsync(user);

            var token = await _jwt.CreateTokenAsync(user);
            return Ok(token);
        }

        /// <summary>Loggar ut (stateless).</summary>
        [Authorize]
        [HttpPost("logout")]
        public IActionResult Logout() => NoContent();

        /// <summary>Loggar ut överallt.</summary>
        [Authorize]
        [HttpPost("logout-all")]
        public async Task<IActionResult> LogoutAll()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return NoContent();

            var user = await _userManager.FindByIdAsync(userId);
            if (user is null) return NoContent();

            await _userManager.UpdateSecurityStampAsync(user);
            return NoContent();
        }

        /// <summary>Returnerar information om den inloggade användaren.</summary>
        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null) return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            var roles = user is null ? Array.Empty<string>() : await _userManager.GetRolesAsync(user);
            return Ok(new { user?.Id, user?.UserName, user?.Email, Roles = roles });
        }
    }
}
