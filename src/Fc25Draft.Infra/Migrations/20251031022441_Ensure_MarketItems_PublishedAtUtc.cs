using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class Ensure_MarketItems_PublishedAtUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Não faz nada - coluna já existe na migration anterior (20251029085220_InitialPostgres1)
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Não faz nada
        }
    }
}