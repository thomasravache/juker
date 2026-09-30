using Microsoft.EntityFrameworkCore;

namespace Juker.Modules.Playback.Infrastructure;

/// <summary>ARQ-03: primeiro DbContext do módulo Playback.</summary>
public sealed class PlaybackDbContext(DbContextOptions<PlaybackDbContext> options) : DbContext(options);
