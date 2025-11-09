namespace MHBank.Mobile.Services;

public class StorageService : IStorageService
{
    public async Task<string?> GetAsync(string key)
    {
        try
        {
            return await SecureStorage.GetAsync(key);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task SetAsync(string key, string value)
    {
        try
        {
            await SecureStorage.SetAsync(key, value);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving to secure storage: {ex.Message}");
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            SecureStorage.Remove(key);
            await Task.CompletedTask;
        }
        catch (Exception)
        {
            // Ignore
        }
    }

    public async Task<bool> ContainsKeyAsync(string key)
    {
        var value = await GetAsync(key);
        return !string.IsNullOrEmpty(value);
    }
}