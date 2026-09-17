using APIClub.Application.Dtos.Socios;
using APIClub.Domain.GestionSocios.Models;
using APIClub.Domain.ModuloGestionCuotas.Models;

namespace APIClub.Domain.GestionSocios.Repositories
{
    public interface ISocioRepository
    {
        Task cargarSocio(Socio socio);
        Task<bool> SocioExists(string dni);
        Task<Socio?> GetSocioByDni(string dni);
        Task<Socio?> GetSocioByDniIgnoreFilter(string dni);
        Task<Socio?> GetSocioById(int id);
        Task UpdateSocio(Socio socio);
        void UpdateSocioWhitoutSave(Socio socio);
        Task<bool> SocioExistsForUpdate(string dni, int id);
        Task<Socio?> GetSocioByIdWithCuotas(int id);
        Task<Socio?> GetSocioByIdIgnoreFilter(int id);
        Task<List<Socio>> GetSociosDeudores(int anioActual, int semestreActual);
        Task<(List<Socio> Items, int TotalCount)> GetSociosDeudoresPaginado(int anioActual, int semestreActual, int pageNumber, int pageSize);
        Task RemoveSocios(Socio socio);
        Task<List<Cuota>> GetCuotasSocioById(int socioId);
        Task<(List<PreviewSocioForCobranzaDto> Items, int TotalCount)> GetSociosDeudoresByLote(int IdLote, int anioActual, int semestreActual, int pageNumber, int pageSize);
        Task<List<Socio>> GetSociosDeudoresWithPreferenceLinkDePagoPaginado(int anioActual, int semestreActual, int pageNumber, int pageSize);
        Task<(List<Socio> Items, int TotalCount)> GetSociosPaginado(int pageNumber, int pageSize);
        Task<(List<Socio> Items, int TotalCount)> SearchSociosAsync(string query, int page, int pageSize);
    }
}
