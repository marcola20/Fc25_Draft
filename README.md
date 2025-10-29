# FC25 Draft

## Migrações EF Core

### SQL Server — DEV (LocalDB)

dotnet ef migrations add InitSqlServer \
  --project src/Fc25Draft.Infra.Migrations.SqlServer \
  --startup-project src/Fc25Draft.Web \
  --context DraftDbContext

dotnet ef database update \
  --project src/Fc25Draft.Infra.Migrations.SqlServer \
  --startup-project src/Fc25Draft.Web \
  --context DraftDbContext

### PostgreSQL — PROD (Render)

dotnet ef migrations add InitPostgres \
  --project src/Fc25Draft.Infra.Migrations.PostgreSQL \
  --startup-project src/Fc25Draft.Web \
  --context DraftDbContext

# Aplicar em produção é SEMPRE manual/consentido (nunca no startup):
dotnet ef database update \
  --project src/Fc25Draft.Infra.Migrations.PostgreSQL \
  --startup-project src/Fc25Draft.Web \
  --context DraftDbContext
