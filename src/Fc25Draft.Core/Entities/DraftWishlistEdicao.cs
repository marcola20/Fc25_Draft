using System;
using System.Collections.Generic;

namespace Fc25Draft.Core.Entities;

/// <summary>Uma versão (rodada de envio) das listas de pré-draft. Só uma fica aberta por vez.</summary>
public class DraftWishlistEdicao
{
    public int Numero { get; set; }
    public string Nome { get; set; } = null!;
    public DateTime CriadoEm { get; set; }
    public DateTime? EncerradoEm { get; set; }

    public bool Aberta => EncerradoEm is null;

    public ICollection<DraftWishlistEntry> Entradas { get; set; } = new List<DraftWishlistEntry>();
}
