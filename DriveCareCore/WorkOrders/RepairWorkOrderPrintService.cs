using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace DriveCareCore.WorkOrders
{
    public static class RepairWorkOrderPrintService
    {
        private const string TokensTemplateFile = "shablon_tokens.docx";
        private const string LegacyTemplateFile = "shablon.docx";
        private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

        private static readonly string[] WorkRowTokens =
        {
            RepairWorkOrderTokens.WorkCode,
            RepairWorkOrderTokens.WorkName,
            RepairWorkOrderTokens.WorkMultiplicity,
            RepairWorkOrderTokens.WorkCoefficient,
            RepairWorkOrderTokens.WorkPricePerHour,
            RepairWorkOrderTokens.WorkTime,
            RepairWorkOrderTokens.WorkCost,
            RepairWorkOrderTokens.WorkDiscount,
            RepairWorkOrderTokens.WorkAmount,
            RepairWorkOrderTokens.WorkExecutor
        };

        private static readonly string[] PartsRowTokens =
        {
            RepairWorkOrderTokens.PartsNumber,
            RepairWorkOrderTokens.PartsName,
            RepairWorkOrderTokens.PartsUnit,
            RepairWorkOrderTokens.PartsQuantity,
            RepairWorkOrderTokens.PartsPrice,
            RepairWorkOrderTokens.PartsDiscount,
            RepairWorkOrderTokens.PartsAmount
        };

        private static readonly IReadOnlyDictionary<string, string> LegacyCompanyReplacements =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["(юридическое название вашего автосервиса)"] = RepairWorkOrderTokens.CompanyName,
                ["(юридический адрес)"] = RepairWorkOrderTokens.CompanyAddress,
                ["(телефон автосервиса)"] = RepairWorkOrderTokens.CompanyPhone,
                ["(дата)"] = RepairWorkOrderTokens.OrderDate,
                ["(время)"] = RepairWorkOrderTokens.OrderTime
            };

        public static string GetDesktopOrderPath(string baseName)
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var safeBase = string.IsNullOrWhiteSpace(baseName) ? "Zakaz-naryad" : baseName.Trim();
            foreach (var c in Path.GetInvalidFileNameChars())
                safeBase = safeBase.Replace(c, '_');

            return Path.Combine(desktop, safeBase + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".docx");
        }

        /// <summary>Сохраняет заказ-наряд; при занятом файле подбирает другое имя.</summary>
        public static (bool success, string savedPath, string error) TryGenerateFilled(
            RepairWorkOrderModel model,
            string preferredPath)
        {
            if (model == null)
                return (false, null, "Нет данных для заказ-наряда.");

            try
            {
                var path = string.IsNullOrWhiteSpace(preferredPath)
                    ? GetDesktopOrderPath("Zakaz-naryad-vydacha")
                    : preferredPath;
                path = GenerateFilled(model, path);
                return (true, path, null);
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        public static void OpenDocument(string path)
        {
            TryOpenDocument(path, out _);
        }

        /// <summary>Открывает DOCX через систему; не бросает исключений (файл уже открыт в Word и т.п.).</summary>
        public static bool TryOpenDocument(string path, out string errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                errorMessage = "Файл не найден.";
                return false;
            }

            try
            {
                var psi = new ProcessStartInfo(path)
                {
                    UseShellExecute = true,
                    Verb = "open"
                };
                Process.Start(psi);
                return true;
            }
            catch (Win32Exception ex)
            {
                errorMessage = "Не удалось открыть документ. Закройте файл в Word или откройте вручную:\n" + path
                    + "\n\n" + ex.Message;
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = "Не удалось открыть документ:\n" + ex.Message + "\n\nПуть:\n" + path;
                return false;
            }
        }

        public static string GenerateEmptyForPrint(RepairWorkOrderModel model, string targetPath)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));
            if (string.IsNullOrWhiteSpace(targetPath))
                throw new ArgumentException("Укажите путь для сохранения.", nameof(targetPath));

            var values = model.ToTokenMap(includeWorkRowTokens: false);
            foreach (var token in values.Keys.ToList())
            {
                if (!RepairWorkOrderTokens.CompanyOnly.Contains(token))
                    values[token] = string.Empty;
            }

            return FillAndSave(values, targetPath, workLines: null, partLines: null);
        }

        public static string GenerateFilled(RepairWorkOrderModel model, string targetPath)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));
            if (string.IsNullOrWhiteSpace(targetPath))
                throw new ArgumentException("Укажите путь для сохранения.", nameof(targetPath));

            model.RecalculateTotals();

            var values = model.ToTokenMap(includeWorkRowTokens: false);
            var effectiveWork = model.GetEffectiveWorkLines();
            var workLines = effectiveWork != null && effectiveWork.Count > 0
                ? effectiveWork
                : null;
            var partLines = model.PartLines != null && model.PartLines.Count > 0
                ? (IReadOnlyList<RepairWorkOrderPartLine>)model.PartLines
                : null;
            return FillAndSave(values, targetPath, workLines, partLines, requireTokensTemplate: true);
        }

        private static string FillAndSave(
            IDictionary<string, string> tokenValues,
            string targetPath,
            IReadOnlyList<RepairWorkOrderWorkLine> workLines,
            IReadOnlyList<RepairWorkOrderPartLine> partLines,
            bool requireTokensTemplate = false)
        {
            var templatePath = ResolveTemplatePath(TokensTemplateFile);
            var legacyTemplate = false;
            if (!File.Exists(templatePath))
            {
                if (requireTokensTemplate)
                {
                    throw new FileNotFoundException(
                        "Не найден шаблон shablon_tokens.docx. Выполните Resources\\Words\\pack_shablon_tokens.ps1 и пересоберите проект.",
                        templatePath);
                }

                templatePath = ResolveTemplatePath(LegacyTemplateFile);
                legacyTemplate = true;
            }

            if (!File.Exists(templatePath))
                throw new FileNotFoundException("Не найден шаблон заказ-наряда.", templatePath);

            targetPath = ResolveWritableTargetPath(targetPath);
            var directory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            CopyTemplateToTarget(templatePath, targetPath);

            using (var archive = ZipFile.Open(targetPath, ZipArchiveMode.Update))
            {
                var entry = FindDocumentXmlEntry(archive)
                    ?? throw new InvalidOperationException(
                        "В шаблоне нет word/document.xml. Проверьте файл: " + templatePath);

                XDocument document;
                using (var readStream = entry.Open())
                    document = XDocument.Load(readStream);

                if (legacyTemplate)
                {
                    foreach (var pair in LegacyCompanyReplacements)
                        ReplaceTextInDocument(document, pair.Key, pair.Value);
                }

                foreach (var pair in tokenValues
                             .Where(p => !string.IsNullOrEmpty(p.Key)
                                 && !WorkRowTokens.Contains(p.Key)
                                 && !PartsRowTokens.Contains(p.Key))
                             .OrderByDescending(p => p.Key.Length))
                {
                    ReplaceTextInDocument(document, pair.Key, pair.Value ?? string.Empty);
                }

                FillWorkAndPartsTableRows(document, workLines, partLines);
                CleanupDocumentLayout(document);

                var entryPath = entry.FullName;
                entry.Delete();
                var newEntry = archive.CreateEntry(entryPath, CompressionLevel.Optimal);
                using (var writeStream = newEntry.Open())
                    document.Save(writeStream);
            }

            return targetPath;
        }

        private static void FillWorkAndPartsTableRows(
            XDocument document,
            IReadOnlyList<RepairWorkOrderWorkLine> workLines,
            IReadOnlyList<RepairWorkOrderPartLine> partLines)
        {
            if (workLines != null && workLines.Count > 0)
                FillWorkTable(document, workLines);
            else
                ClearTableRowByToken(document, RepairWorkOrderTokens.WorkCode, CreateWorkRowTokenMap(new RepairWorkOrderWorkLine()));

            if (partLines != null && partLines.Count > 0)
                FillPartsTable(document, partLines);
            else
                ClearTableRowByToken(document, RepairWorkOrderTokens.PartsNumber, CreatePartsRowTokenMap(new RepairWorkOrderPartLine()));
        }

        private static void FillWorkTable(XDocument document, IReadOnlyList<RepairWorkOrderWorkLine> workLines)
        {
            var templateRow = FindTableRowByToken(document, RepairWorkOrderTokens.WorkCode)
                ?? FindTableRowByToken(document, RepairWorkOrderTokens.WorkName);

            if (templateRow == null)
                return;

            var lines = workLines ?? Array.Empty<RepairWorkOrderWorkLine>();
            if (lines.Count == 0)
            {
                ReplaceInRow(templateRow, CreateWorkRowTokenMap(new RepairWorkOrderWorkLine()));
                return;
            }

            DuplicateAndFillTableRows(templateRow, lines.Count, (row, index) =>
                ReplaceInRow(row, CreateWorkRowTokenMap(lines[index])));
        }

        private static void FillPartsTable(XDocument document, IReadOnlyList<RepairWorkOrderPartLine> partLines)
        {
            var templateRow = FindTableRowByToken(document, RepairWorkOrderTokens.PartsNumber)
                ?? FindTableRowByToken(document, RepairWorkOrderTokens.PartsName);

            if (templateRow == null)
                return;

            var lines = partLines ?? Array.Empty<RepairWorkOrderPartLine>();
            if (lines.Count == 0)
            {
                ReplaceInRow(templateRow, CreatePartsRowTokenMap(new RepairWorkOrderPartLine()));
                return;
            }

            DuplicateAndFillTableRows(templateRow, lines.Count, (row, index) =>
                ReplaceInRow(row, CreatePartsRowTokenMap(lines[index])));
        }

        private static void DuplicateAndFillTableRows(
            XElement templateRow,
            int lineCount,
            Action<XElement, int> fillRow)
        {
            var rows = new List<XElement> { templateRow };
            var insertAfter = templateRow;
            for (var i = 1; i < lineCount; i++)
            {
                var clone = new XElement(templateRow);
                insertAfter.AddAfterSelf(clone);
                insertAfter = clone;
                rows.Add(clone);
            }

            for (var i = 0; i < lineCount; i++)
                fillRow(rows[i], i);
        }

        private static void ClearTableRowByToken(XDocument document, string markerToken, IDictionary<string, string> emptyValues)
        {
            var row = FindTableRowByToken(document, markerToken);

            if (row != null)
                ReplaceInRow(row, emptyValues);
        }

        private static Dictionary<string, string> CreatePartsRowTokenMap(RepairWorkOrderPartLine line)
        {
            line = line ?? new RepairWorkOrderPartLine();
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [RepairWorkOrderTokens.PartsNumber] = line.Number ?? string.Empty,
                [RepairWorkOrderTokens.PartsName] = line.Name ?? string.Empty,
                [RepairWorkOrderTokens.PartsUnit] = line.Unit ?? string.Empty,
                [RepairWorkOrderTokens.PartsQuantity] = line.Quantity ?? string.Empty,
                [RepairWorkOrderTokens.PartsPrice] = line.Price ?? string.Empty,
                [RepairWorkOrderTokens.PartsDiscount] = line.Discount ?? string.Empty,
                [RepairWorkOrderTokens.PartsAmount] = line.Amount ?? string.Empty
            };
        }

        private static XElement FindTableRowByToken(XDocument document, string token)
        {
            if (document == null || string.IsNullOrEmpty(token))
                return null;

            return document
                .Descendants(W + "tr")
                .FirstOrDefault(row => RowContainsToken(row, token));
        }

        private static bool RowContainsToken(XElement row, string token)
        {
            if (row == null || string.IsNullOrEmpty(token))
                return false;

            foreach (var cell in row.Elements(W + "tc"))
            {
                var combined = string.Concat(cell.Descendants(W + "t").Select(t => t.Value ?? string.Empty));
                if (combined.IndexOf(token, StringComparison.Ordinal) >= 0)
                    return true;
            }

            return false;
        }

        private static Dictionary<string, string> CreateWorkRowTokenMap(RepairWorkOrderWorkLine line)
        {
            line = line ?? new RepairWorkOrderWorkLine();
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [RepairWorkOrderTokens.WorkCode] = line.Code ?? string.Empty,
                [RepairWorkOrderTokens.WorkName] = line.Name ?? string.Empty,
                [RepairWorkOrderTokens.WorkMultiplicity] = line.Multiplicity ?? string.Empty,
                [RepairWorkOrderTokens.WorkCoefficient] = line.Coefficient ?? string.Empty,
                [RepairWorkOrderTokens.WorkPricePerHour] = line.PricePerHour ?? string.Empty,
                [RepairWorkOrderTokens.WorkTime] = line.Time ?? string.Empty,
                [RepairWorkOrderTokens.WorkCost] = line.Cost ?? string.Empty,
                [RepairWorkOrderTokens.WorkDiscount] = line.Discount ?? string.Empty,
                [RepairWorkOrderTokens.WorkAmount] = line.Amount ?? string.Empty,
                [RepairWorkOrderTokens.WorkExecutor] = line.Executor ?? string.Empty
            };
        }

        private static void ReplaceInRow(XElement row, IDictionary<string, string> tokens)
        {
            foreach (var cell in row.Elements(W + "tc"))
                ReplaceTokensInContainer(cell, tokens);
        }

        private static void ReplaceTokensInContainer(XElement container, IDictionary<string, string> tokens)
        {
            if (container == null || tokens == null || tokens.Count == 0)
                return;

            var textNodes = container.Descendants(W + "t").ToList();
            if (textNodes.Count == 0)
                return;

            var combined = string.Concat(textNodes.Select(t => t.Value ?? string.Empty));
            var updated = combined;
            foreach (var pair in tokens.OrderByDescending(p => p.Key.Length))
            {
                if (string.IsNullOrEmpty(pair.Key))
                    continue;
                if (updated.IndexOf(pair.Key, StringComparison.Ordinal) >= 0)
                    updated = updated.Replace(pair.Key, pair.Value ?? string.Empty);
            }

            if (string.Equals(combined, updated, StringComparison.Ordinal))
                return;

            textNodes[0].Value = updated;
            for (var i = 1; i < textNodes.Count; i++)
                textNodes[i].Value = string.Empty;
        }

        private static void ReplaceTextInDocument(XDocument document, string search, string replace)
        {
            if (string.IsNullOrEmpty(search))
                return;

            foreach (var paragraph in document.Descendants(W + "p"))
            {
                var textNodes = paragraph.Descendants(W + "t").ToList();
                if (textNodes.Count == 0)
                    continue;

                var combined = string.Concat(textNodes.Select(t => t.Value ?? string.Empty));
                if (!combined.Contains(search))
                    continue;

                var updated = combined.Replace(search, replace ?? string.Empty);
                textNodes[0].Value = updated;
                for (var i = 1; i < textNodes.Count; i++)
                    textNodes[i].Value = string.Empty;
            }
        }

        private static void CleanupDocumentLayout(XDocument document)
        {
            RemoveCustomerPartsSection(document);
            RemoveSurplusEmptyRows(document);
        }

        private static void RemoveCustomerPartsSection(XDocument document)
        {
            if (document == null)
                return;

            var rows = document.Descendants(W + "tr").ToList();
            var startIndex = -1;
            var endIndex = -1;

            for (var i = 0; i < rows.Count; i++)
            {
                var text = GetRowPlainText(rows[i]);
                if (text.IndexOf("{{CUSTOMER_PARTS", StringComparison.Ordinal) < 0)
                    continue;

                startIndex = i;
                while (startIndex > 0)
                {
                    var prev = GetRowPlainText(rows[startIndex - 1]);
                    if (prev.IndexOf(RepairWorkOrderTokens.PartsTotal, StringComparison.Ordinal) >= 0
                        || prev.IndexOf(RepairWorkOrderTokens.LaborCostSum, StringComparison.Ordinal) >= 0)
                        break;
                    startIndex--;
                }

                break;
            }

            if (startIndex < 0)
                return;

            for (var i = startIndex; i < rows.Count; i++)
            {
                var text = GetRowPlainText(rows[i]);
                if (text.IndexOf(RepairWorkOrderTokens.LaborCostSum, StringComparison.Ordinal) >= 0)
                {
                    endIndex = i;
                    break;
                }
            }

            var removeUntil = endIndex >= 0 ? endIndex : rows.Count;
            for (var i = startIndex; i < removeUntil; i++)
                rows[i].Remove();
        }

        private static void RemoveSurplusEmptyRows(XDocument document)
        {
            if (document == null)
                return;

            foreach (var row in document.Descendants(W + "tr").ToList())
            {
                if (ShouldRemoveEmptyRow(row))
                    row.Remove();
            }
        }

        private static bool ShouldRemoveEmptyRow(XElement row)
        {
            var text = GetRowPlainText(row).Trim();
            if (string.IsNullOrEmpty(text))
                return true;

            if (text.IndexOf("Итого", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;

            if (text.IndexOf("{{", StringComparison.Ordinal) >= 0)
                return false;

            if (IsTableHeaderRow(text))
                return false;

            if (IsSectionTitleRow(text))
                return false;

            return IsPlaceholderOnly(text);
        }

        private static bool IsTableHeaderRow(string text)
        {
            return text.IndexOf("Наименование", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("Код работы", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("Кратность", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("Ед. изм", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("Исполнитель", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsSectionTitleRow(string text)
        {
            if (text.IndexOf("Выполненные работы", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (text.IndexOf("потребител", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;

            return text.IndexOf("Запасные детали", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("расходные материалы", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsPlaceholderOnly(string text)
        {
            if (string.IsNullOrEmpty(text))
                return true;

            foreach (var ch in text)
            {
                if (char.IsWhiteSpace(ch))
                    continue;
                if (ch == '×' || ch == 'x' || ch == 'X' || ch == 'х' || ch == 'Х')
                    continue;
                if (ch == '-' || ch == '—' || ch == '_' || ch == '.' || ch == ':' || ch == '|')
                    continue;
                return false;
            }

            return true;
        }

        private static string GetRowPlainText(XElement row)
        {
            if (row == null)
                return string.Empty;

            return string.Concat(row.Descendants(W + "t").Select(t => t.Value ?? string.Empty));
        }

        private static ZipArchiveEntry FindDocumentXmlEntry(ZipArchive archive)
        {
            if (archive == null)
                return null;

            foreach (var entry in archive.Entries)
            {
                var path = (entry.FullName ?? string.Empty).Replace('\\', '/');
                if (path.Equals("word/document.xml", StringComparison.OrdinalIgnoreCase))
                    return entry;
            }

            return archive.GetEntry("word/document.xml")
                ?? archive.GetEntry("word\\document.xml");
        }

        private static string ResolveTemplatePath(string fileName)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(baseDir, "Resources", "Words", fileName);
        }

        private static void CopyTemplateToTarget(string templatePath, string targetPath)
        {
            try
            {
                File.Copy(templatePath, targetPath, overwrite: true);
            }
            catch (IOException ex)
            {
                throw new IOException(
                    "Не удалось сохранить заказ-наряд: файл занят другой программой (часто Word). " +
                    "Закройте документ или сохраните под другим именем.\n" + ex.Message, ex);
            }
        }

        /// <summary>Если целевой файл открыт в Word — сохраняем копию с другим именем.</summary>
        private static string ResolveWritableTargetPath(string targetPath)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
                return GetDesktopOrderPath("Zakaz-naryad");

            if (!File.Exists(targetPath) || CanOverwriteFile(targetPath))
                return targetPath;

            var dir = Path.GetDirectoryName(targetPath) ?? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var baseName = Path.GetFileNameWithoutExtension(targetPath) ?? "Zakaz-naryad";
            var ext = Path.GetExtension(targetPath);
            if (string.IsNullOrEmpty(ext))
                ext = ".docx";

            for (var i = 2; i < 50; i++)
            {
                var candidate = Path.Combine(dir, baseName + "_" + i + ext);
                if (!File.Exists(candidate) || CanOverwriteFile(candidate))
                    return candidate;
            }

            return Path.Combine(dir, baseName + "_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ext);
        }

        private static bool CanOverwriteFile(string path)
        {
            try
            {
                using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
                    return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
