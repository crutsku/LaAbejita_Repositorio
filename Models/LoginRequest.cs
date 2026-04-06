namespace LaAbejita.Models
{
    public class LoginRequest
    {
        public string Username { get; set; }
        public RolUsuarioLogin Rol { get; set; }
        public string Contrasena { get; set; }
    }
    public enum RolUsuarioLogin
    {
        Administrador = 1,
        Cocinero = 2
    }
}
