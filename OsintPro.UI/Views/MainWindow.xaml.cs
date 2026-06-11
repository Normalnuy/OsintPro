using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Text.RegularExpressions;
using System.IO;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using OsintPro.UI.Models;
using OsintPro.UI.Services;
using System.Collections.ObjectModel;
using Sentry;

namespace OsintPro.UI.Views
{
    public partial class MainWindow : Window
    {
        private const string ColorLoading = "#FFD700";
        private const string ColorSuccess = "#32CD32";
        private const string ColorError = "#FF6347";
        private const string ColorInfo = "#007ACC";

        private string currentCaptchaSessionId = "";
        private Dossier _currentDossier;
        private Dossier _selectedArchiveDossier;
        private Dossier _archiveSourceDossier;
        private Dossier _compareAnchorDossier;
        private readonly ArchiveManager _archiveManager = new();
        private readonly SearchProgressTracker _searchProgress = new();
        private readonly SearchOrchestrator _orchestrator = new();

        private SearchContext _lastSearchContext;
        private SearchSession _searchSession;
        private readonly Dictionary<SearchModule, ModuleRunResult> _moduleMeta = new();
        private readonly Dictionary<SearchModule, string> _moduleRawResults = new();

        private CancellationTokenSource _globalCts;
        private CancellationTokenSource _ctsCourts;
        private CancellationTokenSource _ctsBusiness;
        private CancellationTokenSource _ctsDebts;
        private CancellationTokenSource _ctsDeclarations;
        private CancellationTokenSource _ctsSocial;
        private CancellationTokenSource _ctsSecurity;
        private CancellationTokenSource _ctsFootprint;

        public MainWindow()
        {
            InitializeComponent();
            AppSettings.Reload();
            SearchButton.Click += async (s, e) => await PerformSearchAsync();
            SearchResultCache.ClearExpired();
            InitializeAsyncWebView();
            this.ContentRendered += MainWindow_ContentRendered;
        }

        private async void MainWindow_ContentRendered(object sender, EventArgs e)
        {
            this.ContentRendered -= MainWindow_ContentRendered;
            await Task.Delay(300);
            CheckFirstRunAfterUpdate();
            await PlaywrightBootstrap.EnsureChromiumAsync(this);
        }

        private void CheckFirstRunAfterUpdate()
        {
            try
            {
                string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JustinOSINT");
                Directory.CreateDirectory(appDataFolder);
                string versionFile = Path.Combine(appDataFolder, "last_version.txt");
                string currentVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
                string lastVersion = File.Exists(versionFile) ? File.ReadAllText(versionFile).Trim() : "0.0.0";

                string pendingVersionFile = Path.Combine(appDataFolder, "pending_version.txt");
                string pendingChangelogFile = Path.Combine(appDataFolder, "pending_changelog.txt");

                string changelog = null;
                string versionToShow = currentVersion;

                if (File.Exists(pendingVersionFile))
                {
                    versionToShow = File.ReadAllText(pendingVersionFile).Trim();
                    if (File.Exists(pendingChangelogFile))
                        changelog = File.ReadAllText(pendingChangelogFile).Trim();

                    TryDeleteFile(pendingVersionFile);
                    TryDeleteFile(pendingChangelogFile);
                }

                if (string.IsNullOrWhiteSpace(changelog))
                    changelog = AppChangelog.GetForVersion(versionToShow);

                if (versionToShow != lastVersion)
                {
                    var changelogWin = new ChangelogWindow(versionToShow, changelog) { Owner = this };
                    changelogWin.ShowDialog();
                    File.WriteAllText(versionFile, versionToShow);
                }
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
            }
        }

        private async void BtnCancelCourts_Click(object sender, RoutedEventArgs e)
        {
            _ctsCourts?.Cancel();
            _searchProgress.SetCancelled(SearchModule.Courts);
            RefreshProgressSummary();
            ApplyModuleResult(new ModuleRunResult { Module = SearchModule.Courts, RawText = "🛑 Пошук скасовано." });
            var overlay = (Grid)FindName("CaptchaOverlay");
            if (overlay != null) overlay.Visibility = Visibility.Collapsed;
            await CourtScraper.ClearAllSessionsAsync();
            currentCaptchaSessionId = "";
        }

        private void BtnCancelBusiness_Click(object sender, RoutedEventArgs e)
        {
            _ctsBusiness?.Cancel();
            _searchProgress.SetCancelled(SearchModule.Business);
            RefreshProgressSummary();
            ApplyModuleResult(new ModuleRunResult { Module = SearchModule.Business, RawText = "🛑 Пошук скасовано." });
        }

        private void BtnCancelDebts_Click(object sender, RoutedEventArgs e)
        {
            _ctsDebts?.Cancel();
            _searchProgress.SetCancelled(SearchModule.Debts);
            RefreshProgressSummary();
            ApplyModuleResult(new ModuleRunResult { Module = SearchModule.Debts, RawText = "🛑 Пошук скасовано." });
        }

        private void BtnCancelDeclarations_Click(object sender, RoutedEventArgs e)
        {
            _ctsDeclarations?.Cancel();
            _searchProgress.SetCancelled(SearchModule.Declarations);
            RefreshProgressSummary();
            ApplyModuleResult(new ModuleRunResult { Module = SearchModule.Declarations, RawText = "🛑 Пошук скасовано." });
        }

        private void BtnCancelSocial_Click(object sender, RoutedEventArgs e)
        {
            _ctsSocial?.Cancel();
            _searchProgress.SetCancelled(SearchModule.Social);
            RefreshProgressSummary();
            ApplyModuleResult(new ModuleRunResult { Module = SearchModule.Social, RawText = "🛑 Пошук скасовано." });
        }

        private void BtnCancelSecurity_Click(object sender, RoutedEventArgs e)
        {
            _ctsSecurity?.Cancel();
            _searchProgress.SetCancelled(SearchModule.Security);
            RefreshProgressSummary();
            ApplyModuleResult(new ModuleRunResult { Module = SearchModule.Security, RawText = "🛑 Пошук скасовано." });
        }

        private void BtnCancelFootprint_Click(object sender, RoutedEventArgs e)
        {
            _ctsFootprint?.Cancel();
            _searchProgress.SetCancelled(SearchModule.Footprint);
            RefreshProgressSummary();
            ApplyModuleResult(new ModuleRunResult { Module = SearchModule.Footprint, RawText = "🛑 Пошук скасовано." });
        }

        private void BtnCancelAll_Click(object sender, RoutedEventArgs e)
        {
            _globalCts?.Cancel();
            BtnCancelCourts_Click(null, null);
            BtnCancelBusiness_Click(null, null);
            BtnCancelDebts_Click(null, null);
            BtnCancelDeclarations_Click(null, null);
            BtnCancelSocial_Click(null, null);
            BtnCancelSecurity_Click(null, null);
            BtnCancelFootprint_Click(null, null);
            SearchProgressBar.Visibility = Visibility.Collapsed;
            SearchProgressBar.IsIndeterminate = false;
            SearchButton.IsEnabled = true;
            BtnSaveSearchToArchive.Visibility = Visibility.Collapsed;
            BtnExportJson.Visibility = Visibility.Collapsed;
            BtnExportCsv.Visibility = Visibility.Collapsed;
            BtnExportPdfSearch.Visibility = Visibility.Collapsed;
            _searchProgress.CancelAllActive();
            SummaryText.Text = "🛑 Усі пошуки миттєво скасовано.";
            SummaryText.Foreground = (Brush)new BrushConverter().ConvertFrom(ColorError);
        }

        private void BtnClearFields_Click(object sender, RoutedEventArgs e)
        {
            LastNameBox.Text = "";
            FirstNameBox.Text = "";
            PatronymicBox.Text = "";
            InnBox.Text = "";
            NicknameBox.Text = "";
            DobBox.Text = "";
            ContactBox.Text = "";
            LastNameBox.Focus();
        }

        private void BtnNewSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchGrid.Visibility = Visibility.Visible;
            ArchiveGrid.Visibility = Visibility.Collapsed;
            EditorGrid.Visibility = Visibility.Collapsed;
            _archiveSourceDossier = null;
            _currentDossier = null;
        }

        private void BtnOpenArchive_Click(object sender, RoutedEventArgs e)
        {
            SearchGrid.Visibility = Visibility.Collapsed;
            EditorGrid.Visibility = Visibility.Collapsed;
            ArchiveGrid.Visibility = Visibility.Visible;
            RefreshArchiveList();
        }

        private void BtnBackToArchive_Click(object sender, RoutedEventArgs e) => BtnOpenArchive_Click(null, null);

        private void ArchiveSearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshArchiveList();

        private void RefreshArchiveList()
        {
            string query = ArchiveSearchBox?.Text ?? "";
            ArchiveTilesList.ItemsSource = _archiveManager.SearchDossiers(query);
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            var win = new SettingsWindow { Owner = this };
            win.ShowDialog();
        }

        private async void BtnCheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            var result = await UpdateCheckerService.CheckAsync();
            if (!result.UpdateAvailable)
            {
                AppDialogs.Success(this, "Оновлення", $"Система актуальна.\n\nВстановлена версія: v{result.LocalVersion}");
                return;
            }

            if (!AppDialogs.Confirm(this,
                    "Доступне оновлення",
                    $"Нова версія: v{result.LatestVersion}\nПоточна: v{result.LocalVersion}\n\nЗапустити лаунчер для завантаження?"))
                return;

            string launcher = Path.Combine(AppContext.BaseDirectory, "JustinOSINT_Launcher.exe");
            if (File.Exists(launcher))
                Process.Start(new ProcessStartInfo(launcher) { UseShellExecute = true });
            else
                AppDialogs.Warning(this, "Оновлення", "Запустіть JustinOSINT_Launcher.exe з папки програми.");
        }

        private void BtnDeleteArchive_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedArchiveDossier == null)
            {
                MessageBox.Show("Оберіть досьє в архіві (клік по плитці) або видаліть через 🗑 на картці.", "Архів", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            DeleteArchiveDossier(_selectedArchiveDossier);
        }

        private void ArchiveTile_Select(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement el && el.DataContext is Dossier selected)
                _selectedArchiveDossier = selected;
        }

        private void ArchiveTile_Open_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Dossier selected)
                OpenDossierEditor(selected);
        }

        private async void ArchiveTile_Rerun_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Dossier selected)
                await RerunSearchFromArchiveAsync(selected);
        }

        private void BtnCompareArchive_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedArchiveDossier == null)
            {
                MessageBox.Show("Оберіть досьє в архіві (клік по плитці).", "Порівняння",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_compareAnchorDossier == null)
            {
                _compareAnchorDossier = _selectedArchiveDossier;
                MessageBox.Show($"Обрано A: «{_compareAnchorDossier.FullName}». Оберіть друге досьє і натисніть Порівняти знову.",
                    "Порівняння", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_compareAnchorDossier.Id == _selectedArchiveDossier.Id)
            {
                MessageBox.Show("Оберіть інше досьє для порівняння.", "Порівняння",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string text = DossierCompareService.Compare(_compareAnchorDossier, _selectedArchiveDossier);
            _compareAnchorDossier = null;
            new CompareDossiersWindow(text) { Owner = this }.ShowDialog();
        }

        private async Task RerunSearchFromArchiveAsync(Dossier dossier)
        {
            if (dossier?.SearchSnapshot == null)
            {
                MessageBox.Show("У цьому досьє немає збережених параметрів пошуку. Відкрийте і збережіть після нового пошуку.",
                    "Архів", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _archiveSourceDossier = dossier;
            ApplySearchSnapshotToForm(dossier.SearchSnapshot);
            BtnNewSearch_Click(null, null);
            await PerformSearchAsync();
        }

        private void ApplySearchSnapshotToForm(SearchSnapshot snap)
        {
            LastNameBox.Text = snap.LastName;
            FirstNameBox.Text = snap.FirstName;
            PatronymicBox.Text = snap.Patronymic;
            InnBox.Text = snap.Inn;
            NicknameBox.Text = snap.Nickname;
            DobBox.Text = snap.Dob;
            ContactBox.Text = snap.Contact;
            StrictSearchToggle.IsChecked = snap.StrictMatch;
            OnlyExactToggle.IsChecked = snap.OnlyExactResults;
            DisableCacheToggle.IsChecked = !snap.CacheEnabled;
        }

        private void ArchiveDeleteTile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Dossier selected)
                DeleteArchiveDossier(selected);
            e.Handled = true;
        }

        private void DeleteArchiveDossier(Dossier dossier)
        {
            if (MessageBox.Show($"Видалити досьє «{dossier.FullName}»?", "Підтвердження",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            if (_archiveManager.DeleteDossier(dossier.Id))
            {
                if (_selectedArchiveDossier?.Id == dossier.Id)
                    _selectedArchiveDossier = null;
                if (_currentDossier?.Id == dossier.Id)
                    _currentDossier = null;
                RefreshArchiveList();
            }
        }

        private void OpenDossierEditor(Dossier selected)
        {
            _selectedArchiveDossier = selected;
            _currentDossier = selected;
            EditorTitle.Text = $"ДОСЬЄ: {selected.FullName}";
            EditorNotes.Text = selected.CustomNotes;
            EditorSecurity.ItemsSource = _currentDossier.Security;
            EditorCourts.ItemsSource = _currentDossier.CourtCases;
            EditorDebts.ItemsSource = _currentDossier.Debts;
            EditorBusiness.ItemsSource = _currentDossier.Businesses;
            EditorDeclarations.ItemsSource = _currentDossier.Declarations;
            EditorMarket.ItemsSource = _currentDossier.Market;
            EditorSocial.ItemsSource = _currentDossier.Social;
            SearchGrid.Visibility = Visibility.Collapsed;
            ArchiveGrid.Visibility = Visibility.Collapsed;
            EditorGrid.Visibility = Visibility.Visible;
        }

        private void BtnDeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ParsedItem item)
            {
                if (_currentDossier.CourtCases.Contains(item)) _currentDossier.CourtCases.Remove(item);
                else if (_currentDossier.Debts.Contains(item)) _currentDossier.Debts.Remove(item);
                else if (_currentDossier.Businesses.Contains(item)) _currentDossier.Businesses.Remove(item);
                else if (_currentDossier.Declarations.Contains(item)) _currentDossier.Declarations.Remove(item);
                else if (_currentDossier.Social.Contains(item)) _currentDossier.Social.Remove(item);
                else if (_currentDossier.Security.Contains(item)) _currentDossier.Security.Remove(item);
                else if (_currentDossier.Market.Contains(item)) _currentDossier.Market.Remove(item);
            }
        }

        private void BtnAddSecurity_Click(object sender, RoutedEventArgs e) => _currentDossier.Security.Add(new ParsedItem { Title = "Новий запис безпеки", Details = "" });
        private void BtnAddCourt_Click(object sender, RoutedEventArgs e) => _currentDossier.CourtCases.Add(new ParsedItem { Title = "Нова справа", Details = "" });
        private void BtnAddDebt_Click(object sender, RoutedEventArgs e) => _currentDossier.Debts.Add(new ParsedItem { Title = "Новий борг", Details = "" });
        private void BtnAddBusiness_Click(object sender, RoutedEventArgs e) => _currentDossier.Businesses.Add(new ParsedItem { Title = "Новий бізнес", Details = "" });
        private void BtnAddDeclaration_Click(object sender, RoutedEventArgs e) => _currentDossier.Declarations.Add(new ParsedItem { Title = "Нова декларація", Details = "" });
        private void BtnAddMarket_Click(object sender, RoutedEventArgs e) => _currentDossier.Market.Add(new ParsedItem { Title = "Новий маркетплейс", Details = "" });
        private void BtnAddSocial_Click(object sender, RoutedEventArgs e) => _currentDossier.Social.Add(new ParsedItem { Title = "Нова соцмережа", Details = "" });

        private void BtnSaveEditor_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDossier != null)
            {
                _currentDossier.CustomNotes = EditorNotes.Text;
                _archiveManager.SaveDossier(_currentDossier);
                MessageBox.Show("Зміни збережено!", "Архів", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnExportPdf_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDossier == null) return;
            _currentDossier.CustomNotes = EditorNotes.Text;
            var sfd = new SaveFileDialog { Filter = "PDF Document|*.pdf", FileName = $"Report_{_currentDossier.FullName.Replace(" ", "_")}.pdf" };
            if (sfd.ShowDialog() == true)
            {
                try
                {
                    PdfGenerator.ExportToPdf(_currentDossier, sfd.FileName);
                    MessageBox.Show("PDF звіт створено!");
                }
                catch (Exception ex)
                {
                    SentrySdk.CaptureException(ex);
                    MessageBox.Show($"Помилка: {ex.Message}");
                }
            }
        }

        private void BtnSaveSearchToArchive_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDossier == null) return;

            if (_archiveSourceDossier != null)
            {
                _currentDossier.Id = _archiveSourceDossier.Id;
                _currentDossier.DateCreated = _archiveSourceDossier.DateCreated;
                _currentDossier.CustomNotes = _archiveSourceDossier.CustomNotes;
            }

            _archiveManager.SaveDossier(_currentDossier);
            _archiveSourceDossier = null;
            BtnSaveSearchToArchive.Visibility = Visibility.Collapsed;
            MessageBox.Show("Досьє збережено в архів!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnExportJson_Click(object sender, RoutedEventArgs e) => ExportCurrentDossier("JSON Document|*.json", ExportService.ExportDossierJson);
        private void BtnExportCsv_Click(object sender, RoutedEventArgs e) => ExportCurrentDossier("CSV Document|*.csv", ExportService.ExportDossierCsv);

        private void ExportCurrentDossier(string filter, Action<Dossier, string> exporter)
        {
            if (_currentDossier == null) return;
            var sfd = new SaveFileDialog
            {
                Filter = filter,
                FileName = $"Search_{_currentDossier.FullName.Replace(" ", "_")}"
            };
            if (sfd.ShowDialog() == true)
            {
                try
                {
                    exporter(_currentDossier, sfd.FileName);
                    MessageBox.Show("Експорт завершено!");
                }
                catch (Exception ex)
                {
                    SentrySdk.CaptureException(ex);
                    MessageBox.Show($"Помилка експорту: {ex.Message}");
                }
            }
        }

        private void NameBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                if (string.IsNullOrWhiteSpace(tb.Text)) return;
                int caretIndex = tb.CaretIndex;
                char[] chars = tb.Text.ToCharArray();
                bool newWord = true;
                bool changed = false;
                for (int i = 0; i < chars.Length; i++)
                {
                    if (char.IsLetter(chars[i]))
                    {
                        if (newWord)
                        {
                            if (char.IsLower(chars[i])) { chars[i] = char.ToUpper(chars[i]); changed = true; }
                            newWord = false;
                        }
                        else if (char.IsUpper(chars[i])) { chars[i] = char.ToLower(chars[i]); changed = true; }
                    }
                    else if (chars[i] == '-' || char.IsWhiteSpace(chars[i]) || chars[i] == '\'') newWord = true;
                }
                if (changed)
                {
                    tb.TextChanged -= NameBox_TextChanged;
                    tb.Text = new string(chars);
                    tb.CaretIndex = caretIndex;
                    tb.TextChanged += NameBox_TextChanged;
                }
            }
        }

        private void DobBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                string text = new string(tb.Text.Where(char.IsDigit).ToArray());
                if (text.Length > 8) text = text.Substring(0, 8);
                string formatted = "";
                for (int i = 0; i < text.Length; i++)
                {
                    if (i == 2 || i == 4) formatted += ".";
                    formatted += text[i];
                }
                if (tb.Text != formatted)
                {
                    tb.TextChanged -= DobBox_TextChanged;
                    tb.Text = formatted;
                    tb.CaretIndex = formatted.Length;
                    tb.TextChanged += DobBox_TextChanged;
                }
            }
        }

        private void OnlyExactToggle_Changed(object sender, RoutedEventArgs e) => ReapplyResultFilters();

        private async void InitializeAsyncWebView()
        {
            try
            {
                string userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JustinOSINT");
                var environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await CaptchaWebView.EnsureCoreWebView2Async(environment);
                CaptchaWebView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
                CaptchaWebView.CoreWebView2.AddWebResourceRequestedFilter("https://court.gov.ua/justin_captcha*", CoreWebView2WebResourceContext.All);
                CaptchaWebView.CoreWebView2.WebResourceRequested += CoreWebView2_WebResourceRequested;
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
                MessageBox.Show($"Помилка WebView2: {ex.Message}");
            }
        }

        private void CoreWebView2_WebResourceRequested(object sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            if (!e.Request.Uri.Contains("justin_captcha")) return;
            string siteKey = "6LdIjOQSAAAAAA5VkX2tOq9Znrem2-r_WZi6Jetn";
            string html = $"<!DOCTYPE html><html lang='uk'><head><meta charset='UTF-8'><script src='https://www.google.com/recaptcha/api.js' async defer></script><style>body {{ overflow: hidden; display: flex; flex-direction: column; justify-content: center; align-items: center; height: 100vh; margin: 0; background: #181818; color: white; font-family: sans-serif; }} .container {{ background: #252526; padding: 30px; border-radius: 12px; text-align: center; border: 1px solid #007ACC; }} .loader {{ display: none; margin-top: 20px; color: #00C6FF; font-weight: bold; }}</style></head><body><div class='container'><h2>⚠️ Перевірка безпеки</h2><p>Доведіть, що ви не робот:</p><div class='g-recaptcha' data-sitekey='{siteKey}' data-theme='dark' data-callback='onCaptchaSuccess'></div><div id='loading' class='loader'>🔄 Передача даних...</div></div><script>function onCaptchaSuccess(token) {{ document.getElementById('loading').style.display = 'block'; window.location.href = 'https://court.gov.ua/captcha_success?token=' + token; }}</script></body></html>";
            var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(html));
            e.Response = CaptchaWebView.CoreWebView2.Environment.CreateWebResourceResponse(stream, 200, "OK", "Content-Type: text/html; charset=utf-8\nAccess-Control-Allow-Origin: *");
        }

        private async void CoreWebView2_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            if (!e.Uri.Contains("captcha_success?token=")) return;
            e.Cancel = true;
            var overlay = (Grid)FindName("CaptchaOverlay");
            if (overlay != null) overlay.Visibility = Visibility.Collapsed;
            var token = e.Uri.Split(new[] { "token=" }, StringSplitOptions.None)[1];
            SearchProgressBar.Visibility = Visibility.Visible;
            SummaryText.Text = "⏳ Відправляю токен...";
            try
            {
                string rawText = await CourtScraper.ResolveCourtCaptchaAsync(currentCaptchaSessionId, token);
                var result = new ModuleRunResult { Module = SearchModule.Courts, RawText = rawText };
                ApplyModuleResult(result);
                SummaryText.Text = "✅ Судові справи завантажено.";
                UpdateDossierFromCurrentResults();
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
                ApplyModuleResult(new ModuleRunResult { Module = SearchModule.Courts, RawText = $"❌ Помилка: {ex.Message}" });
            }
            finally
            {
                SearchProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private async void CancelCaptcha_Click(object sender, RoutedEventArgs e)
        {
            var overlay = (Grid)FindName("CaptchaOverlay");
            if (overlay != null) overlay.Visibility = Visibility.Collapsed;
            try { CaptchaWebView.CoreWebView2.Navigate("about:blank"); } catch { }
            if (!string.IsNullOrEmpty(currentCaptchaSessionId))
            {
                await CourtScraper.ClearSessionAsync(currentCaptchaSessionId);
                currentCaptchaSessionId = "";
            }
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void BtnMaximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private async Task PerformSearchAsync()
        {
            _lastSearchContext = SearchContext.FromInputs(
                LastNameBox.Text, FirstNameBox.Text, PatronymicBox.Text,
                InnBox.Text, NicknameBox.Text, DobBox.Text, ContactBox.Text,
                StrictSearchToggle.IsChecked == true,
                OnlyExactToggle.IsChecked == true,
                DisableCacheToggle.IsChecked != true);

            if (!_lastSearchContext.HasAnyInput)
            {
                ShowSummaryError("⚠️ Помилка: Введіть хоча б одне поле (ПІБ, ІПН, Нікнейм або Контакт).");
                return;
            }

            if (!string.IsNullOrWhiteSpace(_lastSearchContext.Inn))
            {
                string innError = InnValidator.ValidateMessage(_lastSearchContext.Inn);
                if (!string.IsNullOrEmpty(innError))
                {
                    ShowSummaryError($"⚠️ {innError}");
                    return;
                }
            }

            if (!string.IsNullOrWhiteSpace(_lastSearchContext.Dob) && !DobHelper.IsValid(_lastSearchContext.Dob))
            {
                ShowSummaryError("⚠️ Дата народження має бути у форматі ДД.ММ.РРРР.");
                return;
            }

            if (_archiveSourceDossier == null)
                _currentDossier = null;

            SentrySdk.CaptureMessage("Search Executed", scope =>
            {
                scope.Level = SentryLevel.Info;
                scope.SetTag("search.has_fio", _lastSearchContext.HasFio.ToString());
                scope.SetTag("search.has_inn", _lastSearchContext.HasInn.ToString());
                scope.SetTag("search.has_nickname", _lastSearchContext.HasNickname.ToString());
                scope.SetTag("search.has_contact", _lastSearchContext.HasContact.ToString());
                scope.SetTag("search.strict", _lastSearchContext.StrictMatch.ToString());
            });

            SearchButton.IsEnabled = false;
            SearchProgressBar.Visibility = Visibility.Visible;
            SearchProgressBar.IsIndeterminate = true;
            BtnSaveSearchToArchive.Visibility = Visibility.Collapsed;
            BtnExportJson.Visibility = Visibility.Collapsed;
            BtnExportCsv.Visibility = Visibility.Collapsed;
            BtnExportPdfSearch.Visibility = Visibility.Collapsed;
            SummaryText.Text = "⏳ Запущено мульти-пошук...";
            SummaryText.Foreground = (Brush)new BrushConverter().ConvertFrom(ColorLoading);

            ShowLoadingSkeletons();
            _moduleMeta.Clear();
            _moduleRawResults.Clear();
            ResetCancellationTokens();

            _searchProgress.Reset(
                (SearchModule.Security, _lastSearchContext.HasFio),
                (SearchModule.Courts, _lastSearchContext.HasFio),
                (SearchModule.Debts, _lastSearchContext.HasDebtsDecl),
                (SearchModule.Business, _lastSearchContext.HasBusiness),
                (SearchModule.Declarations, _lastSearchContext.HasDebtsDecl),
                (SearchModule.Footprint, _lastSearchContext.HasFootprint),
                (SearchModule.Social, _lastSearchContext.HasSocial));

            ModuleProgressList.ItemsSource = _searchProgress.Items;
            ModuleProgressPanel.Visibility = Visibility.Visible;
            RefreshProgressSummary();

            if (_searchSession != null)
                await _searchSession.DisposeAsync();
            _searchSession = await SearchSession.CreateAsync(_globalCts.Token);

            try
            {
                var tasks = _orchestrator.Modules.Select(module => RunModuleAsync(module)).ToList();
                RefreshProgressSummary();
                await Task.WhenAll(tasks);

                if (_globalCts.IsCancellationRequested) return;

                if (!SummaryText.Text.Contains("❌ Критична"))
                {
                    SummaryText.Text = _searchProgress.BuildSummaryText();
                    SummaryText.Foreground = (Brush)new BrushConverter().ConvertFrom(
                        _searchProgress.Items.Any(i => i.State == ModuleProgressState.Error) ? ColorError : ColorSuccess);
                    UpdateDossierFromCurrentResults();
                    BtnSaveSearchToArchive.Visibility = Visibility.Visible;
                    BtnExportJson.Visibility = Visibility.Visible;
                    BtnExportCsv.Visibility = Visibility.Visible;
                    BtnExportPdfSearch.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
                ShowSummaryError($"❌ Критична помилка: {ex.Message}");
            }
            finally
            {
                SearchButton.IsEnabled = true;
                SearchProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private async void ModuleRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (_lastSearchContext == null || sender is not Button btn || btn.Tag is not SearchModule module)
                return;

            if (_searchSession == null)
                _searchSession = await SearchSession.CreateAsync();

            try
            {
                var searchModule = _orchestrator.GetModule(module);
                var token = GetModuleToken(module);
                var result = await _orchestrator.RunModuleAsync(
                    searchModule, _lastSearchContext, _searchSession, _searchProgress, token, invalidateCache: true);
                ApplyModuleResult(result);
                RefreshProgressSummary();
                UpdateDossierFromCurrentResults();
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
                MessageBox.Show($"Помилка оновлення модуля: {ex.Message}");
            }
        }

        private async Task RunModuleAsync(ISearchModule module)
        {
            if (!module.IsEnabled(_lastSearchContext))
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    _searchProgress.SetSkipped(module.Module, "Немає даних");
                    ApplyModuleResult(new ModuleRunResult
                    {
                        Module = module.Module,
                        RawText = GetSkippedMessage(module.Module)
                    });
                    RefreshProgressSummary();
                });
                return;
            }

            try
            {
                var result = await _orchestrator.RunModuleAsync(
                    module, _lastSearchContext, _searchSession, _searchProgress, GetModuleToken(module.Module));

                await Dispatcher.InvokeAsync(() =>
                {
                    ApplyModuleResult(result);
                    RefreshProgressSummary();
                });
            }
            catch (TaskCanceledException) { }
        }

        private CancellationToken GetModuleToken(SearchModule module) => module switch
        {
            SearchModule.Security => _ctsSecurity.Token,
            SearchModule.Courts => _ctsCourts.Token,
            SearchModule.Business => _ctsBusiness.Token,
            SearchModule.Debts => _ctsDebts.Token,
            SearchModule.Declarations => _ctsDeclarations.Token,
            SearchModule.Footprint => _ctsFootprint.Token,
            SearchModule.Social => _ctsSocial.Token,
            _ => _globalCts.Token
        };

        private string GetSkippedMessage(SearchModule module) => module switch
        {
            SearchModule.Security => "❕ Для цього запиту бази безпеки не перевірялися.",
            SearchModule.Courts => "❕ Для цього запиту суди не перевірялися.",
            SearchModule.Footprint => "❕ Немає даних для пошуку по цифровому сліду.",
            SearchModule.Social => "❕ Немає даних для соцмереж.",
            _ => "❕ Немає даних для пошуку."
        };

        private void ApplyModuleResult(ModuleRunResult result)
        {
            _moduleMeta[result.Module] = result;
            _moduleRawResults[result.Module] = result.RawText;
            bool onlyExact = OnlyExactToggle.IsChecked == true;

            switch (result.Module)
            {
                case SearchModule.Security:
                    SecurityList.ItemsSource = ResultParser.ParseSecurity(result.RawText, onlyExact);
                    SecuritySectionMeta.Text = ResultParser.BuildSectionMeta(result);
                    break;
                case SearchModule.Courts:
                    CasesList.ItemsSource = ResultParser.ParseCases(result.RawText, onlyExact);
                    CourtsSectionMeta.Text = ResultParser.BuildSectionMeta(result);
                    break;
                case SearchModule.Debts:
                    DebtsList.ItemsSource = ResultParser.ParseDebts(result.RawText, onlyExact);
                    DebtsSectionMeta.Text = ResultParser.BuildSectionMeta(result);
                    break;
                case SearchModule.Business:
                    BusinessList.ItemsSource = ResultParser.ParseBusiness(result.RawText, onlyExact);
                    BusinessSectionMeta.Text = ResultParser.BuildSectionMeta(result);
                    break;
                case SearchModule.Declarations:
                    DeclarationsList.ItemsSource = ResultParser.ParseDeclarations(result.RawText, onlyExact);
                    DeclarationsSectionMeta.Text = ResultParser.BuildSectionMeta(result);
                    break;
                case SearchModule.Footprint:
                    FootprintList.ItemsSource = ResultParser.ParseFootprint(result.RawText, onlyExact);
                    FootprintSectionMeta.Text = ResultParser.BuildSectionMeta(result);
                    break;
                case SearchModule.Social:
                    SocialList.ItemsSource = ResultParser.ParseSocial(result.RawText, onlyExact);
                    SocialSectionMeta.Text = ResultParser.BuildSectionMeta(result);
                    break;
            }
        }

        private void ReapplyResultFilters()
        {
            foreach (var pair in _moduleRawResults.ToList())
            {
                if (_moduleMeta.TryGetValue(pair.Key, out var meta))
                    ApplyModuleResult(new ModuleRunResult
                    {
                        Module = pair.Key,
                        RawText = pair.Value,
                        FromCache = meta.FromCache,
                        CacheAge = meta.CacheAge,
                        CompletedAtUtc = meta.CompletedAtUtc
                    });
            }
        }

        private void UpdateDossierFromCurrentResults()
        {
            Dossier existing = _archiveSourceDossier ?? _currentDossier;
            _currentDossier = DossierBuilder.FromSearchResults(
                _lastSearchContext,
                CasesList.ItemsSource as IEnumerable<CourtCaseDisplay>,
                SecurityList.ItemsSource as IEnumerable<GenericRecordDisplay>,
                DebtsList.ItemsSource as IEnumerable<GenericRecordDisplay>,
                BusinessList.ItemsSource as IEnumerable<GenericRecordDisplay>,
                DeclarationsList.ItemsSource as IEnumerable<GenericRecordDisplay>,
                FootprintList.ItemsSource as IEnumerable<GenericRecordDisplay>,
                SocialList.ItemsSource as IEnumerable<GenericRecordDisplay>,
                existing);
        }

        private void ShowLoadingSkeletons()
        {
            var loadingList = new List<GenericRecordDisplay>
            {
                new() { Title = "⏳ Очікування..." },
                new() { Title = "⏳ Очікування..." },
                new() { Title = "⏳ Очікування..." }
            };
            var loadingCases = new List<CourtCaseDisplay>
            {
                new() { CaseNumber = "⏳ Очікування..." },
                new() { CaseNumber = "⏳ Очікування..." },
                new() { CaseNumber = "⏳ Очікування..." }
            };

            SecurityList.ItemsSource = loadingList;
            CasesList.ItemsSource = loadingCases;
            DebtsList.ItemsSource = loadingList;
            BusinessList.ItemsSource = loadingList;
            DeclarationsList.ItemsSource = loadingList;
            FootprintList.ItemsSource = loadingList;
            SocialList.ItemsSource = loadingList;
        }

        private void ResetCancellationTokens()
        {
            _globalCts?.Dispose();
            _globalCts = new CancellationTokenSource();
            var globalToken = _globalCts.Token;

            _ctsCourts?.Dispose();
            _ctsCourts = CancellationTokenSource.CreateLinkedTokenSource(globalToken);
            _ctsBusiness?.Dispose();
            _ctsBusiness = CancellationTokenSource.CreateLinkedTokenSource(globalToken);
            _ctsDebts?.Dispose();
            _ctsDebts = CancellationTokenSource.CreateLinkedTokenSource(globalToken);
            _ctsDeclarations?.Dispose();
            _ctsDeclarations = CancellationTokenSource.CreateLinkedTokenSource(globalToken);
            _ctsSocial?.Dispose();
            _ctsSocial = CancellationTokenSource.CreateLinkedTokenSource(globalToken);
            _ctsSecurity?.Dispose();
            _ctsSecurity = CancellationTokenSource.CreateLinkedTokenSource(globalToken);
            _ctsFootprint?.Dispose();
            _ctsFootprint = CancellationTokenSource.CreateLinkedTokenSource(globalToken);
        }

        private static void TryDeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        private void ShowSummaryError(string message)
        {
            SummaryText.Text = message;
            SummaryText.Foreground = (Brush)new BrushConverter().ConvertFrom(ColorError);
        }

        private void RefreshProgressSummary()
        {
            if (ModuleProgressPanel.Visibility != Visibility.Visible) return;
            SummaryText.Text = _searchProgress.BuildSummaryText();
            var converter = new BrushConverter();
            if (_searchProgress.Items.Any(i => i.State == ModuleProgressState.Error))
                SummaryText.Foreground = (Brush)converter.ConvertFrom(ColorError);
            else if (_searchProgress.FinishedCount == _searchProgress.Items.Count)
                SummaryText.Foreground = (Brush)converter.ConvertFrom(ColorSuccess);
            else
                SummaryText.Foreground = (Brush)converter.ConvertFrom(ColorLoading);
        }

        private async void CaseCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is CourtCaseDisplay selected)
            {
                if (selected.FullText.StartsWith("CAPTCHA_SESSION|"))
                {
                    var parts = selected.FullText.Split('|');
                    currentCaptchaSessionId = parts[1];
                    try
                    {
                        var overlay = (Grid)FindName("CaptchaOverlay");
                        if (overlay != null) overlay.Visibility = Visibility.Visible;
                        await CaptchaWebView.EnsureCoreWebView2Async();
                        CaptchaWebView.CoreWebView2.Navigate($"https://court.gov.ua/justin_captcha?session_id={currentCaptchaSessionId}");
                    }
                    catch (Exception ex)
                    {
                        SentrySdk.CaptureException(ex);
                        MessageBox.Show($"Помилка WebView2:\n{ex.Message}");
                    }
                    return;
                }
                ShowPopup(selected.CaseNumber, selected.FullText);
            }
        }

        private void GenericCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is GenericRecordDisplay selected)
                ShowPopup(selected.Title, selected.FullDetails);
        }

        private void ShowPopup(string title, string content)
        {
            PopupTitle.Text = title;
            PopupContent.Document = BuildPopupDocument(content);
            DetailPopup.IsOpen = true;
        }

        private static FlowDocument BuildPopupDocument(string content)
        {
            var doc = new FlowDocument { PagePadding = new Thickness(10), FontSize = 14 };
            var accent = (Brush)new BrushConverter().ConvertFrom("#00C6FF");
            var muted = (Brush)new BrushConverter().ConvertFrom("#8A8A8A");
            var info = (Brush)new BrushConverter().ConvertFrom("#007ACC");

            foreach (var line in (content ?? "⏳ Завантаження...").Replace("\r", "").Split('\n'))
            {
                string cleanLine = line.Trim();
                if (string.IsNullOrWhiteSpace(cleanLine))
                {
                    doc.Blocks.Add(new Paragraph { Margin = new Thickness(0, 4, 0, 4) });
                    continue;
                }

                var p = new Paragraph { Margin = new Thickness(0, 0, 0, 8), LineHeight = 22 };

                if (cleanLine.StartsWith("🌐 Платформа:"))
                {
                    p.Inlines.Add(new Run(cleanLine) { Foreground = accent, FontWeight = FontWeights.SemiBold });
                }
                else if (cleanLine.Contains(':'))
                {
                    var parts = cleanLine.Split(':', 2);
                    string label = parts[0].Trim();
                    string value = parts[1].Trim();
                    p.Inlines.Add(new Bold(new Run(label + ": ")) { Foreground = info });
                    p.Inlines.Add(new Run(value) { Foreground = Brushes.White });
                }
                else if (cleanLine.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    p.Inlines.Add(new Run(cleanLine) { Foreground = accent, TextDecorations = TextDecorations.Underline });
                }
                else
                {
                    p.Inlines.Add(new Run(cleanLine) { Foreground = muted });
                }

                doc.Blocks.Add(p);
            }

            return doc;
        }

        private void ClosePopup_Click(object sender, RoutedEventArgs e) => DetailPopup.IsOpen = false;
    }
}