using Bruinet.Samples.Blazor.App.Models;

namespace Bruinet.Samples.Blazor.App.Services;

/// <summary>
/// Service for managing data items (simulates backend API).
/// </summary>
public class DataService
{
    private readonly List<DataItem> _items = new();
    private int _nextId = 1;

    public DataService()
    {
        SeedData();
    }

    private void SeedData()
    {
        var categories = new[] { "Electronics", "Books", "Clothing", "Food", "Toys" };
        var statuses = new[] { "Active", "Inactive", "Pending", "Archived" };

        for (int i = 0; i < 50; i++)
        {
            _items.Add(new DataItem
            {
                Id = _nextId++,
                Title = $"Item {i + 1}",
                Description = $"Description for item {i + 1}. This is sample data for testing.",
                Category = categories[i % categories.Length],
                Status = statuses[i % statuses.Length],
                CreatedAt = DateTime.Now.AddDays(-i),
                Price = Math.Round((decimal)(Random.Shared.NextDouble() * 100), 2),
                Quantity = Random.Shared.Next(1, 100),
                IsStarred = i % 5 == 0
            });
        }
    }

    public Task<List<DataItem>> GetItemsAsync()
    {
        return Task.FromResult(_items.ToList());
    }

    public Task<List<DataItem>> GetItemsAsync(string? searchTerm, string? category, string? status)
    {
        var query = _items.AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(i =>
                i.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                i.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(i => i.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(i => i.Status == status);
        }

        return Task.FromResult(query.ToList());
    }

    public Task<DataItem?> GetItemAsync(int id)
    {
        return Task.FromResult(_items.FirstOrDefault(i => i.Id == id));
    }

    public Task<DataItem> CreateItemAsync(DataItem item)
    {
        item.Id = _nextId++;
        item.CreatedAt = DateTime.Now;
        _items.Add(item);
        return Task.FromResult(item);
    }

    public Task<DataItem?> UpdateItemAsync(DataItem item)
    {
        var existing = _items.FirstOrDefault(i => i.Id == item.Id);
        if (existing != null)
        {
            existing.Title = item.Title;
            existing.Description = item.Description;
            existing.Category = item.Category;
            existing.Status = item.Status;
            existing.Price = item.Price;
            existing.Quantity = item.Quantity;
            existing.IsStarred = item.IsStarred;
            existing.UpdatedAt = DateTime.Now;
        }
        return Task.FromResult(existing);
    }

    public Task<bool> DeleteItemAsync(int id)
    {
        var item = _items.FirstOrDefault(i => i.Id == id);
        if (item != null)
        {
            _items.Remove(item);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<List<string>> GetCategoriesAsync()
    {
        return Task.FromResult(_items.Select(i => i.Category).Distinct().ToList());
    }

    public Task<List<string>> GetStatusesAsync()
    {
        return Task.FromResult(_items.Select(i => i.Status).Distinct().ToList());
    }
}

/// <summary>
/// Service for managing media items.
/// </summary>
public class MediaService
{
    private readonly List<MediaItem> _items = new();
    private int _nextId = 1;

    public MediaService()
    {
        SeedData();
    }

    private void SeedData()
    {
        for (int i = 1; i <= 12; i++)
        {
            _items.Add(new MediaItem
            {
                Id = _nextId++,
                Title = $"Image {i}",
                Description = $"Sample image description {i}",
                ThumbnailUrl = $"https://picsum.photos/seed/{i}/150/150",
                FullUrl = $"https://picsum.photos/seed/{i}/800/600",
                Type = MediaType.Image,
                UploadedAt = DateTime.Now.AddDays(-i)
            });
        }

        // Add some video items
        for (int i = 1; i <= 3; i++)
        {
            _items.Add(new MediaItem
            {
                Id = _nextId++,
                Title = $"Video {i}",
                Description = $"Sample video description {i}",
                ThumbnailUrl = $"https://picsum.photos/seed/v{i}/150/150",
                Type = MediaType.Video,
                Duration = TimeSpan.FromMinutes(Random.Shared.Next(1, 60)),
                UploadedAt = DateTime.Now.AddDays(-i)
            });
        }
    }

    public Task<List<MediaItem>> GetMediaItemsAsync(MediaType? type = null)
    {
        var items = type.HasValue
            ? _items.Where(i => i.Type == type.Value).ToList()
            : _items.ToList();
        return Task.FromResult(items);
    }

    public Task<MediaItem?> GetMediaItemAsync(int id)
    {
        return Task.FromResult(_items.FirstOrDefault(i => i.Id == id));
    }
}

/// <summary>
/// Service for managing toast notifications.
/// </summary>
public class ToastService
{
    public event Action<ToastMessage>? OnShow;
    public event Action<Guid>? OnHide;

    public void Show(string message, ToastType type = ToastType.Info, string title = "")
    {
        var toast = new ToastMessage
        {
            Message = message,
            Type = type,
            Title = string.IsNullOrEmpty(title) ? type.ToString() : title
        };
        OnShow?.Invoke(toast);
    }

    public void ShowSuccess(string message, string title = "Success")
        => Show(message, ToastType.Success, title);

    public void ShowError(string message, string title = "Error")
        => Show(message, ToastType.Error, title);

    public void ShowWarning(string message, string title = "Warning")
        => Show(message, ToastType.Warning, title);

    public void ShowInfo(string message, string title = "Info")
        => Show(message, ToastType.Info, title);

    public void Hide(Guid id)
    {
        OnHide?.Invoke(id);
    }
}
