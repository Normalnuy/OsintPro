using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Sentry;

namespace OsintPro.UI.Services
{
    public class DeclarationScraper
    {
        private static readonly SemaphoreSlim _detailGate = new(4, 4);

        // Перетворення числового типу декларації згідно з оновленою документацією API
        private static string GetDeclarationTypeName(int type)
        {
            return type switch
            {
                1 => "Щорічна",
                2 => "Перед звільненням",
                3 => "Після звільнення",
                4 => "Кандидата на посаду",
                _ => "Декларація"
            };
        }

        public static async Task<string> ParseDeclarationsDataAsync(string query, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(query)) return "✅ Немає даних для пошуку.";

            try
            {
                string cleanQuery = string.Join(" ", query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
                string encodedQuery = Uri.EscapeDataString(cleanQuery);
                string listUrl = $"https://public-api.nazk.gov.ua/v2/documents/list?query={encodedQuery}";

                var response = await AppHttp.Shared.GetAsync(listUrl, token);
                if (!response.IsSuccessStatusCode)
                    return $"❌ Помилка API НАЗК: {(int)response.StatusCode}";

                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("error", out var errorEl))
                    return $"❌ Помилка API НАЗК: {errorEl.ToString()}";

                // Якщо пошук по повному імені нічого не дав, вмикаємо розумний Fallback
                if (!doc.RootElement.TryGetProperty("data", out var dataArray) || dataArray.ValueKind != JsonValueKind.Array || dataArray.GetArrayLength() == 0)
                {
                    var words = cleanQuery.Split(' ');
                    if (words.Length > 1)
                    {
                        // Робимо другий запит виключно за прізвищем (першим словом)
                        string fallbackQuery = Uri.EscapeDataString(words[0]);
                        string fallbackUrl = $"https://public-api.nazk.gov.ua/v2/documents/list?query={fallbackQuery}";
                        var fbResponse = await AppHttp.Shared.GetAsync(fallbackUrl, token);

                        if (fbResponse.IsSuccessStatusCode)
                        {
                            string fbJson = await fbResponse.Content.ReadAsStringAsync();
                            using var fbDoc = JsonDocument.Parse(fbJson);

                            if (fbDoc.RootElement.TryGetProperty("data", out var fbDataArray) && fbDataArray.ValueKind == JsonValueKind.Array && fbDataArray.GetArrayLength() > 0)
                            {
                                // Передаємо cleanQuery для жорсткої локальної фільтрації
                                return await ProcessDeclarationsArrayAsync(fbDataArray, cleanQuery, token);
                            }
                        }
                    }
                    return "✅ Декларацій не знайдено.";
                }

                return await ProcessDeclarationsArrayAsync(dataArray, cleanQuery, token);
            }
            catch (TaskCanceledException)
            {
                return "🛑 Пошук скасовано.";
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
                return $"❌ Помилка запиту НАЗК: {ex.Message}";
            }
        }

        private static async Task<string> ProcessDeclarationsArrayAsync(JsonElement dataArray, string originalQuery, CancellationToken token)
        {
            var tasks = new List<Task<string>>();
            int count = 0;

            foreach (var item in dataArray.EnumerateArray())
            {
                if (count >= 10 || token.IsCancellationRequested) break;

                string docId = item.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                if (string.IsNullOrEmpty(docId))
                    continue;

                string listName = TryGetListName(item);
                if (!string.IsNullOrWhiteSpace(listName) &&
                    !SearchQueryMatcher.MatchesPerson(listName, originalQuery))
                    continue;

                int declTypeInt = item.TryGetProperty("declaration_type", out var dtEl) && dtEl.ValueKind == JsonValueKind.Number ? dtEl.GetInt32() : 0;
                string year = item.TryGetProperty("declaration_year", out var yrEl) ? yrEl.ToString() : "Не вказано";
                string declTypeName = GetDeclarationTypeName(declTypeInt);

                tasks.Add(FetchFullDeclarationAsync(docId, declTypeName, year, originalQuery, token));
                count++;
            }

            var results = await Task.WhenAll(tasks);
            var validResults = results.Where(r => !string.IsNullOrEmpty(r)).ToList();

            return validResults.Count > 0 ? string.Join("\n\n", validResults) : "✅ Декларацій не знайдено.";
        }

        private static string TryGetListName(JsonElement item)
        {
            if (item.TryGetProperty("full_name", out var fullNameEl))
                return fullNameEl.ToString();

            string last = item.TryGetProperty("lastname", out var ln) ? ln.ToString() : "";
            string first = item.TryGetProperty("firstname", out var fn) ? fn.ToString() : "";
            string middle = item.TryGetProperty("middlename", out var mn) ? mn.ToString() : "";
            return $"{last} {first} {middle}".Trim();
        }

        private static async Task<string> FetchFullDeclarationAsync(string docId, string declType, string year, string originalQuery, CancellationToken token)
        {
            await _detailGate.WaitAsync(token);
            try
            {
                string detailUrl = $"https://public-api.nazk.gov.ua/v2/documents/{docId}";
                var response = await AppHttp.Shared.GetAsync(detailUrl, token);
                if (!response.IsSuccessStatusCode) return null;

                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("data", out var fullData)) return null;

                var step1 = fullData.TryGetProperty("step_1", out var s1) && s1.TryGetProperty("data", out var s1d) ? s1d : default;

                string lastName = GetStringProp(step1, "lastname", "");
                string firstName = GetStringProp(step1, "firstname", "");
                string middleName = GetStringProp(step1, "middlename", "");

                // Жорстка локальна фільтрація (відкидаємо однофамільців з іншими іменами)
                string fullName = $"{lastName} {firstName} {middleName}".Trim();
                if (!SearchQueryMatcher.MatchesPerson(fullName, originalQuery))
                    return null;

                lastName = string.IsNullOrEmpty(lastName) ? "" : char.ToUpper(lastName[0]) + lastName.Substring(1).ToLower();
                firstName = string.IsNullOrEmpty(firstName) ? "" : char.ToUpper(firstName[0]) + firstName.Substring(1).ToLower();
                middleName = string.IsNullOrEmpty(middleName) ? "" : char.ToUpper(middleName[0]) + middleName.Substring(1).ToLower();

                string name = $"{lastName} {firstName} {middleName}".Trim();
                if (string.IsNullOrEmpty(name)) name = "Не вказано";

                string post = GetStringProp(step1, "workPost", "Не вказано");
                if (!string.IsNullOrEmpty(post) && post != "Не вказано") post = char.ToUpper(post[0]) + post.Substring(1).ToLower();

                string workplace = GetStringProp(step1, "workPlace", "Місце роботи не вказано");

                var declText = new StringBuilder();
                declText.AppendLine($"📝 Тип: {declType} ({year} рік)");
                declText.AppendLine($"Посада: {post}");
                declText.AppendLine($"Декларант: {name}");
                declText.AppendLine($"Місце роботи: {workplace}");

                var reItems = GetItems(fullData.TryGetProperty("step_3", out var s3) ? s3 : default).ToList();
                if (reItems.Count > 0)
                {
                    declText.AppendLine(" \n🏠 НЕРУХОМІСТЬ:");
                    foreach (var obj in reItems)
                    {
                        string owner = GetOwnerTag(obj);
                        string objType = GetStringProp(obj, "objectType", "Об'єкт");
                        if (objType.Length > 0) objType = char.ToUpper(objType[0]) + objType.Substring(1).ToLower();
                        string area = GetStringProp(obj, "totalArea", "?");
                        declText.AppendLine($"   • {owner} {objType}: {area} м²");
                    }
                }

                var vehItems = GetItems(fullData.TryGetProperty("step_6", out var s6) ? s6 : default).ToList();
                if (vehItems.Count > 0)
                {
                    declText.AppendLine(" \n🚗 ТРАНСПОРТНІ ЗАСОБИ:");
                    foreach (var obj in vehItems)
                    {
                        string owner = GetOwnerTag(obj);
                        string brand = GetStringProp(obj, "brand", "Невідомо");
                        string model = GetStringProp(obj, "model", "");
                        string yearVeh = GetStringProp(obj, "graduationYear", "?");
                        declText.AppendLine($"   • {owner} Авто: {brand} {model} ({yearVeh} р.)");
                    }
                }

                var incItems = GetItems(fullData.TryGetProperty("step_11", out var s11) ? s11 : default).ToList();
                if (incItems.Count > 0)
                {
                    declText.AppendLine(" \n💵 ДОХОДИ ЗА РІК:");
                    foreach (var obj in incItems)
                    {
                        string owner = GetOwnerTag(obj);
                        string incType = GetStringProp(obj, "objectType", "Дохід");
                        if (incType.Length > 0) incType = char.ToUpper(incType[0]) + incType.Substring(1).ToLower();
                        string amount = GetStringProp(obj, "sizeIncome", "0");
                        declText.AppendLine($"   • {owner} {incType}: {amount} грн");
                    }
                }

                var moneyItems = GetItems(fullData.TryGetProperty("step_12", out var s12) ? s12 : default).ToList();
                if (moneyItems.Count > 0)
                {
                    declText.AppendLine(" \n💰 ГРОШОВІ АКТИВИ:");
                    foreach (var obj in moneyItems)
                    {
                        string owner = GetOwnerTag(obj);
                        string assetType = GetStringProp(obj, "objectType", "Активи");
                        if (assetType.Length > 0) assetType = char.ToUpper(assetType[0]) + assetType.Substring(1).ToLower();
                        string amount = GetStringProp(obj, "sizeAssets", "0");
                        string currency = GetStringProp(obj, "assetsCurrency", "UAH");
                        declText.AppendLine($"   • {owner} {assetType}: {amount} {currency}");
                    }
                }

                if (reItems.Count == 0 && vehItems.Count == 0 && incItems.Count == 0 && moneyItems.Count == 0)
                {
                    declText.AppendLine(" \nℹ️ Майно, доходи та активи не задекларовано.");
                }

                return declText.ToString().Trim();
            }
            catch
            {
                return null;
            }
            finally
            {
                _detailGate.Release();
            }
        }

        private static string GetStringProp(JsonElement element, string propName, string defValue)
        {
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propName, out var prop))
                return prop.ToString();
            return defValue;
        }

        private static IEnumerable<JsonElement> GetItems(JsonElement stepElement)
        {
            if (stepElement.ValueKind != JsonValueKind.Object) yield break;
            if (stepElement.TryGetProperty("isNotApplicable", out var isNotApplicable) && isNotApplicable.ToString() == "1") yield break;

            if (stepElement.TryGetProperty("data", out var dataElement))
            {
                if (dataElement.ValueKind == JsonValueKind.Array)
                    foreach (var item in dataElement.EnumerateArray()) yield return item;
                else if (dataElement.ValueKind == JsonValueKind.Object)
                    foreach (var prop in dataElement.EnumerateObject()) yield return prop.Value;
            }
        }

        private static string GetOwnerTag(JsonElement obj)
        {
            bool isDecl = false, isFam = false;

            if (obj.TryGetProperty("rights", out var rightsElement))
            {
                foreach (var r in EnumerateArrayOrObject(rightsElement))
                {
                    if (r.TryGetProperty("rightBelongs", out var rb))
                    {
                        string rbStr = rb.ToString();
                        if (rbStr == "1") isDecl = true;
                        else if (!string.IsNullOrEmpty(rbStr) && rbStr != "0") isFam = true;
                    }
                }
            }

            if (obj.TryGetProperty("person_who_care", out var pwcElement))
            {
                foreach (var p in EnumerateArrayOrObject(pwcElement))
                {
                    if (p.TryGetProperty("person", out var person))
                    {
                        string pStr = person.ToString();
                        if (pStr == "1") isDecl = true;
                        else if (!string.IsNullOrEmpty(pStr) && pStr != "0") isFam = true;
                    }
                }
            }

            if (isDecl && isFam) return "[Спільна]";
            if (isDecl) return "[Декларант]";
            if (isFam) return "[Член сім'ї]";
            return "[?]";
        }

        private static IEnumerable<JsonElement> EnumerateArrayOrObject(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Array) return element.EnumerateArray();
            if (element.ValueKind == JsonValueKind.Object) return element.EnumerateObject().Select(p => p.Value);
            return Enumerable.Empty<JsonElement>();
        }
    }
}