using Microsoft.EntityFrameworkCore;
using Server.Models;

namespace Server.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }

        public AppDbContext(DbContextOptions options) : base(options) { }
    }
}