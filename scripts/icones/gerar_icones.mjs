// Gera src/Fc25Draft.Web/Components/Common/IconesLucide.cs com os ícones do lucide que o site usa.
//
// Uso (na raiz do repositório):
//   npm pack lucide-static@0.460.0 && tar -xzf lucide-static-0.460.0.tgz   (numa pasta temporária)
//   node scripts/icones/gerar_icones.mjs <pasta>/package/icons
//
// Para usar um ícone novo: ponha o nome dele (o do lucide.dev) em icones.txt e rode de novo.
import { readFileSync, writeFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const aqui = dirname(fileURLToPath(import.meta.url));
const pasta = process.argv[2];
if (!pasta) {
    console.error('Informe a pasta icons do lucide-static.');
    process.exit(1);
}

const nomes = [...new Set(readFileSync(join(aqui, 'icones.txt'), 'utf8')
    .split(/\r?\n/).map(l => l.replace(/#.*/, '').trim()).filter(Boolean))].sort();

const linhas = nomes.map(nome => {
    const svg = readFileSync(join(pasta, `${nome}.svg`), 'utf8');
    const corpo = svg.replace(/<!--[\s\S]*?-->/g, '').replace(/^[\s\S]*?<svg[^>]*>/, '').replace(/<\/svg>[\s\S]*$/, '')
        .split(/\r?\n/).map(l => l.trim()).filter(Boolean).join('')
        .replace(/\s*\/>/g, '/>');
    return `        ["${nome}"] = "${corpo.replace(/"/g, '\\"')}",`;
});

const cs = `// Gerado por scripts/icones/gerar_icones.mjs a partir do lucide-static 0.460.0 (licença ISC, lucide.dev).
// Não edite à mão: ponha o nome em scripts/icones/icones.txt e rode o script.
namespace Fc25Draft.Web.Components.Common;

internal static class IconesLucide
{
    public static readonly IReadOnlyDictionary<string, string> Desenhos = new Dictionary<string, string>
    {
${linhas.join('\n')}
    };
}
`;

writeFileSync(join(aqui, '..', '..', 'src', 'Fc25Draft.Web', 'Components', 'Common', 'IconesLucide.cs'), cs);
console.log(`${nomes.length} ícones gerados.`);
