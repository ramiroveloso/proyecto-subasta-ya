namespace PROYECTO_SUBASTA.Application.Services
{
    public interface IPasswordHasher
    {
        string Hash(string password);
        bool Verificar(string password, string hash);
    }
}
