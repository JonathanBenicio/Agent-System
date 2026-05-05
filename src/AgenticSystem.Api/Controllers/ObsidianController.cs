using AgenticSystem.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgenticSystem.Api.Controllers;

/// <summary>
/// GAP-12 — Endpoint para sincronização com Obsidian vault.
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ObsidianController : ControllerBase
{
    private readonly IObsidianSync _obsidianSync;

    public ObsidianController(IObsidianSync obsidianSync)
    {
        _obsidianSync = obsidianSync;
    }

    [HttpPost("index")]
    public async Task<IActionResult> IndexVault(CancellationToken ct)
    {
        await _obsidianSync.IndexExistingVaultAsync();
        return Ok(new { message = "Vault indexado com sucesso" });
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchNotes([FromQuery] string query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest(new { error = "Query é obrigatório" });

        var notes = await _obsidianSync.GetRelevantNotesAsync(query);
        return Ok(notes);
    }

    [HttpPost("watch/start")]
    public async Task<IActionResult> StartWatcher(CancellationToken ct)
    {
        await _obsidianSync.StartFileWatcherAsync();
        return Ok(new { message = "File watcher iniciado" });
    }
}
