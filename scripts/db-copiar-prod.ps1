# Copia o banco de produção para o banco local do Docker (cbfv-db).
# O banco local é APAGADO e recriado; produção é só lida (pg_dump).
#
# Uso (na pasta do projeto, com o Docker Desktop aberto):
#   $env:PROD_DATABASE_URL = "postgresql://usuario:senha@host:5432/banco"
#   powershell -ExecutionPolicy Bypass -File .\scripts\db-copiar-prod.ps1
#
# A URL fica só na variável de ambiente desta sessão do terminal (não é salva em arquivo).
#
# Os comandos evitam aspas dentro de argumentos: o PowerShell 5.1 as remove ao chamar o docker.

$ErrorActionPreference = 'Stop'

$container = 'cbfv-db'
$banco = 'CBFV_DEVELOPMENT'
$arquivoDump = '/tmp/prod.sql'

function Falhar([string]$mensagem) {
    docker exec $container rm -f $arquivoDump 2>$null | Out-Null
    Write-Host ''
    Write-Host "ERRO: $mensagem" -ForegroundColor Red
    exit 1
}

if ([string]::IsNullOrWhiteSpace($env:PROD_DATABASE_URL)) {
    Falhar 'A variável PROD_DATABASE_URL está vazia. Rode $env:PROD_DATABASE_URL = "postgresql://..." neste mesmo terminal antes do script.'
}

$rodando = docker ps --filter "name=^$container$" --format '{{.Names}}'
if ($rodando -ne $container) {
    Falhar "Container $container não está rodando. Abra o Docker Desktop e rode 'docker compose up -d'."
}

# 1) Baixa a produção primeiro: se falhar, o banco local continua intacto.
Write-Host 'Baixando dados de produção (pode levar alguns minutos)...'
docker exec $container pg_dump --no-owner --no-acl --file=$arquivoDump "--dbname=$env:PROD_DATABASE_URL"
if ($LASTEXITCODE -ne 0) { Falhar 'Não foi possível baixar o banco de produção (veja a mensagem acima). O banco local não foi alterado.' }

# 2) Recria o banco local.
Write-Host "Recriando banco local $banco..."
docker exec $container dropdb -U postgres --if-exists --force $banco
if ($LASTEXITCODE -ne 0) { Falhar 'Falha ao apagar o banco local.' }
docker exec $container createdb -U postgres $banco
if ($LASTEXITCODE -ne 0) { Falhar 'Falha ao criar o banco local.' }

# 3) Restaura o dump.
Write-Host 'Restaurando no banco local...'
docker exec $container psql -q -U postgres -d $banco -v ON_ERROR_STOP=1 --output=/dev/null --file=$arquivoDump
if ($LASTEXITCODE -ne 0) { Falhar 'Falha ao restaurar os dados no banco local (veja a mensagem acima).' }

docker exec $container rm -f $arquivoDump | Out-Null
Write-Host ''
Write-Host 'Pronto! Banco local atualizado com os dados de produção.' -ForegroundColor Green
