using ClassificadorDoc.Models.RealTime;

namespace ClassificadorDoc.Services.RealTime
{
    /// <summary>
    /// Interface para gerenciamento de usuários conectados em tempo real
    /// </summary>
    public interface IConnectedUsersService
    {
        /// <summary>
        /// Adiciona um usuário à lista de conectados
        /// </summary>
        Task AddUserAsync(string connectionId, string userId, string userName);

        /// <summary>
        /// Remove um usuário da lista de conectados
        /// </summary>
        Task RemoveUserAsync(string connectionId);

        /// <summary>
        /// Obtém todos os usuários conectados
        /// </summary>
        Task<List<ConnectedUser>> GetConnectedUsersAsync();

        /// <summary>
        /// Obtém contagem de usuários conectados
        /// </summary>
        Task<int> GetConnectedUsersCountAsync();

        /// <summary>
        /// Verifica se um usuário específico está conectado
        /// </summary>
        Task<bool> IsUserConnectedAsync(string userId);

        /// <summary>
        /// Atualiza a última atividade de um usuário
        /// </summary>
        Task UpdateLastActivityAsync(string connectionId);

        /// <summary>
        /// Remove usuários inativos (sem atividade por mais de X minutos)
        /// </summary>
        Task RemoveInactiveUsersAsync(int inactiveMinutes = 30);

        /// <summary>
        /// Notifica todos os clientes sobre mudanças na lista de usuários conectados
        /// </summary>
        Task BroadcastConnectedUsersUpdateAsync();
    }
}
