using Marketly.Application.Interfaces;
using Marketly.Application.DTOs;
using AutoMapper;

namespace Marketly.Application.Services;

public sealed class OrderService(IOrderRepository orders, IMapper mapper) : IOrderService
{
    /// <summary>Loads the customer's orders and maps domain entities to API response contracts.</summary>
    public async Task<IReadOnlyList<OrderSummary>> GetForCustomerAsync(Guid customerId, CancellationToken ct) =>
        mapper.Map<IReadOnlyList<OrderSummary>>(await orders.ForCustomerAsync(customerId, ct));
}
