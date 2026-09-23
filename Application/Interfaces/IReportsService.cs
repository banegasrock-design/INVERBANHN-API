using System.Collections.Generic;
using System.Threading.Tasks;
using Application.DTOs.Reports;
using Application.Common;

namespace Application.Interfaces;

public interface IReportsService
{
    Task<Result<IEnumerable<BankExportDto>>> GetBankExportsAsync();
}
