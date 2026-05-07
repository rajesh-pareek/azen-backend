using Azen.Application.Interfaces;
using Azen.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Azen.Infrastructure.Services;

public class ShipmentRefService : IShipmentRefService
{
    private readonly AppDbContext appDb;

    public ShipmentRefService(AppDbContext appDb)
    {
        this.appDb = appDb;
    }

    public async Task<string> GenerateReferenceNumberAsync(Guid organisationId)
    {
        var refSeq = await appDb.ShipmentRefSequences.FirstOrDefaultAsync(r => r.OrganisationId == organisationId);

        if (refSeq == null)
            throw new Exception($"No ref sequence found for org {organisationId}");

        refSeq.LastSeq++;
        await appDb.SaveChangesAsync();
        return $"SHP-{DateTime.UtcNow.Year}-{refSeq.LastSeq:D5}";
    }
}