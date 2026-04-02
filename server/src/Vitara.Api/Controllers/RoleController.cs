using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Divvy.Api.Data;
using Divvy.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Divvy.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "SuperAdminOnly")]
public class RoleController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public RoleController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<RoleEntity>>> GetAllRoles()
    {
        var roles = await _context.RoleEntities
            .OrderBy(r => r.Id)
            .ToListAsync();
        
        return Ok(roles);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<RoleEntity>> GetRole(int id)
    {
        var role = await _context.RoleEntities.FindAsync(id);
        
        if (role == null)
            return NotFound();
        
        return Ok(role);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateRole(int id, [FromBody] RoleEntity roleUpdate)
    {
        var role = await _context.RoleEntities.FindAsync(id);
        
        if (role == null)
            return NotFound();

        // Don't allow changing the ID or Name for system roles (1-5)
        // SuperAdmin, Administrator, Parent, Staff, CareRecipient are system roles
        if (id <= 5)
        {
            return BadRequest(new { message = "Cannot modify system roles" });
        }

        role.Description = roleUpdate.Description;
        role.IsActive = roleUpdate.IsActive;
        
        await _context.SaveChangesAsync();
        
        return Ok(role);
    }

    [HttpPost]
    public async Task<ActionResult<RoleEntity>> CreateRole([FromBody] CreateRoleRequest request)
    {
        if (await _context.RoleEntities.AnyAsync(r => r.Name == request.Name))
        {
            return BadRequest(new { message = "Role with this name already exists" });
        }

        var role = new RoleEntity
        {
            Name = request.Name,
            Description = request.Description,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.RoleEntities.Add(role);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetRole), new { id = role.Id }, role);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteRole(int id)
    {
        // System roles (1-5) cannot be deleted
        if (id <= 5)
        {
            return BadRequest(new { message = "Cannot delete system roles" });
        }

        var role = await _context.RoleEntities.FindAsync(id);
        
        if (role == null)
            return NotFound();

        // Check if any users have this role
        var usersWithRole = await _context.Users.AnyAsync(u => (int)u.Role == id);
        if (usersWithRole)
        {
            return BadRequest(new { message = "Cannot delete role that is assigned to users" });
        }

        _context.RoleEntities.Remove(role);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}

public class CreateRoleRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
