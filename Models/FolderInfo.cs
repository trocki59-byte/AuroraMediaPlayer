namespace AuroraMediaPlayer.Models;

public class FolderInfo
{
	public string Name { get; init; } = "";
	public string Path { get; init; } = "";
	public int VideoCount { get; init; }
	public int AudioCount { get; init; }
	public string? CoverThumb { get; init; }

	public string CountText
	{
		get
		{
			var parts = new List<string>();
			if (VideoCount > 0) parts.Add($"{VideoCount} video");
			if (AudioCount > 0) parts.Add($"{AudioCount} müzik");
			return string.Join("  •  ", parts);
		}
	}

	public string DisplayThumb => !string.IsNullOrEmpty(CoverThumb) && File.Exists(CoverThumb) ? CoverThumb! : "ic_folder_small.png";
}
