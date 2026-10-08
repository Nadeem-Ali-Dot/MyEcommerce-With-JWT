namespace AuthServer.Security
{
    public interface IRefreshTokenService
    {
        string GenerateRawToken(int byteLength = 64);
        string HashToken(string rawToken);
        DateTime GetExpiryUtc();

    }
}
