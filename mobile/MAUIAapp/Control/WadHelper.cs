// WadHelper.cs
namespace MAUIAapp
{
    internal static class WadHelper
    {
        private const string WadFileName = "doom1.wad";

        public static async Task<string> ExtractWadAsync()
        {
            var destPath = Path.Combine(
                FileSystem.AppDataDirectory,
                WadFileName);

            if (File.Exists(destPath)) return destPath;

            using var asset = await FileSystem.OpenAppPackageFileAsync(WadFileName);
            using var dest = File.Create(destPath);
            await asset.CopyToAsync(dest);

            return destPath;
        }
    }
}