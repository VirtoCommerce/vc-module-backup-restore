using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Permissions = Virtocommerce.Backuprestore.Core.ModuleConstants.Security.Permissions;

namespace Virtocommerce.Backuprestore.Web.Controllers.Api;

[Authorize]
[Route("api/backup-restore")]
public class BackuprestoreController : Controller
{
    // GET: api/backup-restore
    /// <summary>
    /// Get message
    /// </summary>
    /// <remarks>Return "Hello world!" message</remarks>
    [HttpGet]
    [Route("")]
    [Authorize(Permissions.Read)]
    public ActionResult<string> Get()
    {
        return Ok(new { result = "Hello world!" });
    }
}
