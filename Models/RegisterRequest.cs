namespace LaAbejita.Models
{
    public class RegisterRequest
    {
        public int? UsuarioId { get; set; }
        public string? Nombre { get; set; }
        public string? ApellidoPaterno { get; set; }
        public string? ApellidoMaterno { get; set; }
        public string Username { get; set; }
        public RolUsuario Rol { get; set; }
        public string? NumeroCelular { get; set; }
        public string Contrasena { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime FechaActualizacion { get; set; }
    }

    public enum RolUsuario {
        Administrador = 1,
        Cocinero = 2
    }
}