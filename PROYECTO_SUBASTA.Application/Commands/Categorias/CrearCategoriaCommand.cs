namespace PROYECTO_SUBASTA.Application.Commands.Categorias
{
    /// <summary>
    /// Comando para el registro de una nueva categoría en el sistema.
    /// </summary>
    public class CrearCategoriaCommand
    {
        public string Nombre { get; set; } = string.Empty;
        public string UrlIcono { get; set; } = string.Empty;
    }
}
