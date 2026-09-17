using APIClub.Domain.Enums;
using APIClub.Application.Dtos.Socios;
using APIClub.Domain.GestionSocios.Models;
using APIClub.Domain.GestionSocios.Repositories;
using APIClub.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using APIClub.Application.Helpers;
using APIClub.Domain.ModuloGestionCuotas.Models;

namespace APIClub.Infrastructure.Persistence.Repositorio
{
    public class SociosRepository : ISocioRepository
    {
        private readonly AppDbcontext _Dbcontext;

        public SociosRepository(AppDbcontext dbcontext)
        {
            _Dbcontext = dbcontext;
        }

        public async Task cargarSocio(Socio socio)
        {
            _Dbcontext.Socios.Add(socio);
            await _Dbcontext.SaveChangesAsync();
        }

        public async Task<Socio?> GetSocioByDni(string dni)
        {
            dni = System.Text.RegularExpressions.Regex.Replace(dni ?? string.Empty, @"\s+", "").Trim();
            return await _Dbcontext.Socios
                .Include(s => s.HistorialCuotas)
                .Include(s => s.Lote)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Dni == dni);
        }

        public async Task<Socio?> GetSocioByDniIgnoreFilter(string dni)
        {
            dni = System.Text.RegularExpressions.Regex.Replace(dni ?? string.Empty, @"\s+", "").Trim();
            return await _Dbcontext.Socios
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Dni == dni);
        }

        public async Task<bool> SocioExists(string dni)
        {
            dni = System.Text.RegularExpressions.Regex.Replace(dni ?? string.Empty, @"\s+", "").Trim();
            return await _Dbcontext.Socios.IgnoreQueryFilters().AnyAsync(s => s.Dni == dni);
        }

        public async Task<bool> SocioExistsForUpdate(string dni, int id)
        {
            dni = System.Text.RegularExpressions.Regex.Replace(dni ?? string.Empty, @"\s+", "").Trim();
            return await _Dbcontext.Socios.IgnoreQueryFilters().AnyAsync(s => s.Dni == dni && s.Id != id);
        }

        public async Task<Socio?> GetSocioById(int id)
        {
            return await _Dbcontext.Socios
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task UpdateSocio(Socio socio)
        {
            _Dbcontext.Socios.Update(socio);
            await _Dbcontext.SaveChangesAsync();
        }

        public void UpdateSocioWhitoutSave(Socio socio)
        {
            _Dbcontext.Socios.Update(socio);
        }

        public async Task<Socio?> GetSocioByIdIgnoreFilter(int id)
        {
            return await _Dbcontext.Socios.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<Socio?> GetSocioByIdWithCuotas(int id)
        {
            var FechaAsociacionSocio = await _Dbcontext.Socios
                .Select(s => new { s.Id, s.FechaAsociacion })
                .FirstOrDefaultAsync(s => s.Id == id);

            int semestreIngreso = FechaAsociacionSocio!.FechaAsociacion.Month <= 6 ? 1 : 2;

            return await _Dbcontext.Socios
                .Include(s => s.HistorialCuotas.Where(c => c.Anio > FechaAsociacionSocio.FechaAsociacion.Year || (c.Anio == FechaAsociacionSocio.FechaAsociacion.Year && c.Semestre >= semestreIngreso)))
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<List<Socio>> GetSociosDeudores(int anioActual, int semestreActual)
        {
            var deudores = await _Dbcontext.Socios
                .Where(s => !s.HistorialCuotas.Any(c => c.Anio == anioActual && c.Semestre == semestreActual))
                .AsNoTracking()
                .ToListAsync();

            return deudores;
        }

        public async Task<(List<Socio> Items, int TotalCount)> GetSociosDeudoresPaginado(int anioActual, int semestreActual, int pageNumber, int pageSize)
        {
            var query = _Dbcontext.Socios
                .Include(s => s.Lote)
                .AsNoTracking()
                .Where(s => !s.HistorialCuotas.Any(c => c.Anio == anioActual && c.Semestre == semestreActual));

            int totalCount = await query.CountAsync();

            var items = await query
                .OrderBy(s => s.Apellido)
                .ThenBy(s => s.Nombre)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<(List<Socio> Items, int TotalCount)> GetSociosPaginado(int pageNumber, int pageSize)
        {
            var query = _Dbcontext.Socios
                .Include(s => s.Lote)
                .AsNoTracking();

            int totalCount = await query.CountAsync();

            var items = await query
                .OrderBy(s => s.Apellido)
                .ThenBy(s => s.Nombre)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task RemoveSocios(Socio socio)
        {
            socio.IsActivo = false;
            socio.FechaDeBaja = DateOnly.FromDateTime(DateTime.Now);
            _Dbcontext.Socios.Update(socio);
            await _Dbcontext.SaveChangesAsync();
        }

        public async Task<List<Cuota>> GetCuotasSocioById(int socioId)
        {
            return await _Dbcontext.Cuotas
            .Where(c => c.SocioId == socioId)
            .OrderByDescending(c => c.FechaPago)
            .AsNoTracking()
            .ToListAsync();
        }

        public async Task<(List<PreviewSocioForCobranzaDto> Items, int TotalCount)> GetSociosDeudoresByLote(int IdLote, int anioActual, int semestreActual, int pageNumber, int pageSize)
        {
            var query = _Dbcontext.Socios.
                Where(s => s.LoteId == IdLote && s.PreferenciaDePago == MetodosDePago.Cobrador && !s.HistorialCuotas
                .Any(c => c.Anio == anioActual && c.Semestre == semestreActual))
                .AsNoTracking();

            int totalCount = await query.CountAsync();

            // Proyectamos a un tipo anónimo para traer solo lo necesario de la DB
            var socios = await query
                .OrderBy(s => s.Apellido)
                .ThenBy(s => s.Nombre)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new
                {
                    s.Id,
                    s.Nombre,
                    s.Apellido,
                    s.Dni,
                    s.Telefono,
                    s.Direcccion,
                    s.FechaAsociacion,
                    CuotasPagas = s.HistorialCuotas.Select(c => new { c.Anio, c.Semestre }).ToList()
                })
                .ToListAsync();

            // Transformamos al DTO final
            var dtoSocios = socios.Select(s =>
            {
                var periodosAdeudados = new List<PeriodoAdeudadoDto>();
                int anioInicio = s.FechaAsociacion.Year;
                int semestreInicio = s.FechaAsociacion.Month <= 6 ? 1 : 2;

                for (int anio = anioInicio; anio <= anioActual; anio++)
                {
                    int semestreDesde = (anio == anioInicio) ? semestreInicio : 1;
                    int semestreHasta = (anio == anioActual) ? semestreActual : 2;

                    for (int sem = semestreDesde; sem <= semestreHasta; sem++)
                    {
                        if (!s.CuotasPagas.Any(c => c.Anio == anio && c.Semestre == sem))
                        {
                            periodosAdeudados.Add(new PeriodoAdeudadoDto
                            {
                                Anio = anio,
                                Semestre = sem
                            });
                        }
                    }
                }

                return new PreviewSocioForCobranzaDto
                {
                    Id = s.Id,
                    Nombre = s.Nombre,
                    Apellido = s.Apellido,
                    Dni = s.Dni,
                    Telefono = s.Telefono?.FormatearForUserVisibility(),
                    Direcccion = s.Direcccion,
                    PeriodosAdeudados = periodosAdeudados
                };

            }).ToList();

            return (dtoSocios, totalCount);
        }

        public async Task<List<Socio>> GetSociosDeudoresWithPreferenceLinkDePagoPaginado(int anioActual, int semestreActual, int pageNumber, int pageSize)
        {
            var items = await _Dbcontext.Socios
                .Where(s => s.PreferenciaDePago == MetodosDePago.LinkDePago &&
                !s.HistorialCuotas.Any(c => c.Anio == anioActual && c.Semestre == semestreActual))
                .AsNoTracking()
                .OrderBy(s => s.Apellido)
                .ThenBy(s => s.Nombre)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return items;
        }

        public async Task<(List<Socio> Items, int TotalCount)> SearchSociosAsync(string query, int page, int pageSize)
        {
            query = query == null ? string.Empty : System.Text.RegularExpressions.Regex.Replace(query, @"\s+", " ").Trim();
            if (string.IsNullOrWhiteSpace(query))
                return (new List<Socio>(), 0);

            var lowerQuery = query.ToLower();

            var baseQuery = _Dbcontext.Socios
                .Include(s => s.Lote)
                .Include(s => s.HistorialCuotas)
                .AsNoTracking()
                .Where(s => s.Dni.ToLower().Contains(lowerQuery)
                    || s.Nombre.ToLower().Contains(lowerQuery)
                    || s.Apellido.ToLower().Contains(lowerQuery)
                    || (s.Nombre + " " + s.Apellido).ToLower().Contains(lowerQuery)
                    || (s.Apellido + " " + s.Nombre).ToLower().Contains(lowerQuery));

            int totalCount = await baseQuery.CountAsync();

            var items = await baseQuery
                .OrderBy(s => s.Apellido)
                .ThenBy(s => s.Nombre)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }
    }
}
