# Object mapping with AutoMapper

The solution uses AutoMapper 16.2.0 for straightforward object-to-contract transformations. Each independently deployed service owns its profiles so service-specific models and contracts do not leak across microservice boundaries:

- Orders/Application: `src/Marketly.Application/Mapping/OrderMappingProfile.cs`
- Catalog: `src/Marketly.Catalog.Api/Mapping/CatalogMappingProfile.cs`
- Identity: `src/Marketly.Identity.Api/Mapping/IdentityMappingProfile.cs`

The API services register only their own profile assembly at startup. The Orders application layer registers its profile alongside its use cases. Extend the owning profile when adding a new DTO mapping, then inject `IMapper` into the application service or controller that needs it.

```csharp
CreateMap<NewEntity, NewResponse>();
```

Keep business decisions, validation, authorization, pricing, and state transitions in controllers/services/domain code; profiles should describe data transformation only. Do not automatically use `ReverseMap()` for request DTOs: explicitly map allowed fields so a client cannot set identifiers, ownership, payment state, or other protected fields by sending extra JSON properties.

AutoMapper 16 supports license-key configuration through the `AUTOMAPPER_LICENSE_KEY` environment variable. Compose forwards this optional `.env` setting to Orders, Catalog, and Identity. Supply a valid key through your secret store/container environment if your usage requires one; do not commit the key itself to appsettings, Compose, or source control. Review AutoMapper's current [license terms](https://automapper.io/) before commercial deployment. Local startup without a key can log a missing-license warning.

Mapping profiles should be covered by configuration validation and focused mapping tests when the test project is introduced. AutoMapper provides `AssertConfigurationIsValid()` for detecting unmapped destination members.
