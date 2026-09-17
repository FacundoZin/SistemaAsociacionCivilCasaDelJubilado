using APIClub.Application.Common;
using APIClub.Application.Dtos.Socios;

namespace APIClub.Domain.GestionSocios
{
    public interface ISociosManagmentService
    {
        Task<Result<PagedResult<SocioCardDto>>> GetSociosDeudores(int pageNumber, int pageSize);
        Task<Result<PagedResult<socioCardSinEstadoDto>>> GetPadronSocios(int pageNumber, int PageSize);
        Task<Result<object?>> cargarSocio(CreateSocioDto _dto);
        Task<Result<SocioDebtPreviewDto>> GetSocioByDni(string dni);
        Task<Result<object>> UpdateSocio(int id, UpdateSocio dto);
        Task<Result<object>> RemoveSocio(int id);
        Task<Result<object>> ReactivarSocio(int id);
        Task<Result<PreviewSocioDto>> GetSocioById(int id);
        Task<Result<FullSocioDto>> GetFullSocioById(int id);
        Task<Result<PagedResult<PreviewSocioDto>>> SearchSociosAsync(string query, int page, int pageSize);

    }
}
