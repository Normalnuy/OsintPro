using System;
using System.Collections.ObjectModel;
using System.Linq;
using OsintPro.UI.Models;

namespace OsintPro.UI.Services
{
    public class SearchProgressTracker
    {
        public ObservableCollection<ModuleProgressItem> Items { get; } = new();

        public int CachedCount => Items.Count(i => i.State == ModuleProgressState.Cached);
        public int FinishedCount => Items.Count(i =>
            i.State is ModuleProgressState.Completed
                or ModuleProgressState.Cached
                or ModuleProgressState.Skipped
                or ModuleProgressState.Cancelled
                or ModuleProgressState.Error);

        public void Reset(params (SearchModule module, bool enabled)[] modules)
        {
            Items.Clear();

            foreach (var (module, enabled) in modules)
            {
                var item = CreateItem(module);
                if (!enabled)
                    ApplySkipped(item);
                Items.Add(item);
            }
        }

        public ModuleProgressItem Get(SearchModule module) =>
            Items.First(i => i.Module == module);

        public void SetRunning(SearchModule module)
        {
            var item = Get(module);
            item.State = ModuleProgressState.Running;
            item.StatusText = "Пошук...";
            item.Progress = 35;
            item.IsIndeterminate = true;
        }

        public void SetCached(SearchModule module, TimeSpan age)
        {
            var item = Get(module);
            item.State = ModuleProgressState.Cached;
            item.StatusText = FormatAge(age);
            item.Progress = 100;
            item.IsIndeterminate = false;
        }

        public void SetCompleted(SearchModule module)
        {
            var item = Get(module);
            item.State = ModuleProgressState.Completed;
            item.StatusText = "Готово";
            item.Progress = 100;
            item.IsIndeterminate = false;
        }

        public void SetSkipped(SearchModule module, string reason = "Пропущено")
        {
            ApplySkipped(Get(module), reason);
        }

        public void SetCancelled(SearchModule module)
        {
            var item = Get(module);
            item.State = ModuleProgressState.Cancelled;
            item.StatusText = "Скасовано";
            item.Progress = 0;
            item.IsIndeterminate = false;
        }

        public void SetError(SearchModule module, string message = "Помилка")
        {
            var item = Get(module);
            item.State = ModuleProgressState.Error;
            item.StatusText = message;
            item.Progress = 100;
            item.IsIndeterminate = false;
        }

        public void CancelAllActive()
        {
            foreach (var item in Items.Where(i => i.State == ModuleProgressState.Running))
                SetCancelled(item.Module);
        }

        public string BuildSummaryText()
        {
            int total = Items.Count;
            int done = FinishedCount;
            int cached = CachedCount;
            int running = Items.Count(i => i.State == ModuleProgressState.Running);

            if (running > 0)
            {
                string cacheHint = cached > 0 ? $", {cached} з кешу" : "";
                return $"⏳ Оброблено {done}/{total} модулів{cacheHint}...";
            }

            if (Items.Any(i => i.State == ModuleProgressState.Error))
                return cached > 0
                    ? $"⚠️ Завершено з помилками ({cached} з кешу)"
                    : "⚠️ Завершено з помилками";

            if (Items.All(i => i.State == ModuleProgressState.Cancelled))
                return "🛑 Усі пошуки скасовано";

            return cached > 0
                ? $"✅ Пошук завершено ({cached} модулів з кешу)"
                : "✅ Пошук завершено";
        }

        private static ModuleProgressItem CreateItem(SearchModule module) => module switch
        {
            SearchModule.Security => new ModuleProgressItem { Module = module, Icon = "🚨", Title = "Безпека" },
            SearchModule.Courts => new ModuleProgressItem { Module = module, Icon = "⚖️", Title = "Суди" },
            SearchModule.Debts => new ModuleProgressItem { Module = module, Icon = "💰", Title = "Борги" },
            SearchModule.Business => new ModuleProgressItem { Module = module, Icon = "🏢", Title = "Бізнес" },
            SearchModule.Declarations => new ModuleProgressItem { Module = module, Icon = "📄", Title = "НАЗК" },
            SearchModule.Footprint => new ModuleProgressItem { Module = module, Icon = "🌐", Title = "Слід" },
            SearchModule.Social => new ModuleProgressItem { Module = module, Icon = "📱", Title = "Соцмережі" },
            _ => new ModuleProgressItem { Module = module, Title = module.ToString() }
        };

        private static void ApplySkipped(ModuleProgressItem item, string reason = "Пропущено")
        {
            item.State = ModuleProgressState.Skipped;
            item.StatusText = reason;
            item.Progress = 100;
            item.IsIndeterminate = false;
        }

        private static string FormatAge(TimeSpan age)
        {
            if (age.TotalMinutes < 1)
                return "З кешу (<1 хв)";
            if (age.TotalHours < 1)
                return $"З кешу ({(int)age.TotalMinutes} хв)";
            return $"З кешу ({(int)age.TotalHours} год)";
        }
    }
}