using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using AnadesaApi.Services;

namespace AnadesaApi.Controllers;

/// <summary>
/// Sirve /api/fotos/{nombre}: primero los bytes de la BD (sobreviven redeploys
/// de Render, cuyo disco es efimero) y si no estan, el archivo en disco.
/// Sin [Authorize] a proposito: las etiquetas &lt;img&gt; no envian el token JWT
/// (mismo nivel de exposicion que el fichero estatico /fotos/ de antes).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class FotosController : ControllerBase
{
    private readonly ParticipanteService _participanteService;
    private readonly IWebHostEnvironment _env;

    public FotosController(ParticipanteService participanteService, IWebHostEnvironment env)
    {
        _participanteService = participanteService;
        _env = env;
    }

    [HttpGet("{nombre}")]
    public async Task<IActionResult> Obtener(string nombre)
    {
        // Nombre saneado: solo el fichero, nunca una ruta (evita path traversal).
        nombre = Path.GetFileName(nombre);
        if (!Regex.IsMatch(nombre, @"^part_\d+_[0-9a-f]+\.(jpg|jpeg|png|gif|webp)$", RegexOptions.IgnoreCase))
            return NotFound();

        var bytes = await _participanteService.ObtenerContenidoFotoAsync(nombre);
        if (bytes != null && bytes.Length > 0)
            return File(bytes, ContentTypeDe(nombre));

        var disco = Path.Combine(RutasArchivos.DirectorioFotos(_env), nombre);
        if (System.IO.File.Exists(disco))
            return PhysicalFile(disco, ContentTypeDe(nombre));

        return NotFound();
    }

    private static string ContentTypeDe(string nombre) => Path.GetExtension(nombre).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        _ => "image/jpeg"
    };
}
