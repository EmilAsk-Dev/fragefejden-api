namespace FrageFejden.Api.Auth.Dto
{
    public sealed class AuthResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public DateTime ExpiresAtUtc { get; set; }
        public string? UserId { get; set; }
        public string? UserName { get; set; }
        public string[] Roles { get; set; } = Array.Empty<string>();
    }
}
