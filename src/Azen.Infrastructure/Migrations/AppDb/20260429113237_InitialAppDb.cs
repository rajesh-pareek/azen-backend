using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Azen.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class InitialAppDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "organisations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Plan = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "mvp"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organisations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "organisation_members",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SubRole = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "member"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    JoinedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organisation_members", x => x.Id);
                    table.ForeignKey(
                        name: "FK_organisation_members_organisations_OrganisationId",
                        column: x => x.OrganisationId,
                        principalTable: "organisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shipment_ref_sequences",
                columns: table => new
                {
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastSeq = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipment_ref_sequences", x => x.OrganisationId);
                    table.ForeignKey(
                        name: "FK_shipment_ref_sequences_organisations_OrganisationId",
                        column: x => x.OrganisationId,
                        principalTable: "organisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shipments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ConsignorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ConsignorPhone = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    ConsigneeName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ConsigneePhone = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    GoodsDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VehicleNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "created"),
                    FleetOwnerMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FleetOwnerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FleetOwnerPhone = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    FleetOwnerInSystem = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DriverMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DriverName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DriverPhone = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    DriverInSystem = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedByMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shipments_organisation_members_CreatedByMemberId",
                        column: x => x.CreatedByMemberId,
                        principalTable: "organisation_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shipments_organisation_members_DriverMemberId",
                        column: x => x.DriverMemberId,
                        principalTable: "organisation_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shipments_organisation_members_FleetOwnerMemberId",
                        column: x => x.FleetOwnerMemberId,
                        principalTable: "organisation_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shipments_organisations_OrganisationId",
                        column: x => x.OrganisationId,
                        principalTable: "organisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "share_links",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    Token = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsRevoked = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    VisibleDocTypes = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    AccessCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LastAccessAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_share_links", x => x.Id);
                    table.ForeignKey(
                        name: "FK_share_links_organisation_members_CreatedByMemberId",
                        column: x => x.CreatedByMemberId,
                        principalTable: "organisation_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_share_links_shipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalTable: "shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shipment_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    ShipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FileSizeBytes = table.Column<int>(type: "int", nullable: false),
                    MimeType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UploadedByMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploaderRole = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipment_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shipment_documents_organisation_members_UploadedByMemberId",
                        column: x => x.UploadedByMemberId,
                        principalTable: "organisation_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shipment_documents_shipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalTable: "shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shipment_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    ShipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorRole = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{}"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipment_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shipment_events_shipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalTable: "shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_organisation_members_OrganisationId_Role",
                table: "organisation_members",
                columns: new[] { "OrganisationId", "Role" });

            migrationBuilder.CreateIndex(
                name: "IX_organisation_members_OrganisationId_UserId",
                table: "organisation_members",
                columns: new[] { "OrganisationId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organisation_members_UserId",
                table: "organisation_members",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_organisations_Slug",
                table: "organisations",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_share_links_CreatedByMemberId",
                table: "share_links",
                column: "CreatedByMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_share_links_ShipmentId_IsRevoked",
                table: "share_links",
                columns: new[] { "ShipmentId", "IsRevoked" });

            migrationBuilder.CreateIndex(
                name: "IX_share_links_Token",
                table: "share_links",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shipment_documents_ShipmentId_IsDeleted",
                table: "shipment_documents",
                columns: new[] { "ShipmentId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_shipment_documents_UploadedByMemberId",
                table: "shipment_documents",
                column: "UploadedByMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_shipment_events_ActorId_CreatedAt",
                table: "shipment_events",
                columns: new[] { "ActorId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_shipment_events_ShipmentId_CreatedAt",
                table: "shipment_events",
                columns: new[] { "ShipmentId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_shipments_CreatedByMemberId",
                table: "shipments",
                column: "CreatedByMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_shipments_DriverMemberId",
                table: "shipments",
                column: "DriverMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_shipments_FleetOwnerMemberId",
                table: "shipments",
                column: "FleetOwnerMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_shipments_OrganisationId_ReferenceNumber",
                table: "shipments",
                columns: new[] { "OrganisationId", "ReferenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shipments_OrganisationId_Status",
                table: "shipments",
                columns: new[] { "OrganisationId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "share_links");

            migrationBuilder.DropTable(
                name: "shipment_documents");

            migrationBuilder.DropTable(
                name: "shipment_events");

            migrationBuilder.DropTable(
                name: "shipment_ref_sequences");

            migrationBuilder.DropTable(
                name: "shipments");

            migrationBuilder.DropTable(
                name: "organisation_members");

            migrationBuilder.DropTable(
                name: "organisations");
        }
    }
}
