namespace FrageFejden.Api.Auth.Dto
{
    public sealed class LoginRequest
    {
        public string EmailOrUserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
