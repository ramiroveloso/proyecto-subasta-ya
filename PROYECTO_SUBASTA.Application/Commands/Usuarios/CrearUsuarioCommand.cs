namespace PROYECTO_SUBASTA.Application.Commands.Usuarios
{
    /// <summary>
    /// Comando para la creación de un nuevo usuario en la plataforma.
    /// </summary>
    public class CrearUsuarioCommand
    {
        public string Nombre { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}
