using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AnadesaApi.DTOs;
using AnadesaApi.Services;

namespace AnadesaApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GruposController : ControladorConPermisos
{
    private readonly GrupoService _grupoService;
    private readonly PermisoService _permiso;

    public GruposController(GrupoService grupoService, PermisoService permisoService, AlcanceService alcance)
        : base(permisoService, alcance)
    {
        _grupoService = grupoService;
        _permiso = permisoService;
    }

    protected override int IdModulo => 5;
    protected override string NombreModulo => "Grupos";

    /// <summary>Los grupos se consultan desde Actividades (5), Evaluación (7) y Configuración (9).</summary>
    private async Task<bool> PuedeConsultarGrupos()
    {
        var todos = await TodosLosPermisos();
        return todos.Any(m => (m.id_modulo == 5 || m.id_modulo == 7 || m.id_modulo == 9)
                              && m.permisos.Contains("Consultar"));
    }

    /// <summary>
    /// Administrador: siempre. Coordinador/Facilitador: solo si el administrador
    /// le otorgó el permiso individual (usuarios.puede_crear_grupos, switch en la
    /// lista de usuarios). El alcance por programa (BloqueoGrupo / DeMiPrograma /
    /// ParticipanteEnAlcance) limita a cada facilitador a SU programa.
    /// </summary>
    private async Task<bool> PuedeGestionarGrupos()
    {
        var esAdmin = User.FindAll(ClaimTypes.Role).Select(c => c.Value)
            .Any(r => r.Contains("Administrador"));
        if (esAdmin) return true;
        return await _permiso.PuedeGestionarGruposAsync(IdUsuario);
    }

    /// <summary>NotFound si el grupo no existe, ProgramaAjeno si es de otro programa.</summary>
    private async Task<ActionResult?> BloqueoGrupo(int idGrupo)
    {
        if (Alcance == null) return null;
        var idPrograma = await _alcance.ProgramaDeGrupoAsync(idGrupo);
        if (idPrograma == null) return NotFound();
        return idPrograma == IdProgramaDelAlcance ? null : ProgramaAjeno();
    }

    [HttpGet]
    public async Task<ActionResult<List<GrupoDto>>> ObtenerTodos()
    {
        if (!await PuedeConsultarGrupos()) return SinPermiso("consultar");
        var grupos = await _grupoService.ObtenerTodosAsync();
        if (Alcance != null)
            grupos = grupos.Where(g => g.id_programa == IdProgramaDelAlcance || DeMiPrograma(g.programa_nombre)).ToList();
        return Ok(grupos);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<GrupoDto>> ObtenerPorId(int id)
    {
        if (!await PuedeConsultarGrupos()) return SinPermiso("consultar");
        var grupo = await _grupoService.ObtenerPorIdAsync(id);
        if (grupo == null) return NotFound();
        if (Alcance != null && grupo.id_programa != IdProgramaDelAlcance && !DeMiPrograma(grupo.programa_nombre))
            return ProgramaAjeno();
        return Ok(grupo);
    }

    [HttpPost]
    public async Task<ActionResult<GrupoDto>> Crear([FromBody] CrearGrupoRequest request)
    {
        if (!await PuedeGestionarGrupos()) return SinPermiso("crear grupos");
        if (!DeMiPrograma(request.id_programa)) return ProgramaAjeno();
        try
        {
            var grupo = await _grupoService.CrearAsync(request);
            return CreatedAtAction(nameof(ObtenerPorId), new { id = grupo.id_grupo }, grupo);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<GrupoDto>> Actualizar(int id, [FromBody] ActualizarGrupoRequest request)
    {
        if (!await PuedeGestionarGrupos()) return SinPermiso("modificar grupos");
        if (await BloqueoGrupo(id) is ActionResult bloqueo) return bloqueo;
        if (!DeMiPrograma(request.id_programa)) return ProgramaAjeno();
        var grupo = await _grupoService.ActualizarAsync(id, request);
        if (grupo == null) return NotFound();
        return Ok(grupo);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        if (!await PuedeGestionarGrupos()) return SinPermiso("eliminar grupos");
        if (await BloqueoGrupo(id) is ActionResult bloqueo) return bloqueo;
        var result = await _grupoService.EliminarAsync(id);
        if (!result) return NotFound();
        return Ok(new { message = "Grupo eliminado exitosamente" });
    }

    [HttpPost("{id}/participantes")]
    public async Task<ActionResult<GrupoParticipanteDto>> AsignarParticipante(int id, [FromBody] AsignarParticipanteGrupoRequest request)
    {
        if (!await PuedeGestionarGrupos()) return SinPermiso("asignar participantes");
        if (await BloqueoGrupo(id) is ActionResult bloqueo) return bloqueo;
        if (!await ParticipanteEnAlcanceAsync(request.id_participante)) return ProgramaAjeno();
        // El participante debe ser del mismo programa que el grupo (Educación/Mujeres).
        var idProgGrupo = await _alcance.ProgramaDeGrupoAsync(id);
        if (idProgGrupo != null)
        {
            var idProgPart = AlcancePrograma.IdPorFragmento(await _alcance.TipoProgramaParticipanteAsync(request.id_participante));
            if (idProgPart != null && idProgPart != idProgGrupo)
                return BadRequest(new { message = "El participante no pertenece al programa de este grupo." });
        }
        var result = await _grupoService.AsignarParticipanteAsync(id, request);
        if (result == null) return BadRequest(new { message = "El participante ya está asignado a este grupo" });
        return Ok(result);
    }

    [HttpDelete("participantes/{idGrupoParticipante}")]
    public async Task<IActionResult> RemoverParticipante(int idGrupoParticipante)
    {
        if (!await PuedeGestionarGrupos()) return SinPermiso("remover participantes");
        if (Alcance != null)
        {
            var idGrupo = await _alcance.GrupoDeAsignacionAsync(idGrupoParticipante);
            if (idGrupo == null) return NotFound();
            if (await BloqueoGrupo(idGrupo.Value) is ActionResult bloqueo) return bloqueo;
        }
        var result = await _grupoService.RemoverParticipanteAsync(idGrupoParticipante);
        if (!result) return NotFound();
        return Ok(new { message = "Participante removido del grupo" });
    }

    /* ------------------------- ASISTENCIA DEL GRUPO ------------------------- */

    /// <summary>
    /// ¿Puede tomar asistencia del grupo? Administrador o permiso individual
    /// (puede_crear_grupos), o permiso Crear/Actualizar en Actividades (facilitadores).
    /// </summary>
    private async Task<bool> PuedeAsistirGrupos()
    {
        if (await PuedeGestionarGrupos()) return true;
        var todos = await TodosLosPermisos();
        var m5 = todos.FirstOrDefault(m => m.id_modulo == 5)?.permisos;
        return m5 != null && (m5.Contains("Actualizar") || m5.Contains("Crear"));
    }

    private static DateTime ParseFechaAsistencia(string? fecha) =>
        DateTime.TryParse(fecha, out var f) ? f.Date : DateTime.Today;

    /// <summary>Asistencia del grupo en ?fecha=yyyy-MM-dd (hoy por defecto): { id_participante: bool }.</summary>
    [HttpGet("{id}/asistencia")]
    public async Task<ActionResult<Dictionary<int, bool>>> ObtenerAsistencia(int id, [FromQuery] string? fecha)
    {
        if (!await PuedeConsultarGrupos()) return SinPermiso("consultar");
        if (await BloqueoGrupo(id) is ActionResult bloqueo) return bloqueo;
        return Ok(await _grupoService.ObtenerAsistenciaAsync(id, ParseFechaAsistencia(fecha)));
    }

    /// <summary>Marca presente/ausente de un miembro activo del grupo en la fecha (upsert).</summary>
    [HttpPost("{id}/asistencia")]
    public async Task<IActionResult> GuardarAsistencia(int id, [FromBody] AsistenciaGrupoRequest request)
    {
        if (!await PuedeAsistirGrupos()) return SinPermiso("tomar asistencia");
        if (await BloqueoGrupo(id) is ActionResult bloqueo) return bloqueo;
        var ok = await _grupoService.GuardarAsistenciaAsync(id, request.id_participante, ParseFechaAsistencia(request.fecha), request.presente);
        if (!ok) return BadRequest(new { message = "El participante no es miembro activo de este grupo." });
        return Ok(new { message = "Asistencia guardada" });
    }
}
