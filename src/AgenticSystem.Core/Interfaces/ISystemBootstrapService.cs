using System.Threading;
using System.Threading.Tasks;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Interface para o serviço de auto-bootstrap executado no startup do sistema.
/// Responsável por provisionar tenants e credenciais iniciais de forma segura e resiliente.
/// </summary>
public interface ISystemBootstrapService
{
    /// <summary>
    /// Executa o processo de bootstrap de forma resiliente.
    /// </summary>
    Task BootstrapAsync(CancellationToken cancellationToken = default);
}
