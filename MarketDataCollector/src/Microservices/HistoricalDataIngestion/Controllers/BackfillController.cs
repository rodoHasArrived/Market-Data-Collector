using DataIngestion.Contracts.Services;
using DataIngestion.HistoricalService.Services;
using Microsoft.AspNetCore.Mvc;

namespace DataIngestion.HistoricalService.Controllers;

[ApiController]
[Route("api/v1/backfill")]
public class BackfillController : ControllerBase
{
    private readonly IBackfillJobManager _jobManager;

    public BackfillController(IBackfillJobManager jobManager)
    {
        _jobManager = jobManager;
    }

    [HttpPost]
    public IActionResult CreateJob([FromBody] BackfillRequest request)
    {
        var job = _jobManager.CreateJob(request);
        return CreatedAtAction(nameof(GetJob), new { id = job.JobId }, job);
    }

    [HttpGet("{id:guid}")]
    public IActionResult GetJob(Guid id)
    {
        var job = _jobManager.GetJob(id);
        return job != null ? Ok(job) : NotFound();
    }

    [HttpGet]
    public IActionResult GetActiveJobs()
    {
        return Ok(_jobManager.GetActiveJobs());
    }

    [HttpDelete("{id:guid}")]
    public IActionResult CancelJob(Guid id)
    {
        _jobManager.CancelJob(id);
        return NoContent();
    }
}
