using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using MockQueryable.Moq;
using Moq;

namespace OpsDash.UnitTests.Helpers;

public static class MockDbSetHelper
{
    /// <summary>
    /// Creates a <see cref="DbSet{TEntity}"/> mock backed by <paramref name="backingList"/> with async query support.
    /// </summary>
    public static Mock<DbSet<TEntity>> CreateMockDbSet<TEntity>(IList<TEntity> backingList)
        where TEntity : class
    {
        var mock = backingList.AsQueryable().BuildMockDbSet();
        mock.Setup(x => x.Add(It.IsAny<TEntity>())).Callback<TEntity>(backingList.Add);
        mock.Setup(x => x.AddAsync(It.IsAny<TEntity>(), It.IsAny<CancellationToken>()))
            .Returns((TEntity entity, CancellationToken _) =>
            {
                backingList.Add(entity);
                var entry = Mock.Of<EntityEntry<TEntity>>(e => e.Entity == entity);
                return ValueTask.FromResult(entry);
            });
        mock.Setup(x => x.AddRange(It.IsAny<IEnumerable<TEntity>>()))
            .Callback<IEnumerable<TEntity>>(entities =>
            {
                foreach (var e in entities)
                {
                    backingList.Add(e);
                }
            });
        return mock;
    }
}
