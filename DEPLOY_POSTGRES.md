# Deploy para PostgreSQL

## Pré-requisitos
- Banco PostgreSQL disponível (ex.: Render) com a connection string `postgresql://...`.
- Arquivos CSV exportados com os dados atuais e com as PKs originais preservadas.
- Ferramentas de linha de comando para executar scripts SQL (ex.: `psql`).

## Ordem de execução
1. **Criar extensões necessárias**  
   ```bash
   psql "$DATABASE_URL" -f db/postgres/01_create_extensions.sql
   ```
2. **Criar as tabelas base (sem relacionamentos)**  
   ```bash
   psql "$DATABASE_URL" -f db/postgres/02_create_tables.sql
   ```
3. **Importar os dados via CSV**  
   Ajuste os caminhos no arquivo `db/postgres/import_csv_template.sql` apontando para os CSVs exportados e execute:
   ```bash
   psql "$DATABASE_URL" -f db/postgres/import_csv_template.sql
   ```
4. **Criar índices, constraints e chaves estrangeiras**  
   ```bash
   psql "$DATABASE_URL" -f db/postgres/03_add_foreign_keys.sql
   ```
5. **Ajustar as sequences após a importação**  
   ```bash
   psql "$DATABASE_URL" -f db/postgres/04_post_import_fixes.sql
   ```

## Observações
- Não há geração automática de UUIDs no banco. As PKs devem ser importadas exatamente como estão nos CSVs.
- Os arquivos CSV devem conter cabeçalho (`csv header`).
- Caso precise importar apenas alguns dados, garanta que nenhuma constraint dependa deles antes de executar `03_add_foreign_keys.sql`.
- Após os passos acima, a aplicação pode ser executada normalmente apontando para o banco PostgreSQL.
