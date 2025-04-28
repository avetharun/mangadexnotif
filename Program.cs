using System;
using System.Collections;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using MangaDexSharp;

namespace MangaDexNotif
{
    public class Program {
        public static IMangaDex API {get;} = MangaDex.Create();
        public static List<MangaDexSharp.Chapter> updatedChapters {get;private set;} = [];
        public static DateTime lastUpdatedChapterTime = DateTime.MinValue;
        public static bool hasSeenChapter(Chapter chapter) {
            if (chapter == null || chapter.Attributes == null) return false;
            if (chapter.Attributes.UpdatedAt > DateTime.Now.AddHours(1)) {return true;}
            var existingChapter = updatedChapters.Find(chapter1 => chapter1.Id.Equals(chapter.Id));
            return existingChapter != null;
        }
        static async Task Main(string[] args) {
            int delay = 15000; //15s
            while (true) {
                var filter = new MangaFilter();
                filter.Order = new Dictionary<MangaFilter.OrderKey, OrderValue>
                {
                    { MangaFilter.OrderKey.latestUploadedChapter, OrderValue.desc }
                };
                filter.Limit = 8;
                var results = await API.Manga.List(filter);
                List<Chapter> chapters = [];
                foreach (var mangaType in results.Data) {
                    if (mangaType == null || mangaType.Attributes == null) continue;
                    var mangaId = mangaType.Id;
                    var latestChapter = mangaType.Attributes.LatestUploadedChapter;
                    //Fetch a chapter by chapter ID:
                    var chapter = await API.Chapter.Get(latestChapter);
                    chapters.Add(chapter.Data);
                    if (chapter == null || chapter.Data.Attributes == null) continue;
                    var secondsElapsed = ( DateTime.Now - chapter.Data.Attributes.UpdatedAt).TotalSeconds;
                    Console.WriteLine(secondsElapsed);
                    if (!hasSeenChapter(chapter.Data)) {
                        var defaultTitle = mangaType.Attributes.Title[mangaType.Attributes.Title.Keys.First()];
                        var title = mangaType.Attributes.Title.GetValueOrDefault("en",defaultTitle);
                        updatedChapters.Add(chapter.Data);
                        Console.WriteLine($"https://mangadex.org/chapter/{chapter.Data.Id} - {title} - {secondsElapsed}s");
                        if (secondsElapsed < 60) { // threshold for "added" chapters. Usually this should be 2-4x the delay. This ensures upon startup, it'll only notify of chapters added Ns ago. At runtime, this shouldn't have any impact.
                            string resp = $"Manga Chapter added: {title} {chapter.Data.Attributes.TranslatedLanguage}";
                            resp += $"\nUploaded:\t{secondsElapsed}s ago";
                            resp += $"\nManga:\t\thttps://mangadex.org/title/{mangaId}";
                            resp += $"\nRead:\t\thttps://mangadex.org/chapter/{chapter.Data.Id}";
                            Console.WriteLine(resp);
                        }
                    }
                }

                // Check if updatedChapters contains an Id that chapters doesn't
                var missingChapters = updatedChapters.Where(uc => !chapters.Any(c => c.Id == uc.Id)).ToList();
                if (missingChapters.Any()) {
                    foreach (var missingChapter in missingChapters) {
                        var mangaRelationship = missingChapter.Relationships.Where(relationship => relationship.Type == "manga").FirstOrDefault();
                        var mangaType = (await API.Manga.Get(mangaRelationship.Id)).Data;
                        var defaultTitle = mangaType.Attributes.Title[mangaType.Attributes.Title.Keys.First()];
                        var title = mangaType.Attributes.Title.GetValueOrDefault("en",defaultTitle);
                        Console.WriteLine($"Removing cached Chapter: " + missingChapter.Id + $"({title})");
                        updatedChapters.Remove(missingChapter);
                    }
                }
                System.Threading.Thread.Sleep(delay);
            }
        }
    }
}