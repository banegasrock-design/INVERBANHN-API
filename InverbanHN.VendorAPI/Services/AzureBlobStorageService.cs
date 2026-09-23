using Microsoft.Extensions.Configuration;

namespace InverbanHN.VendorAPI.Services
{
    public class AzureBlobStorageService : InverbanHN.Shared.Services.AzureBlobStorageService, IMediaService
    {
        public AzureBlobStorageService(IConfiguration configuration) : base(configuration)
        {
        }
    }
}
