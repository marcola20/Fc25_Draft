# CBFV

Site da liga CBFV: draft, mercado de transferências, Série A e B, Copa, Supercopa, bolão e o resto
que a liga usa no dia a dia. Publicado em https://cbfv-app.onrender.com.

## Stack

- .NET 8 com Blazor Server. O `global.json` fixa o SDK 8, o mesmo do build do Render.
- EF Core 8 com Npgsql e PostgreSQL 18.
- SignalR para as telas ao vivo (draft, mercado, telão e sorteio da Copa).

## Estrutura

| Pasta | O que tem |
|---|---|
| `src/Fc25Draft.Core` | Entidades, DTOs, interfaces e regras puras (`Utilities`: desempate, loteria, calendário, suspensões, overall do PES...) |
| `src/Fc25Draft.Infra` | `DraftDbContext`, configurações do EF, migrations e os serviços que falam com o banco |
| `src/Fc25Draft.Web` | Páginas Blazor, API em `/api`, hubs do SignalR, autenticação por token e serviços de tela |
| `scripts/db-copiar-prod.ps1` | Copia o banco de produção para o banco local |
| `scripts/pes/gerar_base_pes.py` | Gera a base de jogadores do PES 2021 que o site usa para preencher atributos |

## Rodar no PC

1. Abra o Docker Desktop e suba o Postgres local (container `cbfv-db`, Postgres 18, banco
   `CBFV_DEVELOPMENT` na porta 5432), definido no `docker-compose.yml` da raiz:

   ```bash
   docker compose up -d
   ```

2. O site acha esse banco pela connection string de `src/Fc25Draft.Web/appsettings.Development.json`;
   usuário e senha têm de bater com os do `docker-compose.yml`. Os dois arquivos só têm a senha do banco
   local, nunca a de produção.

3. Para trabalhar com os dados reais, copie a produção (o banco local é apagado e recriado; a produção
   só é lida):

   ```powershell
   $env:PROD_DATABASE_URL = "postgresql://usuario:senha@host:5432/banco"
   powershell -ExecutionPolicy Bypass -File .\scripts\db-copiar-prod.ps1
   ```

4. Rode o site (https://localhost:5001):

   ```bash
   dotnet run --project src/Fc25Draft.Web
   ```

O site aplica as migrations pendentes sozinho ao subir.

## Banco e migrations

O schema sai só das migrations do EF, em `src/Fc25Draft.Infra/Migrations`. Para criar uma nova:

```bash
dotnet ef migrations add NomeDaMudanca --project src/Fc25Draft.Infra --startup-project src/Fc25Draft.Web
```

Ajuste de dados que precisa acompanhar a mudança (backfill) vai na própria migration, com
`migrationBuilder.Sql(...)`.

## Deploy (Render)

- O Render faz o build pelo `Dockerfile` (SDK 8 para compilar, imagem `aspnet:8.0` para rodar, porta 10000)
  a cada push na branch `producao`.
- A conexão vem da variável `DATABASE_URL` no formato `postgresql://usuario:senha@host:5432/banco`
  (o site também aceita `POSTGRES_URL`, `DATABASE_PUBLIC_URL` ou `ConnectionStrings:DefaultConnection`).
- As migrations rodam na subida, então um deploy com migration nova atualiza o banco sozinho.
- Antes de subir, rode o build local: com o `global.json` ele usa o mesmo SDK do Render.

## Acesso

Não há usuário e senha: cada treinador entra com o próprio token, que vale para o clube em que ele está.
Os administradores têm token próprio, gerenciado em Admin › Administradores. Três tokens errados em
15 minutos bloqueiam o IP por 15 minutos, tanto no login quanto na API.

## Resultados do PES

Os jogos simulados no PES 2021 entram pelo `POST /api/admin/liga/resultados-pes` (token de admin;
`?simular=true` testa sem gravar). O envio é feito pelo `enviar_resultados.py`, que fica no projeto
Auto_PES21.
