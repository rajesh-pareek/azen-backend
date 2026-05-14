namespace Azen.Application.Interfaces;

public interface IShipmentRefService
{
    Task<string> GenerateReferenceNumberAsync(Guid organisationId);
}
