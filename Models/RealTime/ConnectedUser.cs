namespace ClassificadorDoc.Models.RealTime
{
    /// <summary>
    /// Modelo para representar um usuário conectado em tempo real
    /// </summary>
    public class ConnectedUser
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime ConnectedAt { get; set; }
        public DateTime LastActivity { get; set; }
        public string IpAddress { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Tempo online em formato legível
        /// </summary>
        public string OnlineTime
        {
            get
            {
                var duration = DateTime.UtcNow - ConnectedAt;

                if (duration.TotalHours >= 1)
                    return $"{duration.Hours}h {duration.Minutes}m";
                else if (duration.TotalMinutes >= 1)
                    return $"{duration.Minutes}m";
                else
                    return "agora";
            }
        }

        /// <summary>
        /// Status da última atividade
        /// </summary>
        public string ActivityStatus
        {
            get
            {
                var timeSinceActivity = DateTime.UtcNow - LastActivity;

                if (timeSinceActivity.TotalMinutes < 2)
                    return "Ativo";
                else if (timeSinceActivity.TotalMinutes < 10)
                    return "Inativo";
                else
                    return "Ausente";
            }
        }

        /// <summary>
        /// Classe CSS para o status
        /// </summary>
        public string StatusClass
        {
            get
            {
                return ActivityStatus switch
                {
                    "Ativo" => "text-success",
                    "Inativo" => "text-warning",
                    "Ausente" => "text-muted",
                    _ => "text-secondary"
                };
            }
        }

        /// <summary>
        /// Ícone para o status
        /// </summary>
        public string StatusIcon
        {
            get
            {
                return ActivityStatus switch
                {
                    "Ativo" => "fas fa-circle",
                    "Inativo" => "fas fa-circle",
                    "Ausente" => "far fa-circle",
                    _ => "far fa-circle"
                };
            }
        }
    }

    /// <summary>
    /// Estatísticas de usuários conectados
    /// </summary>
    public class ConnectedUsersStats
    {
        public int TotalConnected { get; set; }
        public int ActiveUsers { get; set; }
        public int InactiveUsers { get; set; }
        public int AbsentUsers { get; set; }
        public List<ConnectedUser> Users { get; set; } = new();
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}
