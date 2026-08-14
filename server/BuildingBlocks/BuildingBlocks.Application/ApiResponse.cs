using System.Collections.ObjectModel;

namespace BuildingBlocks.Application;

/// <summary>
///     Standard API response wrapper
/// </summary>
public class ApiResponse
{
    public bool Success { get; set; } = true;
    public string? Message { get; set; }

    public static ApiResponse Ok(string? message = null)
    {
        return new ApiResponse
        {
            Success = true,
            Message = message
        };
    }

    public static ApiResponse Fail(string? message = null)
    {
        return new ApiResponse
        {
            Success = false,
            Message = message
        };
    }
}

/// <summary>
///     Standard API response wrapper for successful responses with data
/// </summary>
/// <typeparam name="T">Type of data being returned</typeparam>
public class ApiResponse<T> : ApiResponse
{
    public T? Data { get; set; }

    public static ApiResponse<T> Ok(T data, string? message = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Data = data,
            Message = message
        };
    }

    public new static ApiResponse<T> Fail(string? message = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = message
        };
    }
}

/// <summary>
///     Error API response with detailed error information
/// </summary>
public class ErrorApiResponse : ApiResponse
{
    public IReadOnlyList<string> Errors { get; set; } = [];

    public static ErrorApiResponse Create(string message, IEnumerable<string>? errors = null)
    {
        return new ErrorApiResponse
        {
            Success = false,
            Message = message,
            Errors = errors?.ToList().AsReadOnly() ?? new ReadOnlyCollection<string>(Array.Empty<string>())
        };
    }
}

/// <summary>
///     Paginated response wrapper
/// </summary>
/// <typeparam name="T">Type of items in the collection</typeparam>
public class PaginatedResponse<T>
{
    public bool Success { get; set; } = true;
    public PaginatedData<T> Data { get; set; } = null!;

    public static PaginatedResponse<T> Ok(List<T> items, PaginationMetadata pagination)
    {
        return new PaginatedResponse<T>
        {
            Success = true,
            Data = new PaginatedData<T>
            {
                Items = items,
                Pagination = pagination
            }
        };
    }
}

public class PagedRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;

    public int Skip => (Page - 1) * PageSize;
}

/// <summary>
///     Container for paginated data
/// </summary>
public class PaginatedData<T>
{
    public List<T> Items { get; set; } = [];
    public PaginationMetadata Pagination { get; set; } = null!;
}

/// <summary>
///     Pagination metadata
/// </summary>
public class PaginationMetadata
{
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
    public bool HasPreviousPage => CurrentPage > 1;
    public bool HasNextPage => CurrentPage < TotalPages;

    public static PaginationMetadata Create(int currentPage, int pageSize, int totalItems)
    {
        return new PaginationMetadata
        {
            CurrentPage = currentPage,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize)
        };
    }
}