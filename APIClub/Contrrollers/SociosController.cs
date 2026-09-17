using APIClub.Application.Dtos.Socios;
using APIClub.Domain.GestionSocios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace APIClub.Contrrollers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SociosController : ControllerBase
    {
        private readonly ISociosManagmentService _SocioService;
        public SociosController(ISociosManagmentService socioService)
        {
            _SocioService = socioService;
        }

        [HttpPost]
        public async Task<IActionResult> CargarSocio([FromBody] CreateSocioDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _SocioService.cargarSocio(dto);

            if (result.Exit != true)
            {
                if (result.Errorcode == 409)
                {
                    return Conflict(new { message = result.Errormessage, data = result.Data });
                }
                return StatusCode(result.Errorcode, result.Errormessage);
            }

            return Ok(result.Data);
        }

        [HttpGet("padroncompleto")]
        public async Task<IActionResult> GetAllSocios([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var result = await _SocioService.GetPadronSocios(pageNumber, pageSize);

            if (result.Exit != true)
            {
                return StatusCode(result.Errorcode, result.Errormessage);
            }

            return Ok(result.Data);
        }


        [HttpPost("reactivar/{id}")]
        public async Task<IActionResult> ReactivarSocio(int id)
        {
            var result = await _SocioService.ReactivarSocio(id);

            if (result.Exit != true)
            {
                return StatusCode(result.Errorcode, result.Errormessage);
            }

            return Ok(result.Data);
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchSocios([FromQuery] string q, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            q = string.IsNullOrWhiteSpace(q) ? string.Empty : System.Text.RegularExpressions.Regex.Replace(q, @"\s+", " ").Trim();
            if (string.IsNullOrWhiteSpace(q))
                return BadRequest(new { mensaje = "Debe indicar un término de búsqueda." });
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 50) pageSize = 10;

            var result = await _SocioService.SearchSociosAsync(q, page, pageSize);
            if (result.Exit != true)
            {
                return StatusCode(result.Errorcode, new { mensaje = result.Errormessage });
            }
            return Ok(result.Data);
        }

        [HttpGet("{dni}")]
        public async Task<IActionResult> GetSocioByDni(string dni)
        {
            dni = string.IsNullOrWhiteSpace(dni) ? string.Empty : System.Text.RegularExpressions.Regex.Replace(dni, @"\s+", "").Trim();
            if (string.IsNullOrWhiteSpace(dni))
                return BadRequest(new { mensaje = "Debe indicar un DNI válido." });
            var result = await _SocioService.GetSocioByDni(dni);

            if (result.Exit != true)
            {
                // Usa el mismo manejo de errores que en CargarSocio
                return StatusCode(result.Errorcode, new { mensaje = result.Errormessage });
            }

            // Si todo salió bien, devolvés el DTO con código 200
            return Ok(result.Data);
        }


        [HttpGet("byId/{id}")]
        public async Task<IActionResult> GetSocioById(int id)
        {
            var result = await _SocioService.GetSocioById(id);

            if (result.Exit != true)
            {
                return StatusCode(result.Errorcode, result.Errormessage);
            }

            return Ok(result.Data);
        }

        [HttpGet("full/{id}")]
        public async Task<IActionResult> GetFullSocioById(int id)
        {
            var result = await _SocioService.GetFullSocioById(id);

            if (result.Exit != true)
            {
                return StatusCode(result.Errorcode, result.Errormessage);
            }

            return Ok(result.Data);
        }


        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateSocio(int id, [FromBody] UpdateSocio dto)
        {
            var result = await _SocioService.UpdateSocio(id, dto);

            if (result.Exit != true)
            {
                return StatusCode(result.Errorcode, result.Errormessage);
            }

            return Ok(result.Data);
        }


        [HttpGet("deudores")]
        public async Task<IActionResult> GetSociosDeudores([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var result = await _SocioService.GetSociosDeudores(pageNumber, pageSize);

            if (result.Exit != true)
            {
                return StatusCode(result.Errorcode, result.Errormessage);
            }

            return Ok(result.Data);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> RemoveSocio(int id)
        {
            var result = await _SocioService.RemoveSocio(id);

            if (!result.Exit)
            {
                return StatusCode(result.Errorcode, result.Errormessage);
            }

            return Ok(result.Data);
        }
    }
}
