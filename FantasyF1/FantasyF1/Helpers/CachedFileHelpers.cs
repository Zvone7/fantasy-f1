namespace FantasyF1.Helpers;

public static class CachedFileHelpers
{
    // except file path, check the file file exists, if it does, read the content, check if it's empty. return true or  false
    public static async Task<Boolean> IsValidCachedFileAsync(String filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            return false;
        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        if(String.IsNullOrWhiteSpace(content))
            return false;
        if (content == "[]")
            return false;
        return true;
    }
}