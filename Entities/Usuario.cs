using System.ComponentModel.DataAnnotations;

namespace PROYECTO_SUBASTA.Entities
{
    public class Usuario
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(150)]
        public string Email { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string Nombre { get; set; } = null!;

        [Required]
        public string PasswordHash { get; set; } = null!;

        public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

        // Propiedad de navegación: Relación 1 a 1 con Billetera
        public Billetera Billetera { get; set; } = null!;
    }
}
