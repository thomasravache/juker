using Microsoft.EntityFrameworkCore;

namespace Juker.Modules.Playback.Infrastructure;

/// <summary>ARQ-03: segundo DbContext no mesmo módulo (a regra exige um DbContext por módulo).</summary>
public sealed class PlaybackReportingDbContext(DbContextOptions<PlaybackReportingDbContext> options) : DbContext(options);
