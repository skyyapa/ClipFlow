using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Windows.Media.Imaging;

namespace ClipFlow
{
    [DataContract]
    internal sealed class HistoryDocument
    {
        [DataMember] public List<ClipboardItem> Items { get; set; }

        public HistoryDocument()
        {
            Items = new List<ClipboardItem>();
        }
    }

    internal sealed class HistoryStore
    {
        private readonly string _directory;
        private readonly string _path;
        private readonly string _imageDirectory;
        private readonly DataContractSerializer _serializer;
        private readonly object _gate = new object();

        internal List<ClipboardItem> Items { get; private set; }

        internal HistoryStore()
        {
            string overrideDirectory = Environment.GetEnvironmentVariable("CLIPFLOW_DATA_DIR");
            _directory = string.IsNullOrWhiteSpace(overrideDirectory)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClipFlow")
                : overrideDirectory;
            _path = Path.Combine(_directory, "history.xml");
            _imageDirectory = Path.Combine(_directory, "images");
            _serializer = new DataContractSerializer(typeof(HistoryDocument));
            Items = Load();
        }

        internal ClipboardItem AddOrRefresh(string text, string rtf, string html, string sourceApp, string sourceTitle)
        {
            lock (_gate)
            {
                ClipboardItem existing = Items.FirstOrDefault(item => string.Equals(item.Text, text, StringComparison.Ordinal));
                if (existing != null)
                {
                    existing.CreatedAt = DateTime.Now;
                    existing.CopyCount++;
                    existing.SourceApp = sourceApp;
                    existing.SourceTitle = sourceTitle;
                    if (!string.IsNullOrEmpty(rtf)) existing.Rtf = rtf;
                    if (!string.IsNullOrEmpty(html)) existing.Html = html;
                    Save();
                    return existing;
                }

                ClipboardItem created = new ClipboardItem
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Text = text,
                    Rtf = rtf,
                    Html = html,
                    ContentType = "Text",
                    SourceApp = sourceApp,
                    SourceTitle = sourceTitle,
                    CreatedAt = DateTime.Now,
                    LastUsedAt = DateTime.MinValue,
                    CopyCount = 1,
                    UseCount = 0,
                    IsFavorite = false
                };
                Items.Add(created);
                Trim();
                Save();
                return created;
            }
        }

        internal ClipboardItem AddOrRefreshImage(BitmapSource bitmap, string sourceApp, string sourceTitle)
        {
            if (bitmap == null) return null;
            lock (_gate)
            {
                byte[] pngBytes;
                FormatConvertedBitmap opaqueBitmap = new FormatConvertedBitmap();
                opaqueBitmap.BeginInit();
                opaqueBitmap.Source = bitmap;
                opaqueBitmap.DestinationFormat = System.Windows.Media.PixelFormats.Bgr32;
                opaqueBitmap.EndInit();
                opaqueBitmap.Freeze();
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(opaqueBitmap));
                using (MemoryStream stream = new MemoryStream())
                {
                    encoder.Save(stream);
                    pngBytes = stream.ToArray();
                }

                string hash;
                using (SHA256 sha = SHA256.Create())
                {
                    hash = BitConverter.ToString(sha.ComputeHash(pngBytes)).Replace("-", string.Empty).ToLowerInvariant();
                }

                ClipboardItem existing = Items.FirstOrDefault(item =>
                    item.IsImage && string.Equals(item.ImageHash, hash, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    existing.CreatedAt = DateTime.Now;
                    existing.CopyCount++;
                    existing.SourceApp = sourceApp;
                    existing.SourceTitle = sourceTitle;
                    Save();
                    return existing;
                }

                Directory.CreateDirectory(_imageDirectory);
                string imagePath = Path.Combine(_imageDirectory, hash + ".png");
                if (!File.Exists(imagePath)) File.WriteAllBytes(imagePath, pngBytes);

                ClipboardItem created = new ClipboardItem
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ContentType = "Image",
                    ImagePath = imagePath,
                    ImageHash = hash,
                    ImageWidth = bitmap.PixelWidth,
                    ImageHeight = bitmap.PixelHeight,
                    SourceApp = sourceApp,
                    SourceTitle = sourceTitle,
                    CreatedAt = DateTime.Now,
                    LastUsedAt = DateTime.MinValue,
                    CopyCount = 1,
                    UseCount = 0,
                    IsFavorite = false
                };
                Items.Add(created);
                Trim();
                Save();
                return created;
            }
        }

        internal List<ClipboardItem> Search(string query, int limit)
        {
            lock (_gate)
            {
                IEnumerable<ClipboardItem> result = Items;
                if (!string.IsNullOrWhiteSpace(query))
                {
                    string[] words = query.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    result = result.Where(item => words.All(word =>
                        (item.Text ?? string.Empty).IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        (item.SourceApp ?? string.Empty).IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        (item.IsImage && ("图片 截图 image screenshot").IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)));
                }

                return result
                    .OrderByDescending(item => item.IsFavorite)
                    .ThenByDescending(item => item.CreatedAt)
                    .Take(limit)
                    .ToList();
            }
        }

        internal void MarkUsed(ClipboardItem item)
        {
            if (item == null) return;
            lock (_gate)
            {
                item.LastUsedAt = DateTime.Now;
                item.UseCount++;
                Save();
            }
        }

        internal void ToggleFavorite(ClipboardItem item)
        {
            if (item == null) return;
            lock (_gate)
            {
                item.IsFavorite = !item.IsFavorite;
                Save();
            }
        }

        internal void Remove(ClipboardItem item)
        {
            if (item == null) return;
            lock (_gate)
            {
                Items.RemoveAll(value => value.Id == item.Id);
                DeleteImageFileIfUnused(item);
                Save();
            }
        }

        internal void ClearUnfavorited()
        {
            lock (_gate)
            {
                List<ClipboardItem> removed = Items.Where(item => !item.IsFavorite).ToList();
                Items.RemoveAll(item => !item.IsFavorite);
                foreach (ClipboardItem item in removed) DeleteImageFileIfUnused(item);
                Save();
            }
        }

        private List<ClipboardItem> Load()
        {
            try
            {
                if (!File.Exists(_path)) return new List<ClipboardItem>();
                using (FileStream stream = File.OpenRead(_path))
                {
                    HistoryDocument document = (HistoryDocument)_serializer.ReadObject(stream);
                    return document.Items ?? new List<ClipboardItem>();
                }
            }
            catch
            {
                return new List<ClipboardItem>();
            }
        }

        private void Trim()
        {
            if (Items.Count <= 5000) return;
            List<ClipboardItem> removable = Items
                .Where(item => !item.IsFavorite)
                .OrderBy(item => item.CreatedAt)
                .Take(Items.Count - 5000)
                .ToList();
            foreach (ClipboardItem item in removable)
            {
                Items.Remove(item);
                DeleteImageFileIfUnused(item);
            }
        }

        private void DeleteImageFileIfUnused(ClipboardItem item)
        {
            if (item == null || !item.IsImage || string.IsNullOrEmpty(item.ImagePath)) return;
            bool stillUsed = Items.Any(value => string.Equals(value.ImagePath, item.ImagePath, StringComparison.OrdinalIgnoreCase));
            if (!stillUsed && File.Exists(item.ImagePath))
            {
                try { File.Delete(item.ImagePath); }
                catch { }
            }
        }

        private void Save()
        {
            Directory.CreateDirectory(_directory);
            string temporary = _path + ".tmp";
            using (FileStream stream = File.Create(temporary))
            {
                _serializer.WriteObject(stream, new HistoryDocument { Items = Items });
            }
            if (File.Exists(_path)) File.Delete(_path);
            File.Move(temporary, _path);
        }
    }
}
