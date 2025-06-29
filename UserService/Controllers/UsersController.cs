using Microsoft.AspNetCore.Mvc;
using UserService.Data;
using UserService.Models;

namespace UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController(AppDbContext db) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<User>> Get(Guid id)
    {
        var user = await db.Users.FindAsync(id);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost]
    public async Task<ActionResult<User>> Create(User user)
    {
        user.Id = Guid.NewGuid();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = user.Id }, user);
    }
}