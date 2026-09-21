using APIClub.Application.Common;
using APIClub.Application.Dtos.Socios;
using APIClub.Application.Helpers;
using APIClub.Domain.GestionSocios;
using APIClub.Domain.GestionSocios.Models;
using APIClub.Domain.GestionSocios.Repositories;
using APIClub.Domain.GestionSocios.Validations;

namespace APIClub.Application.Services
{
    public class SociosManagmentService : ISociosManagmentService
    {
        private readonly ISocioRepository _SocioRepository;
        private readonly ISocioIntegrityValidator _validator;

        public SociosManagmentService(ISocioRepository socioRepository, ISocioIntegrityValidator validator)
        {
            _SocioRepository = socioRepository;
            _validator = validator;
        }


        public async Task<Result<object?>> cargarSocio(CreateSocioDto _dto)
        {
            var result = await _validator.ValidateCargaSocio(_dto);

            if (!result.Exit)
            {
                if (result.Errorcode == 409) return Result<object?>.Conflict(result.Errormessage, result.Errorcode, result.Data);
                return Result<object?>.Error(result.Errormessage, result.Errorcode);
            }

            var hoy = DateOnly.FromDateTime(DateTime.Now);
            var fechaAsociacion = _dto.FechaAsociacion ?? hoy;

            if (fechaAsociacion > hoy)
            {
                return Result<object?>.Error("La fecha de asociación no puede ser mayor a la fecha actual.", 400);
            }

            if (fechaAsociacion.Year < 1900)
            {
                return Result<object?>.Error("La fecha de asociación no es válida (año debe ser >= 1900).", 400);
            }

            var socio = new Socio
            {
                Nombre = _dto.Nombre,
                Apellido = _dto.Apellido,
                Dni = System.Text.RegularExpressions.Regex.Replace(_dto.Dni ?? string.Empty, @"\s+", "").Trim(),
                Telefono = _dto.Telefono?.FormatearForWhatsapp().Data,
                Direcccion = _dto.Direcccion,
                LoteId = _dto.IdLote,
                Localidad = _dto.Localidad,
                FechaAsociacion = fechaAsociacion,
                IsActivo = true,
                PreferenciaDePago = _dto.PreferenciaDePago
            };

            await _SocioRepository.cargarSocio(socio);

            return Result<object?>.Exito(new ExistingSocio
            {
                Id = socio.Id,
                Nombre = socio.Nombre,
            });
        }

        public async Task<Result<object>> ReactivarSocio(int id)
        {
            var socio = await _SocioRepository.GetSocioByIdIgnoreFilter(id);

            if (socio is null)
            {
                return Result<object>.Error("No se encontró el socio.", 404);
            }

            if (socio.IsActivo)
            {
                return Result<object>.Error("El socio ya se encuentra activo.", 400);
            }

            socio.IsActivo = true;
            socio.FechaDeBaja = null;
            socio.FechaAsociacion = DateOnly.FromDateTime(DateTime.Now);

            await _SocioRepository.UpdateSocio(socio);

            return Result<object>.Exito(new { Message = "Socio reactivado correctamente.", SocioId = socio.Id });
        }

        public async Task<Result<SocioDebtPreviewDto>> GetSocioByDni(string dni)
        {
            dni = string.IsNullOrWhiteSpace(dni) ? string.Empty : System.Text.RegularExpressions.Regex.Replace(dni, @"\s+", "").Trim();
            if (string.IsNullOrWhiteSpace(dni))
            {
                return Result<SocioDebtPreviewDto>.Error("Debe indicar un DNI válido.", 400);
            }

            var socio = await _SocioRepository.GetSocioByDni(dni);

            if (socio is null)
            {
                return Result<SocioDebtPreviewDto>.Error("No se encontró un socio con ese DNI.", 404);
            }

            var hoy = DateTime.Now;
            int anioActual = hoy.Year;
            int semestreActual = hoy.Month <= 6 ? 1 : 2;


            var periodosAdeudados = new List<PeriodoAdeudadoDto>();
            int anioInicio = socio.FechaAsociacion.Year;
            int semestreInicio = socio.FechaAsociacion.Month <= 6 ? 1 : 2;

            for (int anio = anioInicio; anio <= anioActual; anio++)
            {
                int semestreDesde = (anio == anioInicio) ? semestreInicio : 1;
                int semestreHasta = (anio == anioActual) ? semestreActual : 2;

                for (int sem = semestreDesde; sem <= semestreHasta; sem++)
                {
                    if (!socio.HistorialCuotas.Any(c => c.Anio == anio && c.Semestre == sem))
                    {
                        periodosAdeudados.Add(new PeriodoAdeudadoDto
                        {
                            Anio = anio,
                            Semestre = sem
                        });
                    }
                }
            }

            var dto = new SocioDebtPreviewDto
            {
                Id = socio.Id,
                Nombre = socio.Nombre,
                Apellido = socio.Apellido,
                Dni = socio.Dni,
                Telefono = socio.Telefono?.FormatearForUserVisibility(),
                Direcccion = socio.Direcccion,
                nombreLote = socio.Lote?.NombreLote,
                IdLote = socio.LoteId,
                Localidad = socio.Localidad,
                PreferenciaDePago = socio.PreferenciaDePago,
                AdeudaCuotas = periodosAdeudados.Count > 0 ? true : false,
                PeriodosAdeudados = periodosAdeudados
            };

            return Result<SocioDebtPreviewDto>.Exito(dto);
        }

        public async Task<Result<PagedResult<SocioCardDto>>> GetSociosDeudores(int pageNumber, int pageSize)
        {
            var fechaPagoActual = DateOnly.FromDateTime(DateTime.Now);
            int anioActual = fechaPagoActual.Year;
            int semestreActual = fechaPagoActual.Month <= 6 ? 1 : 2;

            var (socios, totalCount) = await _SocioRepository.GetSociosDeudoresPaginado(anioActual, semestreActual, pageNumber, pageSize);

            var items = socios.Select(s => new SocioCardDto
            {
                Id = s.Id,
                Nombre = s.Nombre,
                Apellido = s.Apellido,
                Dni = s.Dni,
                Telefono = s.Telefono?.FormatearForUserVisibility(),
                Direcccion = s.Direcccion,
                nombreLote = s.Lote?.NombreLote,
                Localidad = s.Localidad,
                AdeudaCuotas = true,
            }).ToList();

            var result = new PagedResult<SocioCardDto>(items, totalCount, pageNumber, pageSize);

            return Result<PagedResult<SocioCardDto>>.Exito(result);
        }

        public async Task<Result<object>> UpdateSocio(int id, UpdateSocio dto)
        {
            var result = await _validator.ValidateUpdateSocio(id, dto);

            if (!result.Exit) return Result<object>.Error(result.Errormessage, result.Errorcode);

            var socio = result.Data;

            // Actualizar propiedades
            socio.Nombre = dto.Nombre;
            socio.Apellido = dto.Apellido;
            socio.Dni = System.Text.RegularExpressions.Regex.Replace(dto.Dni ?? string.Empty, @"\s+", "").Trim();
            socio.Telefono = dto.Telefono?.FormatearForWhatsapp().Data;
            socio.Direcccion = dto.Direcccion;
            socio.LoteId = dto.IdLote;
            socio.Localidad = dto.Localidad;
            socio.LoteId = dto.IdLote;
            socio.Localidad = dto.Localidad;
            socio.PreferenciaDePago = dto.PreferenciaDePago;

            if (dto.FechaAsociacion.HasValue)
            {
                var hoy = DateOnly.FromDateTime(DateTime.Now);
                if (dto.FechaAsociacion.Value > hoy) return Result<object>.Error("La fecha de asociación no puede ser mayor a la fecha actual.", 400);
                if (dto.FechaAsociacion.Value.Year < 1900) return Result<object>.Error("La fecha de asociación no es válida (año debe ser >= 1900).", 400);
                socio.FechaAsociacion = dto.FechaAsociacion.Value;
            }

            await _SocioRepository.UpdateSocio(socio);

            return Result<object>.Exito(new
            {
                Message = "Socio actualizado correctamente.",
                SocioId = socio.Id
            });


        }
        public async Task<Result<PreviewSocioDto>> GetSocioById(int id)
        {
            if (id <= 0)
            {
                return Result<PreviewSocioDto>.Error("El ID proporcionado no es válido.", 400);
            }

            var socio = await _SocioRepository.GetSocioById(id);

            if (socio is null)
            {
                return Result<PreviewSocioDto>.Error("No se encontró un socio con ese ID.", 404);
            }

            var dto = new PreviewSocioDto
            {
                Id = socio.Id,
                Nombre = socio.Nombre,
                Apellido = socio.Apellido,
                Dni = socio.Dni,
                Telefono = socio.Telefono?.FormatearForUserVisibility(),
                Direcccion = socio.Direcccion,
                nombreLote = socio.Lote?.NombreLote,
                IdLote = socio.LoteId,
                Localidad = socio.Localidad,
                PreferenciaDePago = socio.PreferenciaDePago,
                FechaAsociacion = socio.FechaAsociacion,
                AdeudaCuotas = false // Tendriamos que calcularlo si fuera necesario, pero para el form no hace falta
            };

            return Result<PreviewSocioDto>.Exito(dto);
        }

        public async Task<Result<FullSocioDto>> GetFullSocioById(int id)
        {
            if (id <= 0)
            {
                return Result<FullSocioDto>.Error("El ID proporcionado no es válido.", 400);
            }

            var socio = await _SocioRepository.GetSocioByIdWithCuotas(id);

            if (socio is null)
            {
                return Result<FullSocioDto>.Error("No se encontró un socio con ese ID.", 404);
            }

            var hoy = DateTime.Now;
            int anioActual = hoy.Year;
            int semestreActual = hoy.Month <= 6 ? 1 : 2;

            int maxAnioPagado = socio.HistorialCuotas.Any() ? Math.Max(anioActual, socio.HistorialCuotas.Max(c => c.Anio)) : anioActual;
            int maxSemestrePagado = (maxAnioPagado == anioActual) ? semestreActual : socio.HistorialCuotas.Where(c => c.Anio == maxAnioPagado).Max(c => (int?)c.Semestre) ?? 2;

            var periodosCuotas = new List<PeriodoCuotasDto>();
            int anioAsociacion = socio.FechaAsociacion.Year;
            int semestreInicio = socio.FechaAsociacion.Month <= 6 ? 1 : 2;

            for (int anio = anioAsociacion; anio <= maxAnioPagado; anio++)
            {
                int semestreDesde = (anio == anioAsociacion) ? semestreInicio : 1;
                int semestreHasta = (anio == maxAnioPagado) ? maxSemestrePagado : 2;

                for (int sem = semestreDesde; sem <= semestreHasta; sem++)
                {
                    var cuota = socio.HistorialCuotas.FirstOrDefault(c => c.Anio == anio && c.Semestre == sem);

                    if (cuota == null)
                    {
                        periodosCuotas.Add(new PeriodoCuotasDto
                        {
                            anio = anio,
                            semestre = sem,
                            pagado = false,
                            cuotaId = null,
                        });
                    }
                    else
                    {
                        periodosCuotas.Add(new PeriodoCuotasDto
                        {
                            FechaDePago = cuota.FechaPago,
                            ImportePagado = cuota.Monto,
                            MetodoDePago = cuota.FormaDePago,
                            anio = anio,
                            semestre = sem,
                            pagado = true,
                            cuotaId = cuota.Id,
                        });
                    }
                }
            }

            var dto = new FullSocioDto
            {
                Id = socio.Id,
                Nombre = socio.Nombre,
                Apellido = socio.Apellido,
                Dni = socio.Dni,
                Telefono = socio.Telefono?.FormatearForUserVisibility(),
                Direcccion = socio.Direcccion,
                NombreLote = socio.Lote?.NombreLote,
                IdLote = socio.LoteId,
                Localidad = socio.Localidad,
                FechaAsociacion = socio.FechaAsociacion,
                AdeudaCuotas = periodosCuotas.Any(c => c.pagado == false),
                HistorialCuotas = periodosCuotas.OrderBy(p => p.anio).ThenBy(p => p.semestre).ToList(),
            };

            return Result<FullSocioDto>.Exito(dto);
        }

        public async Task<Result<object>> RemoveSocio(int id)
        {
            if (id <= 0)
            {
                return Result<object>.Error("El ID proporcionado no es válido.", 400);
            }

            // 1. Validar Integridad antes de proceder
            var validationResult = await _validator.ValidateSocioDeletion(id);
            if (!validationResult.Exit)
            {
                return Result<object>.Error(validationResult.Errormessage, validationResult.Errorcode);
            }

            var socio = await _SocioRepository.GetSocioById(id);

            if (socio is null)
            {
                return Result<object>.Error("No se encontró un socio con ese ID.", 404);
            }

            await _SocioRepository.RemoveSocios(socio);

            return Result<object>.Exito(new
            {
                Message = "Socio eliminado correctamente.",
                SocioId = id
            });
        }

        public async Task<Result<PagedResult<socioCardSinEstadoDto>>> GetPadronSocios(int pageNumber, int PageSize)
        {
            var fechaPagoActual = DateOnly.FromDateTime(DateTime.Now);
            int anioActual = fechaPagoActual.Year;
            int semestreActual = fechaPagoActual.Month <= 6 ? 1 : 2;

            var (socios, totalCount) = await _SocioRepository.GetSociosPaginado(pageNumber, PageSize);

            var items = socios.Select(s => new socioCardSinEstadoDto
            {
                Id = s.Id,
                Nombre = s.Nombre,
                Apellido = s.Apellido,
                Dni = s.Dni,
                Telefono = s.Telefono?.FormatearForUserVisibility(),
                Direcccion = s.Direcccion,
                nombreLote = s.Lote?.NombreLote,
                Localidad = s.Localidad,
            }).ToList();

            var result = new PagedResult<socioCardSinEstadoDto>(items, totalCount, pageNumber, PageSize);

            return Result<PagedResult<socioCardSinEstadoDto>>.Exito(result);
        }

        public async Task<Result<PagedResult<PreviewSocioDto>>> SearchSociosAsync(string query, int page, int pageSize)
        {
            query = string.IsNullOrWhiteSpace(query) ? string.Empty : System.Text.RegularExpressions.Regex.Replace(query, @"\s+", " ").Trim();
            if (string.IsNullOrWhiteSpace(query))
                return Result<PagedResult<PreviewSocioDto>>.Error("Debe indicar un término de búsqueda.", 400);

            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 50) pageSize = 10;

            var (socios, totalCount) = await _SocioRepository.SearchSociosAsync(query, page, pageSize);

            var hoy = DateTime.Now;
            int anioActual = hoy.Year;
            int semestreActual = hoy.Month <= 6 ? 1 : 2;

            var items = socios.Select(s =>
            {
                bool adeudaCuotas = false;

                if (s.FechaAsociacion != default)
                {
                    int anioInicio = s.FechaAsociacion.Year;
                    int semestreInicio = s.FechaAsociacion.Month <= 6 ? 1 : 2;

                    for (int anio = anioInicio; anio <= anioActual && !adeudaCuotas; anio++)
                    {
                        int semestreDesde = (anio == anioInicio) ? semestreInicio : 1;
                        int semestreHasta = (anio == anioActual) ? semestreActual : 2;

                        for (int sem = semestreDesde; sem <= semestreHasta; sem++)
                        {
                            if (!(s.HistorialCuotas?.Any(c => c.Anio == anio && c.Semestre == sem) ?? false))
                            {
                                adeudaCuotas = true;
                                break;
                            }
                        }
                    }
                }

                return new PreviewSocioDto
                {
                    Id = s.Id,
                    Nombre = s.Nombre,
                    Apellido = s.Apellido,
                    Dni = s.Dni,
                    Telefono = s.Telefono?.FormatearForUserVisibility(),
                    Direcccion = s.Direcccion,
                    nombreLote = s.Lote?.NombreLote,
                    IdLote = s.LoteId,
                    Localidad = s.Localidad,
                    PreferenciaDePago = s.PreferenciaDePago,
                    FechaAsociacion = s.FechaAsociacion,
                    AdeudaCuotas = adeudaCuotas
                };
            }).ToList();

            var result = new PagedResult<PreviewSocioDto>(items, totalCount, page, pageSize);

            return Result<PagedResult<PreviewSocioDto>>.Exito(result);
        }
    }
}

