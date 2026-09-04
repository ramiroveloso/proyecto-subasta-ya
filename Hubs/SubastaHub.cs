using Microsoft.AspNetCore.SignalR;

namespace PROYECTO_SUBASTA.Hubs
{
    public class SubastaHub : Hub
    {
        // El frontend llama a este método al entrar a la vista de una subasta específica
        public async Task UnirseASubasta(string subastaId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, subastaId);
        }

        // El frontend llama a este método al salir de la vista
        public async Task SalirDeSubasta(string subastaId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, subastaId);
        }
    }
}