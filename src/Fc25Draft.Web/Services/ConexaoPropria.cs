namespace Fc25Draft.Web.Services;

/// <summary>
/// Um serviço que fica registrado como AddScoped (porque divide o DbContext e a transação com outros
/// serviços, como o preço na venda rápida), mas entregue aqui com uma conexão só dele.
/// </summary>
/// <remarks>
/// Para páginas e para o layout: no Blazor Server o DbContext scoped é um só para o circuito inteiro, e
/// duas cargas ao mesmo tempo (o layout montando a prévia do link enquanto a página do jogador carrega,
/// ou outra aba viva) quebravam com "A second operation was started on this context instance".
/// Registrado com <c>AddTambemComConexaoPropria</c> (ver InfraDI).
/// </remarks>
public sealed class ConexaoPropria<TService>(TService servico) where TService : class
{
    public TService Servico { get; } = servico;
}
