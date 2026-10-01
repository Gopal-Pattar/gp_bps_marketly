using AutoMapper;
using Marketly.Identity.Api.DTOs;
using Marketly.Identity.Api.Entities;

namespace Marketly.Identity.Api.Mapping;

/// <summary>Maps Identity-owned account entities to safe API response contracts.</summary>
public sealed class IdentityMappingProfile : Profile
{
    public IdentityMappingProfile() => CreateMap<CustomerAccount, CustomerProfileResponse>();
}
