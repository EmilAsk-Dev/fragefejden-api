using System.ComponentModel.DataAnnotations;

namespace FrageFejden.Api.Auth.Dto
{
    public sealed class RegisterRequest
    {
        [Required, MinLength(3)]
        public string UserName { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, MinLength(6)]
        public string Password { get; set; } = string.Empty;

        [Required, MinLength(3)]
        public string Fullname { get; set; } = string.Empty;

    }
}
