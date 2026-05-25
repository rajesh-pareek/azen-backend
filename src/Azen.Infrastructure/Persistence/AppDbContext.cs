using Azen.Domain.Entities.App;
using Microsoft.EntityFrameworkCore;

namespace Azen.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    //DbSets - one per table
    public DbSet<Organisation> Organisations { get; set; }
    public DbSet<OrganisationMember> OrganisationMembers { get; set; }
    public DbSet<Shipment> Shipments { get; set; }
    public DbSet<ShipmentDocument> ShipmentDocuments { get; set; }
    public DbSet<ShareLink> ShareLinks { get; set; }
    public DbSet<ShipmentEvent> ShipmentEvents { get; set; }
    public DbSet<ShipmentRefSequence> ShipmentRefSequences { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        //Organisation 
        modelBuilder.Entity<Organisation>(entity =>
        {
            entity.ToTable("organisations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Slug).HasMaxLength(60).IsRequired();
            entity.HasIndex(entity => entity.Slug).IsUnique(); // slug must be unique across all orgs
            entity.Property(e => e.Plan).HasMaxLength(20).IsRequired().HasDefaultValue("mvp");
            entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).IsRequired().HasDefaultValueSql("now()");
        });

        //Organisation Members
        modelBuilder.Entity<OrganisationMember>(entity =>
        {
            entity.ToTable("organisation_members");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");

            //UserId is cross - db reference - no FK constraint, just a plain column
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.Role).HasMaxLength(20).IsRequired();
            entity.Property(e => e.SubRole).HasMaxLength(20).IsRequired().HasDefaultValue("member");
            entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(e => e.JoinedAt).IsRequired().HasDefaultValueSql("now()");

            //Fk to organisation - every member belongs to exactly one org
            entity.HasOne(e => e.Organisation)
            .WithMany()
            .HasForeignKey(e => e.OrganisationId)
            .OnDelete(DeleteBehavior.Restrict); // dont't cascade delete members when prg is deleted

            //one user can only be in a org once (prevents duplicate membership)
            entity.HasIndex(e => new { e.OrganisationId, e.UserId }).IsUnique();

            //Index for looking up all orgs a user belongs to (used during login)
            entity.HasIndex(e => e.UserId);

            //Index for listing members of an org filtered by role
            entity.HasIndex(e => new { e.OrganisationId, e.Role });
        });

        //Shipments
        // biggest configuration since Shipment has 3 FK references
        //to OrganisationMember (fleet owner, driver, created by)

        modelBuilder.Entity<Shipment>(entity =>
        {
            entity.ToTable("shipments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.ReferenceNumber).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ConsignorName).HasMaxLength(200);
            entity.Property(e => e.ConsignorPhone).HasMaxLength(15);
            entity.Property(e => e.ConsigneeName).HasMaxLength(200);
            entity.Property(e => e.ConsigneePhone).HasMaxLength(15);
            entity.Property(e => e.GoodsDescription).HasMaxLength(500);
            entity.Property(e => e.VehicleNumber).HasMaxLength(50);
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired().HasDefaultValue("created");

            //fleet owner fields
            entity.Property(e => e.FleetOwnerName).HasMaxLength(200);
            entity.Property(e => e.FleetOwnerPhone).HasMaxLength(15);
            entity.Property(e => e.FleetOwnerInSystem).IsRequired().HasDefaultValue(false);

            //Driver fields
            entity.Property(e => e.DriverName).HasMaxLength(200);
            entity.Property(e => e.DriverPhone).HasMaxLength(15);
            entity.Property(e => e.DriverInSystem).IsRequired().HasDefaultValue(false);

            entity.Property(e => e.CreatedAt).IsRequired().HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAt).IsRequired().HasDefaultValueSql("now()");

            //Fk to Organisation
            entity.HasOne(e => e.Organisation)
            .WithMany()
            .HasForeignKey(e => e.OrganisationId)
            .OnDelete(DeleteBehavior.Restrict);

            //Fk to OrganisationMember for fleet owner (nullable- might not be assigned)
            // we must configure these explicitly because there are 3 fks to the same table
            //ef core can't auto resolve which navigation goes to which FK when there are multiple

            entity.HasOne(e => e.FleetOwnerMember)
            .WithMany()
            .HasForeignKey(e => e.FleetOwnerMemberId)
            .OnDelete(DeleteBehavior.Restrict);

            // fK to OrganisationMember for Driver (nullable)
            entity.HasOne(e => e.DriverMember)
            .WithMany()
            .HasForeignKey(e => e.DriverMemberId)
            .OnDelete(DeleteBehavior.Restrict);

            // Fk to OrganisationMember for createdBy (required - every shipment has a creator)
            entity.HasOne(e => e.CreatedByMember)
            .WithMany()
            .HasForeignKey(e => e.CreatedByMemberId)
            .OnDelete(DeleteBehavior.Restrict);

            //unique reference number per org - prevents duplicate shipment Ids within a company
            entity.HasIndex(e => new { e.OrganisationId, e.ReferenceNumber }).IsUnique();

            //Index for dashboard: list shipments  by org + status
            entity.HasIndex(e => new { e.OrganisationId, e.Status });

            //Index for fleet owner's view : find shipments assigned to them
            entity.HasIndex(e => e.FleetOwnerMemberId);

            //Index for Driver's view: find shipments assigned to them
            entity.HasIndex(e => e.DriverMemberId);
        });

        // --shipment documents
        modelBuilder.Entity<ShipmentDocument>(entity =>
        {
            entity.ToTable("shipment_documents");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.DocType).HasMaxLength(30).IsRequired();
            entity.Property(e => e.StorageKey).HasMaxLength(500).IsRequired();
            entity.Property(e => e.OriginalFileName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.FileSizeBytes).IsRequired();
            entity.Property(e => e.MimeType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.UploaderRole).HasMaxLength(20).IsRequired();
            entity.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).IsRequired().HasDefaultValueSql("now()");

            //Fk to shipment - casacde delete: if shiopment is delete, it's docs go too
            entity.HasOne(e => e.Shipment)
            .WithMany()
            .HasForeignKey(e => e.ShipmentId)
            .OnDelete(DeleteBehavior.Cascade);

            //Fk to organisationMember - who uploaded this doc
            entity.HasOne(e => e.UploadedByMember)
            .WithMany()
            .HasForeignKey(e => e.UploadedByMemberId)
            .OnDelete(DeleteBehavior.Restrict);

            //index for listing active (non-deleted) docs of a shipment
            entity.HasIndex(e => new { e.ShipmentId, e.IsDeleted });


        });

        //share links

        modelBuilder.Entity<ShareLink>(entity =>
        {
            entity.ToTable("share_links");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.Token).HasMaxLength(12).IsRequired();
            entity.HasIndex(e => e.Token).IsUnique(); // token must be globally unique for url resolution

            entity.Property(e => e.ExpiresAt).IsRequired();
            entity.Property(e => e.IsRevoked).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.VisibleDocTypes).IsRequired().HasDefaultValue("[]");
            entity.Property(e => e.AccessCount).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.CreatedAt).IsRequired().HasDefaultValueSql("now()");


            //Fk to shipment
            entity.HasOne(e => e.Shipment)
            .WithMany()
            .HasForeignKey(e => e.ShipmentId)
            .OnDelete(DeleteBehavior.Restrict);

            // fk tp organisationMember - who created the link
            entity.HasOne(e => e.CreatedByMember)
            .WithMany()
            .HasForeignKey(e => e.CreatedByMemberId)
            .OnDelete(DeleteBehavior.Restrict);

            //Index for listing active links per shipment
            entity.HasIndex(e => new { e.ShipmentId, e.IsRevoked });

        });

        // shipment events
        modelBuilder.Entity<ShipmentEvent>(entity =>
        {
            entity.ToTable("shipment_events");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.EventType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ActorRole).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Payload).IsRequired().HasDefaultValue("{}");
            entity.Property(e => e.CreatedAt).IsRequired().HasDefaultValueSql("now()");

            //fk to shipment 
            entity.HasOne(e => e.Shipment)
            .WithMany()
            .HasForeignKey(e => e.ShipmentId)
            .OnDelete(DeleteBehavior.Restrict);

            //index for chronological audit trail per shipmemnt

            entity.HasIndex(e => new { e.ShipmentId, e.CreatedAt });

            //index for finding all actions be a specific actor
            entity.HasIndex(e => new { e.ActorId, e.CreatedAt });
        });

        //shipment Ref Sequence

        //counter table - opne row per org for auto-generating reference numbers

        modelBuilder.Entity<ShipmentRefSequence>(entity =>
        {
            entity.ToTable("shipment_ref_sequences");

            //organsiationsId is the primary key (no seperate ID columns)
            entity.HasKey(e => e.OrganisationId);
            entity.Property(e => e.LastSeq).IsRequired().HasDefaultValue(0);

            //fk to orgnaisation
            entity.HasOne(e => e.Organisation)
            .WithMany()
            .HasForeignKey(e => e.OrganisationId)
            .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
