using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace DriveCarePro.Services.RepairWorkOrder
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

            return Path.Combine(desktop, safeBase + "_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".docx");
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

            return FillAndSave(values, targetPath, workLines: null);
        }

        public static string GenerateFilled(RepairWorkOrderModel model, string targetPath)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));
            if (string.IsNullOrWhiteSpace(targetPath))
                throw new ArgumentException("Укажите путь для сохранения.", nameof(targetPath));

            var values = model.ToTokenMap(includeWorkRowTokens: false);
            // При записи на сервис — только клиент, авто и причина; таблицы работ/запчастей пустые.
            return FillAndSave(values, targetPath, workLines: null);
        }

        public static void OpenDocument(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return;

            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }

        private static string FillAndSave(
            IDictionary<string, string> tokenValues,
            string targetPath,
            IReadOnlyList<RepairWorkOrderWorkLine> workLines)
        {
            var templatePath = ResolveTemplatePath(TokensTemplateFile);
            var legacyTemplate = false;
            if (!File.Exists(templatePath))
            {
                templatePath = ResolveTemplatePath(LegacyTemplateFile);
                legacyTemplate = true;
            }

            if (!File.Exists(templatePath))
                throw new FileNotFoundException("Не найден шаблон заказ-наряда.", templatePath);

            var directory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.Copy(templatePath, targetPath, overwrite: true);

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
                             .Where(p => !string.IsNullOrEmpty(p.Key) && !WorkRowTokens.Contains(p.Key))
                             .OrderByDescending(p => p.Key.Length))
                {
                    ReplaceTextInDocument(document, pair.Key, pair.Value ?? string.Empty);
                }

                ClearWorkAndPartsTableRows(document, workLines);

                var entryPath = entry.FullName;
                entry.Delete();
                var newEntry = archive.CreateEntry(entryPath, CompressionLevel.Optimal);
                using (var writeStream = newEntry.Open())
                    document.Save(writeStream);
            }

            return targetPath;
        }

        /// <summary>Очищает строки таблиц работ и запчастей (заполняются позже вручную или из системы).</summary>
        private static void ClearWorkAndPartsTableRows(XDocument document, IReadOnlyList<RepairWorkOrderWorkLine> workLines)
        {
            if (workLines != null && workLines.Count > 0)
                FillWorkTable(document, workLines);
            else
                ClearTableRowByToken(document, RepairWorkOrderTokens.WorkName, CreateRowTokenMap(new RepairWorkOrderWorkLine()));

            ClearTableRowByToken(document, RepairWorkOrderTokens.PartsName, CreatePartsRowTokenMap());
        }

        private static void FillWorkTable(XDocument document, IReadOnlyList<RepairWorkOrderWorkLine> workLines)
        {
            var templateRow = document
                .Descendants(W + "tr")
                .FirstOrDefault(row => RowContainsToken(row, RepairWorkOrderTokens.WorkName));

            if (templateRow == null)
                return;

            var lines = workLines ?? Array.Empty<RepairWorkOrderWorkLine>();
            if (lines.Count == 0)
            {
                ReplaceInRow(templateRow, CreateRowTokenMap(new RepairWorkOrderWorkLine()));
                return;
            }

            var rows = new List<XElement> { templateRow };
            var insertAfter = templateRow;
            for (var i = 1; i < lines.Count; i++)
            {
                var clone = new XElement(templateRow);
                insertAfter.AddAfterSelf(clone);
                insertAfter = clone;
                rows.Add(clone);
            }

            for (var i = 0; i < lines.Count; i++)
                ReplaceInRow(rows[i], CreateRowTokenMap(lines[i]));
        }

        private static void ClearTableRowByToken(XDocument document, string markerToken, IDictionary<string, string> emptyValues)
        {
            var row = document
                .Descendants(W + "tr")
                .FirstOrDefault(r => RowContainsToken(r, markerToken));

            if (row != null)
                ReplaceInRow(row, emptyValues);
        }

        private static Dictionary<string, string> CreatePartsRowTokenMap()
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [RepairWorkOrderTokens.PartsNumber] = string.Empty,
                [RepairWorkOrderTokens.PartsName] = string.Empty,
                [RepairWorkOrderTokens.PartsUnit] = string.Empty,
                [RepairWorkOrderTokens.PartsQuantity] = string.Empty,
                [RepairWorkOrderTokens.PartsPrice] = string.Empty,
                [RepairWorkOrderTokens.PartsDiscount] = string.Empty,
                [RepairWorkOrderTokens.PartsAmount] = string.Empty
            };
        }

        private static bool RowContainsToken(XElement row, string token)
        {
            foreach (var text in row.Descendants(W + "t"))
            {
                if (text.Value != null && text.Value.IndexOf(token, StringComparison.Ordinal) >= 0)
                    return true;
            }

            return false;
        }

        private static Dictionary<string, string> CreateRowTokenMap(RepairWorkOrderWorkLine line)
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
            foreach (var text in row.Descendants(W + "t"))
            {
                if (string.IsNullOrEmpty(text.Value))
                    continue;

                foreach (var pair in tokens)
                {
                    if (text.Value.IndexOf(pair.Key, StringComparison.Ordinal) >= 0)
                        text.Value = text.Value.Replace(pair.Key, pair.Value ?? string.Empty);
                }
            }
        }

        private static void ReplaceTextInDocument(XDocument document, string search, string replace)
        {
            if (string.IsNullOrEmpty(search))
                return;

            foreach (var text in document.Descendants(W + "t"))
            {
                if (!string.IsNullOrEmpty(text.Value) && text.Value.Contains(search))
                    text.Value = text.Value.Replace(search, replace ?? string.Empty);
            }
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
    }
}
