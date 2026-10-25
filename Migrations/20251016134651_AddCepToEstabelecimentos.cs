using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace projetoX.Migrations
{
    /// <inheritdoc />
    public partial class AddCepToEstabelecimentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Cep",
                table: "Estabelecimentos",
                type: "longtext",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Cep",
                table: "Estabelecimentos");
        }
    }
}
