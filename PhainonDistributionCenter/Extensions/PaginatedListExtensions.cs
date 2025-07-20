using PhainonDistributionCenter.Abstractions.Entities;
using PhainonDistributionCenter.ComponentModels;

namespace PhainonDistributionCenter.Extensions;

public static class PaginatedListExtensions
{
    public static async Task<PaginatedList<T>> ToPaginatedListAsync<T>(this IQueryable<T> queryable, int pageIndex, int pageSize
        , bool decreasing = false, bool orderByUpdatedTime = false) where T : IObjectWithTime
    {
        return await PaginatedList<T>.CreateAsync(queryable, pageIndex, pageSize, decreasing, orderByUpdatedTime);
    } 
    
    public static PaginatedList<T> ToPaginatedList<T>(this IList<T> list, int pageIndex, int pageSize) where T : IObjectWithTime
    {
        return PaginatedList<T>.CreateFromRawList(list, pageIndex, pageSize);
    }
}