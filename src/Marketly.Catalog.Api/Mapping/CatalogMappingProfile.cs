using AutoMapper;
using Marketly.Catalog.Api.DTOs;
using Marketly.Catalog.Api.Data.Entities;

namespace Marketly.Catalog.Api.Mapping;

/// <summary>Maps Catalog persistence models into public API contracts.</summary>
public sealed class CatalogMappingProfile : Profile
{
    public CatalogMappingProfile()
    {
        CreateMap<CatalogProduct, CatalogProductResponse>();
        CreateMap<ProductPage, CatalogProductPageResponse>();
    }
}
