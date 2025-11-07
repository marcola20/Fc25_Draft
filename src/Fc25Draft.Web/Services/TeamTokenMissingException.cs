using System;

namespace Fc25Draft.Web.Services;

public sealed class TeamTokenMissingException : Exception
{
    public TeamTokenMissingException()
        : base("Informe o token do seu time para executar ações como lances, compras ou quick sell.")
    {
    }
}
