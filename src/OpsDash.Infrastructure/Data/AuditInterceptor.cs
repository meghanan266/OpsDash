using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OpsDash.Application.Interfaces;
using OpsDash.Domain.Entities;
using OpsDash.Domain.Interfaces;

namespace OpsDash.Infrastructure.Data;

public sealed class AuditInterceptor : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly ICurrentUserService _currentUser;
    private readonly List<AuditLog> _pending = new();
    private bool _suppress;

    public AuditInterceptor(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (_suppress || eventData.Context is null)
        {
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        var userId = _currentUser.UserId;
        if (userId is null)
        {
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        foreach (var entry in eventData.Context.ChangeTracker
                     .Entries()
                     .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if (entry.Entity is AuditLog)
            {
                continue;
            }

            if (entry.Entity is not ITenantEntity tenantEntity)
            {
                continue;
            }

            var log = TryBuildAudit(entry, tenantEntity, userId.Value);
            if (log is not null)
            {
                _pending.Add(log);
            }
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        var r = await base.SavedChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);

        if (_suppress || eventData.Context is null)
        {
            return r;
        }

        if (_pending.Count == 0)
        {
            return r;
        }

        var batch = _pending.ToList();
        _pending.Clear();

        var db = eventData.Context;
        _suppress = true;
        try
        {
            db.Set<AuditLog>().AddRange(batch);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _suppress = false;
        }

        return r;
    }

    private static AuditLog? TryBuildAudit(EntityEntry entry, ITenantEntity tenantEntity, int userId)
    {
        var pk = string.Join(
            ",",
            entry.Properties.Where(p => p.Metadata.IsPrimaryKey()).Select(p => p.CurrentValue?.ToString() ?? string.Empty));

        if (string.IsNullOrEmpty(pk))
        {
            return null;
        }

        var entityName = entry.Entity.GetType().Name;
        var action = entry.State switch
        {
            EntityState.Added => "Created",
            EntityState.Deleted => "Deleted",
            EntityState.Modified => "Updated",
            _ => null,
        };

        if (action is null)
        {
            return null;
        }

        string? oldJson = null;
        string? newJson = null;

        if (entry.State is EntityState.Modified or EntityState.Deleted)
        {
            oldJson = SerializeSnapshot(entry, useOriginal: true);
        }

        if (entry.State is EntityState.Added or EntityState.Modified)
        {
            newJson = SerializeSnapshot(entry, useOriginal: false);
        }

        return new AuditLog
        {
            TenantId = tenantEntity.TenantId,
            UserId = userId,
            Action = action,
            EntityName = entityName,
            EntityId = pk,
            OldValues = oldJson,
            NewValues = newJson,
            Timestamp = DateTime.UtcNow,
        };
    }

    private static string SerializeSnapshot(EntityEntry entry, bool useOriginal)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var prop in entry.Properties)
        {
            if (prop.Metadata.IsPrimaryKey())
            {
                continue;
            }

            var value = useOriginal ? prop.OriginalValue : prop.CurrentValue;
            dict[prop.Metadata.Name] = value;
        }

        return JsonSerializer.Serialize(dict, JsonOptions);
    }
}
