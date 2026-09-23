using System;
using System.Threading.Tasks;
using Application.Common;

namespace Application.Interfaces;

public interface IDispatchService
{
    /// <summary>
    /// Procesa una orden pagada: la asigna a bodega central (Ecommerce) o notifica a la tienda (Tienda).
    /// </summary>
    Task<Result<string>> ProcessDispatchAsync(Guid subOrderId);
}
