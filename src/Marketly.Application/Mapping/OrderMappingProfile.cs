using AutoMapper;
using Marketly.Application.DTOs;
using Marketly.Domain;

namespace Marketly.Application.Mapping;

/// <summary>Maps Orders-owned domain entities into stable customer-facing response contracts.</summary>
public sealed class OrderMappingProfile : Profile
{
    public OrderMappingProfile()
    {
        CreateMap<OrderItem, OrderLineSummary>();
        CreateMap<Order, ShippingAddress>()
            .ForCtorParam(nameof(ShippingAddress.Name), options => options.MapFrom(source => source.ShippingName))
            .ForCtorParam(nameof(ShippingAddress.AddressLine1), options => options.MapFrom(source => source.ShippingAddressLine1))
            .ForCtorParam(nameof(ShippingAddress.AddressLine2), options => options.MapFrom(source => source.ShippingAddressLine2))
            .ForCtorParam(nameof(ShippingAddress.City), options => options.MapFrom(source => source.ShippingCity))
            .ForCtorParam(nameof(ShippingAddress.Region), options => options.MapFrom(source => source.ShippingRegion))
            .ForCtorParam(nameof(ShippingAddress.PostalCode), options => options.MapFrom(source => source.ShippingPostalCode))
            .ForCtorParam(nameof(ShippingAddress.CountryCode), options => options.MapFrom(source => source.ShippingCountryCode));
        CreateMap<Order, OrderSummary>()
            .ForCtorParam(nameof(OrderSummary.ShippingAddress), options => options.MapFrom(source => source))
            .ForCtorParam(nameof(OrderSummary.Items), options => options.MapFrom(source => source.Items));
        CreateMap<Order, OrderReceipt>();
    }
}
