using FrageFejden.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FrageFejden_api.Api.Auth
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase 
    {
        private readonly UserManager<AppUser> _userManager;
        readonly AppDbContext _context;

        public UserController(UserManager<AppUser> userManager, AppDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public class EditUserDto
        {
            public string? UserName { get; set; }
            public string? CurrentPassword { get; set; }
            public string? NewPassword { get; set; }
            public string? Email { get; set; }
            public string? AvatarUrl { get; set; }
        }

        [Authorize]
        [HttpPost("edit")]
        public async Task<IActionResult> ChangeUser([FromBody] EditUserDto editUser)
        {
            if (editUser is null) return BadRequest("Body is required.");

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            
            var errors = new List<IdentityError>();

            if (!string.IsNullOrWhiteSpace(editUser.UserName) && user.UserName != editUser.UserName)
            {
                var r = await _userManager.SetUserNameAsync(user, editUser.UserName);
                if (!r.Succeeded) errors.AddRange(r.Errors);
            }

            if (!string.IsNullOrWhiteSpace(editUser.Email) && user.Email != editUser.Email)
            {
                var r = await _userManager.SetEmailAsync(user, editUser.Email);
                if (!r.Succeeded) errors.AddRange(r.Errors);
            }

            if (!string.IsNullOrWhiteSpace(editUser.AvatarUrl))
            {
                user.AvatarUrl = editUser.AvatarUrl;
            }

            if (!string.IsNullOrWhiteSpace(editUser.CurrentPassword) &&
                !string.IsNullOrWhiteSpace(editUser.NewPassword))
            {
                var r = await _userManager.ChangePasswordAsync(user, editUser.CurrentPassword, editUser.NewPassword);
                if (!r.Succeeded) errors.AddRange(r.Errors);
            }

            if (errors.Count > 0)
            {
                foreach (var e in errors)
                    ModelState.AddModelError(e.Code ?? string.Empty, e.Description);

                return ValidationProblem(ModelState);
            }

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                foreach (var e in updateResult.Errors)
                    ModelState.AddModelError(e.Code ?? string.Empty, e.Description);

                return ValidationProblem(ModelState);
            }

            
            return NoContent();
            
        }
    }
}
