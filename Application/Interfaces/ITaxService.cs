using System;
using System.Threading.Tasks;
using Application.Common;

namespace Application.Interfaces;

public interface ITaxService
{
    Task<Result<bool>> ProcessOrderBillingAsync(Guid subOrderId);
}
