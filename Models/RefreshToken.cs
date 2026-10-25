public class RefreshToken
{
    public int Id { get; set; }
    public string Token { get; set; }
    public DateTime Expiration { get; set; }
    public string EmailUsuario { get; set; }
    public bool Ativo { get; set; } = true;
}
