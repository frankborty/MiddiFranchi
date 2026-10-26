using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiddiFranchi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNegativeReturnHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Migrazione storica mantenuta per compatibilità con i database già aggiornati.
            // I dati dimostrativi specifici di questo ambiente non fanno parte del repository.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Le osservazioni dimostrative sono dati storici append-only e non vengono rimosse.
        }
    }
}
